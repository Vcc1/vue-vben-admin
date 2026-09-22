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

        await signInManager.SignInAsync(user, request.RememberMe);

        TokenPair? tokens = null;
        if (string.Equals(request.Mode, "token", StringComparison.OrdinalIgnoreCase))
        {
            tokens = await CreateTokenPairAsync(user, ipAddress, cancellationToken);
        }

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

        var replacement = await CreateTokenPairAsync(user, ipAddress, cancellationToken);
        var replacementEntity = await dbContext.RefreshTokens.SingleAsync(
            x => x.TokenHash == HashSecret(replacement.RefreshToken), cancellationToken);
        stored.RevokedAtUtc = now;
        stored.ReplacedByTokenId = replacementEntity.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return replacement;
    }

    public Task LogoutAsync(CancellationToken cancellationToken) => signInManager.SignOutAsync();

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

    private async Task<TokenPair> CreateTokenPairAsync(AdminUser user, string? ipAddress, CancellationToken cancellationToken)    {
        var jwtOptions = options.Value.Jwt;
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
        {
            throw new InvalidOperationException("AdminIdentity:Jwt:SigningKey must contain at least 32 bytes.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
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
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = ipAddress
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TokenPair(accessToken, accessExpires, refreshToken, refreshExpires);
    }

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
