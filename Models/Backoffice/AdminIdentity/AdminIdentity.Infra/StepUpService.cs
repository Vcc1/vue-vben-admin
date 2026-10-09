using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdminIdentity.App;
using Auditing.App;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdminIdentity.Infra;

/// <summary>
/// 二次验证（step-up）挑战的签发与校验。
/// stepUpToken 由 DataProtection 加密签名、自带过期时间；挑战与验证码状态放 <see cref="IStepUpStateStore"/>，
/// 这样发码与验码落到不同副本、或进程重启，用户都不会被卡在流程中间。
/// 验证码只以 SHA256 形式存放，明文既不进数据库也不进日志。
/// </summary>
internal sealed class StepUpService(
    IDataProtectionProvider dataProtection,
    ISessionService sessions,
    UserManager<AdminUser> userManager,
    IEnumerable<IOtpDeliveryChannel> deliveryChannels,
    IStepUpStateStore stateStore,
    IOptions<AdminIdentityOptions> options,
    ISecurityEventLogger securityEvents,
    TimeProvider timeProvider,
    ILogger<StepUpService> logger)
    : IStepUpService
{
    private const string ProtectionPurpose = "MengtuV.Admin.StepUp.v1";
    private const string SessionKeyPrefix = "s:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, IOtpDeliveryChannel> channels =
        deliveryChannels.ToDictionary(x => x.Channel, StringComparer.OrdinalIgnoreCase);

    private IDataProtector Protector => dataProtection.CreateProtector(ProtectionPurpose);

    public async Task<StepUpChallenge?> TryCreateLoginChallengeAsync(
        Guid userId, string? deviceId, string? deviceInfoJson, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(Math.Max(1, options.Value.StepUp.ChallengeMinutes));
        var jti = Guid.CreateVersion7().ToString("N");

        var state = new StepUpChallengeState
        {
            UserId = userId,
            DeviceId = deviceId,
            DeviceInfoJson = deviceInfoJson,
            PasswordVerifiedAtUtc = now,
            ExpiresAtUtc = expires,
            Reason = reason,
        };

        var available = new List<string>();
        foreach (var (name, channel) in channels)
        {
            var destination = await channel.ResolveDestinationAsync(userId, cancellationToken);
            if (destination is null)
            {
                continue;
            }
            state.Destinations[name] = destination;
            available.Add(name);
        }

        if (available.Count == 0)
        {
            await AuditAsync("StepupUnavailable", "Medium", userId,
                new { Reason = reason, Detail = "没有可用的强认证通道" }, cancellationToken);
            return null;
        }

        await stateStore.SetAsync(jti, state, expires - now, cancellationToken);

        return new StepUpChallenge(
            Protect(new StepUpTokenPayload(jti, userId, deviceId, deviceInfoJson, now, expires)),
            available,
            Mask(state.Destinations[available[0]]),
            (int)(expires - now).TotalSeconds,
            reason);
    }

    public async Task SendCodeAsync(string stepUpToken, string channel, CancellationToken cancellationToken)
    {
        var payload = Unprotect(stepUpToken);
        var state = await RequireAsync(payload.Jti, payload.ExpiresAtUtc, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (!state.Destinations.TryGetValue(channel, out var destination))
        {
            throw new StepUpException(StepUpException.ChannelUnavailable, "该验证方式对当前账号不可用。");
        }

        var cooldown = TimeSpan.FromSeconds(Math.Max(0, options.Value.StepUp.ResendCooldownSeconds));
        if (state.LastSentAtUtc is { } lastSent && now - lastSent < cooldown)
        {
            throw new StepUpException(StepUpException.RateLimited,
                $"请等待 {(int)(cooldown - (now - lastSent)).TotalSeconds} 秒后重新获取验证码。");
        }

        var hourWindowStart = now.AddHours(-1);
        if (state.SentWindowStartUtc < hourWindowStart)
        {
            state.SentWindowStartUtc = now;
            state.SentCount = 0;
        }
        if (state.SentCount >= Math.Max(1, options.Value.StepUp.MaxCodesPerHour))
        {
            throw new StepUpException(StepUpException.RateLimited, "验证码发送次数过多，请稍后再试。");
        }

        if (!channels.TryGetValue(channel, out var delivery))
        {
            throw new StepUpException(StepUpException.ChannelUnavailable, "该验证方式对当前账号不可用。");
        }

        var code = GenerateCode();
        state.CodeHash = Hash(code);
        state.CodeChannel = channel;
        state.CodeExpiresAtUtc = now.AddMinutes(Math.Max(1, options.Value.StepUp.CodeTtlMinutes));
        state.Attempts = 0;
        state.LastSentAtUtc = now;
        state.SentCount++;

        await stateStore.SetAsync(payload.Jti, state, state.ExpiresAtUtc - now, cancellationToken);
        await delivery.SendAsync(destination, code, cancellationToken);
        await AuditAsync("StepupSent", "Low", state.UserId, new { state.DeviceId, Channel = channel, Target = Mask(destination) }, cancellationToken);
    }

    public async Task<StepUpVerification> VerifyLoginAsync(
        string stepUpToken, string channel, string credential, CancellationToken cancellationToken)
    {
        var payload = Unprotect(stepUpToken);
        var state = await RequireAsync(payload.Jti, payload.ExpiresAtUtc, cancellationToken);

        var matched = await MatchCodeAsync(payload.Jti, state, channel, credential, cancellationToken);
        if (!matched)
        {
            await AuditAsync("StepupFailed", "Medium", state.UserId,
                new { state.DeviceId, Channel = channel, state.Attempts }, cancellationToken);

            if (state.Attempts >= Math.Max(1, options.Value.StepUp.MaxVerifyAttempts))
            {
                await stateStore.RemoveAsync(payload.Jti, cancellationToken);
                throw new StepUpException(StepUpException.TooManyAttempts, "验证失败次数过多，请重新登录。");
            }
            throw new StepUpException(StepUpException.InvalidCode, "验证码不正确或已过期。");
        }

        await stateStore.RemoveAsync(payload.Jti, cancellationToken);
        await AuditAsync("StepupSuccess", "Low", state.UserId, new { state.DeviceId, Channel = channel, Purpose = "login" }, cancellationToken);
        logger.LogInformation("用户 {UserId} 通过 {Channel} 完成登录二次验证。", state.UserId, channel);

        return new StepUpVerification(state.UserId, state.DeviceId, channel, state.DeviceInfoJson);
    }

    public async Task<StepUpChallenge> SendSessionCodeAsync(string sid, string channel, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(sid, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (!channels.TryGetValue(channel, out var delivery)
            || await delivery.ResolveDestinationAsync(session.UserId, cancellationToken) is not { } destination)
        {
            throw new StepUpException(StepUpException.ChannelUnavailable, "该验证方式对当前账号不可用。");
        }

        var expires = now.AddMinutes(Math.Max(1, options.Value.StepUp.ChallengeMinutes));
        var state = new StepUpChallengeState
        {
            UserId = session.UserId,
            DeviceId = session.DeviceId,
            Sid = sid,
            PasswordVerifiedAtUtc = session.PasswordVerifiedAtUtc,
            ExpiresAtUtc = expires,
            Reason = "session_stepup",
        };
        state.Destinations[channel] = destination;

        var code = GenerateCode();
        state.CodeHash = Hash(code);
        state.CodeChannel = channel;
        state.CodeExpiresAtUtc = now.AddMinutes(Math.Max(1, options.Value.StepUp.CodeTtlMinutes));
        state.LastSentAtUtc = now;
        state.SentCount = 1;

        var key = SessionKeyPrefix + sid;
        await stateStore.SetAsync(key, state, expires - now, cancellationToken);

        await delivery.SendAsync(destination, code, cancellationToken);
        await AuditAsync("StepupSent", "Low", session.UserId, new { Sid = sid, Channel = channel, Target = Mask(destination) }, cancellationToken);

        // 会话内挑战以当前 sid 为凭据，无需再下发 stepUpToken。
        return new StepUpChallenge(string.Empty, [channel], Mask(destination), (int)(expires - now).TotalSeconds, "session_stepup");
    }

    public async Task<StepUpVerification> VerifyInSessionAsync(
        string sid, SessionStepUpRequest request, CancellationToken cancellationToken)
    {
        var session = await RequireSessionAsync(sid, cancellationToken);
        var channel = request.Channel?.Trim() ?? string.Empty;

        if (string.Equals(channel, StepUpChannels.Password, StringComparison.OrdinalIgnoreCase))
        {
            var user = await userManager.FindByIdAsync(session.UserId.ToString());
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Credential ?? string.Empty))
            {
                await AuditAsync("StepupFailed", "Medium", session.UserId, new { Sid = sid, Channel = channel }, cancellationToken);
                throw new StepUpException(StepUpException.InvalidPassword, "当前密码不正确。");
            }

            await sessions.GrantPasswordVerifiedAsync(sid, cancellationToken);
            await AuditAsync("StepupSuccess", "Low", session.UserId, new { Sid = sid, Channel = channel, Level = SessionLevels.L2 }, cancellationToken);
            return new StepUpVerification(session.UserId, session.DeviceId, StepUpChannels.Password, null);
        }

        var key = SessionKeyPrefix + sid;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var state = await stateStore.GetAsync(key, cancellationToken);
        if (state is null || state.ExpiresAtUtc <= now)
        {
            await stateStore.RemoveAsync(key, cancellationToken);
            throw new StepUpException(StepUpException.Expired, "验证码已过期，请重新获取。");
        }

        if (!await MatchCodeAsync(key, state, channel, request.Credential ?? string.Empty, cancellationToken))
        {
            await AuditAsync("StepupFailed", "Medium", session.UserId, new { Sid = sid, Channel = channel, state.Attempts }, cancellationToken);
            if (state.Attempts >= Math.Max(1, options.Value.StepUp.MaxVerifyAttempts))
            {
                await stateStore.RemoveAsync(key, cancellationToken);
                throw new StepUpException(StepUpException.TooManyAttempts, "验证失败次数过多，请重新获取验证码。");
            }
            throw new StepUpException(StepUpException.InvalidCode, "验证码不正确或已过期。");
        }

        await stateStore.RemoveAsync(key, cancellationToken);
        await sessions.GrantStrongAuthAsync(sid, channel, cancellationToken);
        await AuditAsync("StepupSuccess", "Low", session.UserId, new { Sid = sid, Channel = channel, Level = SessionLevels.L3 }, cancellationToken);

        return new StepUpVerification(session.UserId, session.DeviceId, channel, null);
    }

    // --- helpers ---

    private async Task<SessionState> RequireSessionAsync(string sid, CancellationToken cancellationToken)
    {
        var state = await sessions.GetStateAsync(sid, cancellationToken);
        if (state is null || state.RevokedAtUtc is not null || state.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new StepUpException(StepUpException.InvalidToken, "会话不存在或已失效，请重新登录。");
        }
        return state;
    }

    /// <summary>校验并落库尝试次数：失败计数必须持久化，否则多副本下能无限次试码。</summary>
    private async Task<bool> MatchCodeAsync(
        string key, StepUpChallengeState state, string channel, string credential, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        state.Attempts++;

        var matched = state.CodeHash is not null
            && string.Equals(state.CodeChannel, channel, StringComparison.OrdinalIgnoreCase)
            && state.CodeExpiresAtUtc > now
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state.CodeHash),
                Encoding.UTF8.GetBytes(Hash(credential?.Trim() ?? string.Empty)));

        if (matched)
        {
            // 验证码一次性：命中即失效，避免同一码被重复使用。
            state.CodeHash = null;
        }

        await stateStore.SetAsync(key, state, state.ExpiresAtUtc - now, cancellationToken);
        return matched;
    }

    private async Task<StepUpChallengeState> RequireAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (expiresAtUtc <= now)
        {
            throw new StepUpException(StepUpException.Expired, "验证已超时，请重新登录。");
        }
        if (await stateStore.GetAsync(jti, cancellationToken) is not { } state)
        {
            throw new StepUpException(StepUpException.Consumed, "该验证已使用或已失效，请重新登录。");
        }
        return state;
    }

    private string Protect(StepUpTokenPayload payload) =>
        Protector.Protect(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions)));

    private StepUpTokenPayload Unprotect(string? stepUpToken)
    {
        if (string.IsNullOrWhiteSpace(stepUpToken))
        {
            throw new StepUpException(StepUpException.InvalidToken, "缺少验证凭据。");
        }
        try
        {
            var json = Protector.Unprotect(stepUpToken);
            return JsonSerializer.Deserialize<StepUpTokenPayload>(Convert.FromBase64String(json), JsonOptions)
                ?? throw new StepUpException(StepUpException.InvalidToken, "验证凭据无效。");
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            throw new StepUpException(StepUpException.InvalidToken, "验证凭据无效。");
        }
    }

    private string GenerateCode()
    {
        var length = Math.Clamp(options.Value.StepUp.CodeLength, 4, 10);
        var max = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, max).ToString($"D{length}");
    }

    private Task AuditAsync(string eventType, string severity, Guid userId, object detail, CancellationToken cancellationToken) =>
        securityEvents.LogAsync(new SecurityEvent(eventType, severity, null, detail, ActorId: userId.ToString()), cancellationToken);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Mask(string destination)
    {
        var at = destination.IndexOf('@');
        if (at < 0)
        {
            return destination.Length <= 4 ? "***" : $"{destination[..2]}****{destination[^2..]}";
        }
        return at <= 1 ? $"***{destination[at..]}" : $"{destination[0]}***{destination[at..]}";
    }

    private sealed record StepUpTokenPayload(
        string Jti, Guid UserId, string? DeviceId, string? DeviceInfoJson, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);
}
