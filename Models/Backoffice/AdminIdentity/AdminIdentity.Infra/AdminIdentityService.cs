using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdminIdentity.App;
using Auditing.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AdminIdentity.Infra;

internal sealed class AdminIdentityService(
    UserManager<AdminUser> userManager,
    SignInManager<AdminUser> signInManager,
    RoleManager<AdminRole> roleManager,
    AdminDbContext dbContext,
    IOptions<AdminIdentityOptions> options,
    ICurrentActorAccessor actorAccessor,
    ISecurityEventLogger securityEvents,
    ISessionService sessionService,
    IDeviceTrustService deviceTrust,
    IStepUpService stepUpService,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider)
    : IAdminAuthenticationService, IAdminUserService, IAdminApiKeyService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<LoginResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Mode, "cookie", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Mode, "token", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Login mode must be 'cookie' or 'token'.", nameof(request));

        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.IsEnabled)
        {
            return null;
        }

        var checkedPassword = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!checkedPassword.Succeeded)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        await userManager.UpdateAsync(user);

        var deviceInfoJson = SerializeDeviceInfo(request.DeviceInfo);

        // deviceId 优先取 AS 侧 cookie（首次登录会自动下发）；
        // 请求体里的 deviceId 只作为原生客户端等非浏览器场景的显式覆盖。
        var deviceId = string.IsNullOrWhiteSpace(request.DeviceId)
            ? DeviceCookie.Ensure(httpContextAccessor.HttpContext)
            : request.DeviceId.Trim();

        // 受信设备只需密码；新设备与被踢设备必须再走一次强认证（HTTP 428）。
        var trust = await deviceTrust.EvaluateAsync(user.Id, deviceId, cancellationToken);
        if (!trust.IsTrusted)
        {
            var challenge = await stepUpService.CreateLoginChallengeAsync(user.Id, deviceId, deviceInfoJson, trust.Reason, cancellationToken);
            await securityEvents.LogAsync(new SecurityEvent(
                "LoginStepUpRequired", "Low", "密码校验通过但设备不受信，要求二次验证。",
                new { user.Id, DeviceId = deviceId, trust.Reason, IpAddress = ipAddress },
                ActorId: user.Id.ToString(), ActorName: user.UserName), cancellationToken);
            return new LoginResult(await MapUserAsync(user), null, challenge);
        }

        return await EstablishSessionAsync(
            user, deviceId, deviceInfoJson, SessionLoginMethods.Password,
            strongAuthMethod: null, request.Mode, request.RememberMe, ipAddress, trustDevice: false, cancellationToken);
    }

    public async Task<LoginResult> CompleteStepUpLoginAsync(
        StepUpVerification verification, string mode, bool rememberMe, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(verification.UserId.ToString())
            ?? throw new KeyNotFoundException("User was not found.");
        if (!user.IsEnabled)
        {
            throw new InvalidOperationException("该账号已停用。");
        }

        // 设备信息在 428 往返中被封存在 stepUpToken 里，二次验证通过时随结果带回。
        return await EstablishSessionAsync(
            user, verification.DeviceId, verification.DeviceInfoJson, SessionLoginMethods.Otp,
            strongAuthMethod: verification.Channel, mode, rememberMe, ipAddress, trustDevice: true, cancellationToken);
    }

    /// <summary>创建 sid、签入 cookie（带 sid 与等级 claim）、按需签发 token。</summary>
    private async Task<LoginResult> EstablishSessionAsync(
        AdminUser user,
        string? deviceId,
        string? deviceInfoJson,
        string loginMethod,
        string? strongAuthMethod,
        string mode,
        bool rememberMe,
        string? ipAddress,
        bool trustDevice,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var strongAuthAt = strongAuthMethod is null ? (DateTime?)null : now;

        var sid = await sessionService.CreateAsync(new CreateSessionContext(
            user.Id,
            deviceId,
            deviceInfoJson,
            loginMethod,
            ipAddress,
            actorAccessor.Current.UserAgent,
            PasswordVerifiedAtUtc: now,
            StrongAuthAtUtc: strongAuthAt,
            strongAuthMethod,
            TimeSpan.FromDays(Math.Clamp(options.Value.Jwt.RefreshTokenDays, 1, 365))),
            cancellationToken);

        if (trustDevice && deviceId is not null)
        {
            await deviceTrust.MarkTrustedAsync(user.Id, deviceId, deviceInfoJson, sid, cancellationToken);
        }

        var facts = new SessionFacts(sid, deviceId, loginMethod, now, now, strongAuthAt, strongAuthMethod);

        await SignInWithSessionAsync(user, facts, rememberMe);

        TokenPair? tokens = null;
        if (string.Equals(mode, "token", StringComparison.OrdinalIgnoreCase))
        {
            tokens = await CreateTokenPairAsync(user, facts, ipAddress, now, cancellationToken);
        }

        await securityEvents.LogAsync(new SecurityEvent(
            "LoginSuccess", "Low", $"登录成功（{loginMethod}）。",
            new { Sid = sid, user.Id, DeviceId = deviceId, LoginMethod = loginMethod, IpAddress = ipAddress },
            ActorId: user.Id.ToString(), ActorName: user.UserName), cancellationToken);

        return new LoginResult(await MapUserAsync(user), tokens);
    }

    public async Task<TokenPair?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var hash = HashSecret(refreshToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null || stored.ExpiresAtUtc <= now)
        {
            return null;
        }

        // 重放检测：已吊销的 refresh token 被再次使用。
        // 宽限窗口内视为并发竞态（如多页签一前一后刷新），仅拒绝本次刷新，不吊销令牌链；
        // 超过窗口则判定为令牌泄露，记录安全事件并吊销整条链，强制该链上所有会话重新登录。
        if (stored.RevokedAtUtc is not null)
        {
            var leeway = TimeSpan.FromSeconds(Math.Max(0, options.Value.Jwt.RefreshTokenReuseLeewaySeconds));
            var revokedAge = now - stored.RevokedAtUtc.Value;
            if (revokedAge > leeway)
            {
                await securityEvents.LogAsync(new SecurityEvent(
                    "RefreshTokenReplay",
                    "High",
                    "已吊销的 refresh token 在宽限窗口外被重复使用，疑似令牌泄露，已吊销整条令牌链。",
                    new
                    {
                        stored.UserId,
                        RefreshTokenId = stored.Id,
                        RevokedAtUtc = stored.RevokedAtUtc,
                        AgeSeconds = (int)revokedAge.TotalSeconds,
                        LeewaySeconds = (int)leeway.TotalSeconds,
                        IpAddress = ipAddress
                    },
                    ActorId: stored.UserId.ToString()),
                    cancellationToken);
                await RevokeTokenChainAsync(stored, now, cancellationToken);
            }

            return null;
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsEnabled)
        {
            return null;
        }

        // 凭证级吊销：refresh token 所属 sid 被踢后，续期必须被拒绝。
        if (stored.Sid is not null && await sessionService.IsRevokedAsync(stored.Sid, cancellationToken))
        {
            await securityEvents.LogAsync(new SecurityEvent(
                "RefreshDenied", "Medium", "refresh token 所属会话已被吊销，拒绝续期。",
                new { stored.UserId, Sid = stored.Sid, IpAddress = ipAddress },
                ActorId: stored.UserId.ToString()),
                cancellationToken);
            return null;
        }

        var session = stored.Sid is null
            ? null
            : await sessionService.GetStateAsync(stored.Sid, cancellationToken);

        var facts = new SessionFacts(
            stored.Sid,
            stored.DeviceId ?? session?.DeviceId,
            session?.LoginMethod ?? SessionLoginMethods.Password,
            session?.PasswordVerifiedAtUtc ?? now,
            session?.PasswordVerifiedAtUtc,
            session?.StrongAuthAtUtc,
            session?.StrongAuthMethod);

        var replacement = await CreateTokenPairAsync(user, facts, ipAddress, now, cancellationToken);

        var replacementEntity = await dbContext.RefreshTokens.SingleAsync(
            x => x.TokenHash == HashSecret(replacement.RefreshToken), cancellationToken);
        stored.RevokedAtUtc = now;
        stored.ReplacedByTokenId = replacementEntity.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return replacement;
    }

    public Task LogoutAsync(CancellationToken cancellationToken) => LogoutAsync(null, cancellationToken);

    public async Task LogoutAsync(string? sid, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sid))
        {
            await sessionService.RevokeAsync(sid, "self-logout", cancellationToken);
        }
        await signInManager.SignOutAsync();
    }

    public async Task<LoginResult?> ReissueAsync(string? sid, string mode, string? ipAddress, CancellationToken cancellationToken)
    {
        var state = await sessionService.GetStateAsync(sid, cancellationToken);
        if (state is null || state.RevokedAtUtc is not null || state.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(state.UserId.ToString());
        if (user is null || !user.IsEnabled)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var facts = new SessionFacts(
            state.Sid,
            state.DeviceId,
            state.LoginMethod,
            state.PasswordVerifiedAtUtc ?? now,
            state.PasswordVerifiedAtUtc,
            state.StrongAuthAtUtc,
            state.StrongAuthMethod);

        await SignInWithSessionAsync(user, facts, rememberMe: true);

        TokenPair? tokens = null;
        if (string.Equals(mode, "token", StringComparison.OrdinalIgnoreCase))
        {
            tokens = await CreateTokenPairAsync(user, facts, ipAddress, now, cancellationToken);
        }

        return new LoginResult(await MapUserAsync(user), tokens);
    }

    public async Task<AdminUserItem?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var id = actorAccessor.Current.ActorType == "User" ? actorAccessor.Current.ActorId : null;
        if (!Guid.TryParse(id, out var userId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : await MapUserAsync(user);
    }

    public async Task<IReadOnlyList<AdminUserItem>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.OrderBy(x => x.UserName).ToListAsync(cancellationToken);
        var result = new List<AdminUserItem>(users.Count);
        foreach (var user in users)
        {
            result.Add(await MapUserAsync(user));
        }
        return result;
    }

    public async Task<AdminUserItem> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        await ValidateRolesAsync(request.Roles);
        var actor = actorAccessor.Current;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new AdminUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.UserName.Trim(),
            Email = request.Email?.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Name = request.Name.Trim(),
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        EnsureSucceeded(await userManager.CreateAsync(user, request.Password));
        EnsureSucceeded(await userManager.AddToRolesAsync(user, request.Roles.Distinct(StringComparer.OrdinalIgnoreCase)));
        await AddUserAuditAsync("Create", user.Id, null, MapAuditUser(user, request.Roles), actor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapUserAsync(user);
    }

    public async Task SetEnabledAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await RequiredUserAsync(userId);
        var before = MapAuditUser(user, await userManager.GetRolesAsync(user));
        if (!isEnabled && user.IsEnabled && await userManager.IsInRoleAsync(user, AdminRoles.Admin))
            await EnsureAnotherAdministratorAsync(userId, cancellationToken);
        user.IsEnabled = isEnabled;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        await AddUserAuditAsync(isEnabled ? "Enable" : "Disable", userId, before,
            MapAuditUser(user, await userManager.GetRolesAsync(user)), actorAccessor.Current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequiredUserAsync(userId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, newPassword));
        await AddUserAuditAsync("ResetPassword", userId, null, new { user.Id, user.UserName }, actorAccessor.Current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        await ValidateRolesAsync(roles);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await RequiredUserAsync(userId);
        var existing = await userManager.GetRolesAsync(user);
        if (user.IsEnabled && existing.Contains(AdminRoles.Admin, StringComparer.OrdinalIgnoreCase) &&
            !roles.Contains(AdminRoles.Admin, StringComparer.OrdinalIgnoreCase))
            await EnsureAnotherAdministratorAsync(userId, cancellationToken);
        EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, existing));
        EnsureSucceeded(await userManager.AddToRolesAsync(user, roles.Distinct(StringComparer.OrdinalIgnoreCase)));
        await AddUserAuditAsync("SetRoles", userId, new { Roles = existing }, new { Roles = roles }, actorAccessor.Current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdminUserItem> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await RequiredUserAsync(userId);
        var before = new { user.DisplayName };
        user.DisplayName = request.DisplayName.Trim();
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        await AddUserAuditAsync("UpdateProfile", userId, before,
            new { user.DisplayName }, actorAccessor.Current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapUserAsync(user);
    }

    public async Task<AdminUserItem> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await RequiredUserAsync(userId);
        var before = new { user.Name, user.DisplayName, user.Email };
        user.Name = request.Name.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email?.Trim();
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        await AddUserAuditAsync("UpdateUser", userId, before,
            new { user.Name, user.DisplayName, user.Email }, actorAccessor.Current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapUserAsync(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await RequiredUserAsync(userId);
        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            throw new InvalidOperationException("当前密码不正确。");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, request.NewPassword));
        await AddUserAuditAsync("ChangePassword", userId, null, new { user.Id, user.UserName }, actorAccessor.Current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    async Task<IReadOnlyList<ApiKeyItem>> IAdminApiKeyService.ListAsync(CancellationToken cancellationToken) =>
        await dbContext.ApiKeys.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ApiKeyItem(x.Id, x.UserId, x.Name, x.Prefix, x.CreatedAtUtc, x.ExpiresAtUtc, x.LastUsedAtUtc, x.RevokedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<CreatedApiKey> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        _ = await RequiredUserAsync(request.UserId);
        var actor = actorAccessor.Current;
        if (!Guid.TryParse(actor.ActorId, out var creatorId))
        {
            throw new InvalidOperationException("Only an authenticated user can create API keys.");
        }

        var prefix = $"ak_{Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant()}";
        var secret = $"{prefix}.{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=')}";
        var entity = new AdminApiKey
        {
            Id = Guid.CreateVersion7(),
            UserId = request.UserId,
            Name = request.Name.Trim(),
            Prefix = prefix,
            SecretHash = HashSecret(secret),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ExpiresAtUtc = request.ExpiresAtUtc,
            CreatedByUserId = creatorId
        };
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ApiKeys.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AddUserAuditAsync("CreateApiKey", request.UserId, null,
            new { entity.Id, entity.Name, entity.Prefix, entity.ExpiresAtUtc }, actor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreatedApiKey(entity.Id, entity.Name, entity.Prefix, secret, entity.ExpiresAtUtc);
    }

    public async Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ApiKeys.SingleOrDefaultAsync(x => x.Id == apiKeyId, cancellationToken)
            ?? throw new KeyNotFoundException("API key was not found.");
        if (entity.RevokedAtUtc is null)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            entity.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
            await AddUserAuditAsync("RevokeApiKey", entity.UserId, null,
                new { entity.Id, entity.Name, entity.Prefix, entity.RevokedAtUtc }, actorAccessor.Current, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task RevokeTokenChainAsync(AdminRefreshToken start, DateTime now, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { start.Id };
        var nextId = start.ReplacedByTokenId;
        while (nextId is { } id && visited.Add(id))
        {
            var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (token is null)
            {
                break;
            }
            token.RevokedAtUtc ??= now;
            nextId = token.ReplacedByTokenId;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TokenPair> CreateTokenPairAsync(
        AdminUser user, SessionFacts facts, string? ipAddress, DateTime now, CancellationToken cancellationToken)
    {
        var jwtOptions = options.Value.Jwt;
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
        {
            throw new InvalidOperationException("AdminIdentity:Jwt:SigningKey must contain at least 32 bytes.");
        }

        var accessExpires = now.AddMinutes(Math.Clamp(jwtOptions.AccessTokenMinutes, 1, 1440));
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Id.ToString()),
            new("display_name", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(BuildSessionClaims(facts, now));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims, now, accessExpires, credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var refreshExpires = now.AddDays(Math.Clamp(jwtOptions.RefreshTokenDays, 1, 365));
        dbContext.RefreshTokens.Add(new AdminRefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = HashSecret(refreshToken),
            Sid = facts.Sid,
            DeviceId = facts.DeviceId,
            UserAgent = actorAccessor.Current.UserAgent,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = ipAddress
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenPair(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    /// <summary>签入 cookie，并把 sid / 等级事实写进 principal，供中间件与授权策略读取。</summary>
    private async Task SignInWithSessionAsync(AdminUser user, SessionFacts facts, bool rememberMe)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("当前请求没有 HttpContext，无法签入 cookie。");

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var claim in BuildSessionClaims(facts, now))
        {
            identity.AddClaim(claim);
        }

        await context.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = rememberMe });
    }

    /// <summary>
    /// 会话相关 claim。acr 只是签发时刻的快照：token 生命周期内等级可能因掉窗而降低，
    /// 实时等级以 userinfo 或服务端会话校验为准。
    /// </summary>
    private IEnumerable<Claim> BuildSessionClaims(SessionFacts facts, DateTime now)
    {
        var level = SessionLevels.Evaluate(
            now, facts.PasswordVerifiedAtUtc, facts.StrongAuthAtUtc, now, PasswordGapWindow, StrongAuthWindow);

        yield return new Claim(SessionClaims.Acr, SessionLevels.ToAcr(level));
        yield return new Claim(SessionClaims.AuthTime, ToUnixSeconds(facts.AuthTimeUtc), ClaimValueTypes.Integer64);

        if (facts.Sid is not null)
        {
            yield return new Claim(SessionClaims.Sid, facts.Sid);
        }
        if (facts.DeviceId is not null)
        {
            yield return new Claim(SessionClaims.DeviceId, facts.DeviceId);
        }
        if (facts.PasswordVerifiedAtUtc is { } passwordVerifiedAt)
        {
            yield return new Claim(SessionClaims.PasswordVerifiedAt, ToUnixSeconds(passwordVerifiedAt), ClaimValueTypes.Integer64);
        }
        if (facts.StrongAuthAtUtc is { } strongAuthAt)
        {
            yield return new Claim(SessionClaims.StrongAuthAt, ToUnixSeconds(strongAuthAt), ClaimValueTypes.Integer64);
        }
        foreach (var method in AmrValues(facts))
        {
            yield return new Claim(SessionClaims.Amr, method);
        }
    }

    private static IEnumerable<string> AmrValues(SessionFacts facts)
    {
        var values = new List<string>
        {
            facts.LoginMethod switch
            {
                SessionLoginMethods.Otp => "otp",
                SessionLoginMethods.Sso => "sso",
                _ => "pwd"
            }
        };

        if (facts.StrongAuthAtUtc is not null)
        {
            values.Add("mfa");
            if (string.Equals(facts.StrongAuthMethod, StepUpChannels.Totp, StringComparison.OrdinalIgnoreCase))
            {
                values.Add("totp");
            }
        }

        return values.Distinct(StringComparer.Ordinal);
    }

    private TimeSpan PasswordGapWindow =>
        TimeSpan.FromMinutes(Math.Max(1, options.Value.Session.PasswordActivityGapMinutes));

    private TimeSpan StrongAuthWindow =>
        TimeSpan.FromMinutes(Math.Max(1, options.Value.Session.StrongAuthWindowMinutes));

    private static string ToUnixSeconds(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString();

    private static string? SerializeDeviceInfo(JsonElement? deviceInfo)
    {
        if (deviceInfo is not { } info || info.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        var json = info.GetRawText();
        return json.Length <= 2000 ? json : json[..2000];
    }

    /// <summary>签发 cookie / token 时使用的会话事实快照。</summary>
    private sealed record SessionFacts(
        string? Sid,
        string? DeviceId,
        string LoginMethod,
        DateTime AuthTimeUtc,
        DateTime? PasswordVerifiedAtUtc,
        DateTime? StrongAuthAtUtc,
        string? StrongAuthMethod);

    private async Task<AdminUserItem> MapUserAsync(AdminUser user) => new(
        user.Id,
        user.UserName ?? string.Empty,
        user.DisplayName,
        user.Name,
        user.Email,
        user.PhoneNumber,
        user.IsEnabled,
        (await userManager.GetRolesAsync(user)).ToArray(),
        user.CreatedAtUtc,
        user.LastLoginAtUtc);

    private static object MapAuditUser(AdminUser user, IEnumerable<string> roles) => new
    {
        user.Id, user.UserName, user.DisplayName, user.Name, user.Email, user.IsEnabled, Roles = roles.ToArray()
    };

    private async Task<AdminUser> RequiredUserAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new KeyNotFoundException("User was not found.");

    private async Task AddUserAuditAsync(string action, Guid? targetUserId, object? before, object? after, CurrentActor actor, CancellationToken cancellationToken)
    {
        dbContext.UserAuditLogs.Add(new AdminUserAuditLog
        {
            TargetUserId = targetUserId,
            Action = action,
            ActorType = actor.ActorType,
            ActorId = actor.ActorId,
            ActorName = actor.ActorName,
            AuthenticationType = actor.AuthenticationType,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, AuditJsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, AuditJsonOptions),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            CorrelationId = actor.CorrelationId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string HashSecret(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    private async Task ValidateRolesAsync(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var existing = await roleManager.Roles.AsNoTracking()
            .Where(x => x.Name != null).Select(x => x.Name!).ToListAsync();
        var invalid = roles.Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
        if (invalid.Length != 0)
        {
            throw new ArgumentException($"Unknown roles: {string.Join(", ", invalid)}");
        }
    }

    private async Task EnsureAnotherAdministratorAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (actorAccessor.Current.ActorId == userId.ToString())
            throw new InvalidOperationException("不能停用自己的管理员账户或移除自己的管理员角色。");
        var adminRoleId = await roleManager.Roles.Where(x => x.Name == AdminRoles.Admin)
            .Select(x => x.Id).SingleAsync(cancellationToken);
        var activeAdmins = await dbContext.UserRoles
            .Join(dbContext.Users, link => link.UserId, user => user.Id,
                (link, user) => new { link.RoleId, user.IsEnabled })
            .CountAsync(x => x.RoleId == adminRoleId && x.IsEnabled, cancellationToken);
        if (activeAdmins <= 1)
            throw new InvalidOperationException("至少需要保留一个启用的管理员账户。");
    }
}
