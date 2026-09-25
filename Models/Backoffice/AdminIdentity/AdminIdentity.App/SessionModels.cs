using System.Text.Json;

namespace AdminIdentity.App;

/// <summary>
/// 会话与等级相关的 JWT claim 名。sid/amr/acr/auth_time 为 OIDC 标准 claim，
/// pva/saa/device_id 为自有附加 claim，第三方客户端可忽略。
/// </summary>
public static class SessionClaims
{
    public const string Sid = "sid";
    public const string DeviceId = "device_id";
    public const string PasswordVerifiedAt = "pva";
    public const string StrongAuthAt = "saa";
    public const string Acr = "acr";
    public const string Amr = "amr";
    public const string AuthTime = "auth_time";
}

/// <summary>会话 assurance 等级。等级是会话的动态状态，不是授予给用户的权限。</summary>
public static class SessionLevels
{
    /// <summary>已登录：sid 有效且未被吊销。</summary>
    public const int L1 = 1;

    /// <summary>密码核验有效：连续活跃间隔未超过 <see cref="PasswordActivityGapMinutes"/>。</summary>
    public const int L2 = 2;

    /// <summary>近期强认证：强认证时间在 <see cref="StrongAuthWindowMinutes"/> 内。</summary>
    public const int L3 = 3;

    public const int Max = L3;
    public const string AcrPrefix = "urn:mengtu:acr:";

    public static string ToAcr(int level) => $"{AcrPrefix}l{Clamp(level)}";

    public static int FromAcr(string? acr)
    {
        if (string.IsNullOrEmpty(acr) || !acr.StartsWith(AcrPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return L1;
        }
        return int.TryParse(acr[AcrPrefix.Length..].TrimStart('l', 'L'), out var level) ? Clamp(level) : L1;
    }

    public static int Clamp(int level) => Math.Clamp(level, L1, Max);

    /// <summary>
    /// 由会话事实推导当前等级。
    /// L2 的条件是"距上次活跃的间隔未超过 passwordGap"——间隔超限即掉窗，
    /// 且因为 PasswordVerifiedAtUtc 不会被活动更新，掉窗后不会自动恢复（粘性）。
    /// </summary>
    public static int Evaluate(
        DateTime nowUtc,
        DateTime? passwordVerifiedAtUtc,
        DateTime? strongAuthAtUtc,
        DateTime lastActivityAtUtc,
        TimeSpan passwordGap,
        TimeSpan strongAuthWindow)
    {
        if (InWindow(nowUtc, strongAuthAtUtc, strongAuthWindow))
        {
            return L3;
        }
        if (passwordVerifiedAtUtc is not null && InWindow(nowUtc, lastActivityAtUtc, passwordGap))
        {
            return L2;
        }
        return L1;
    }

    private static bool InWindow(DateTime nowUtc, DateTime? momentUtc, TimeSpan window)
    {
        if (momentUtc is not { } moment)
        {
            return false;
        }
        var age = nowUtc - moment;
        return age >= TimeSpan.Zero && age <= window;
    }

    public static IReadOnlyList<string> SupportedAcrValues { get; } = [ToAcr(L1), ToAcr(L2), ToAcr(L3)];
}

/// <summary>
/// 向该 sid 签发过 token 的 OIDC 客户端推送标准 logout_token
/// （OIDC Back-Channel Logout 1.0）。推送失败不阻塞吊销，由 access token 寿命兜底。
/// </summary>
public interface IBackChannelLogoutNotifier
{
    Task NotifyAsync(string sid, Guid userId, IReadOnlyList<string> clientIds, CancellationToken cancellationToken);
}

/// <summary>step-up 通道。password 提升 L2，其余为强认证提升 L3。</summary>
public static class StepUpChannels
{
    public const string Password = "password";
    public const string EmailOtp = "email_otp";
    public const string Sms = "sms";
    public const string Totp = "totp";

    public static bool IsStrongAuth(string channel) =>
        !string.Equals(channel, Password, StringComparison.OrdinalIgnoreCase);
}

public static class SessionLoginMethods
{
    public const string Password = "pwd";
    public const string Otp = "otp";
    public const string Sso = "sso";
}

/// <summary>
/// 标记控制器或 Action 需要的最低会话等级。
/// 等级是会话的动态状态，不是授予给用户的权限，因此刻意不复用 AuthorizeAttribute/角色策略：
/// 角色由管理员分配且不过期，等级随时间自动衰减。执行由 SessionLevelMiddleware 完成。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireSessionLevelAttribute(int level = SessionLevels.L2) : Attribute
{
    public int Level { get; } = SessionLevels.Clamp(level);
}

public static class TrustedDeviceStatus
{
    public const string Trusted = "trusted";
    public const string Revoked = "revoked";
}

/// <summary>deviceInfo 开放 JSON 的展示摘要，只解析已知字段，未知字段保留在原始 JSON 中。</summary>
public sealed record DeviceInfoSummary(
    string? Platform,
    string? ClientKind,
    string? Os,
    string? ClientName,
    string? Model,
    string? Timezone)
{
    public static DeviceInfoSummary? Parse(string? deviceInfoJson)
    {
        if (string.IsNullOrWhiteSpace(deviceInfoJson))
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(deviceInfoJson);
            var root = document.RootElement;
            return new DeviceInfoSummary(
                GetString(root, "platform"),
                GetString(root, "clientKind"),
                GetString(root, "os"),
                GetString(root, "clientName"),
                GetString(root, "model"),
                GetString(root, "timezone"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed record CreateSessionContext(
    Guid UserId,
    string? DeviceId,
    string? DeviceInfoJson,
    string LoginMethod,
    string? IpAddress,
    string? UserAgent,
    DateTime? PasswordVerifiedAtUtc,
    DateTime? StrongAuthAtUtc,
    string? StrongAuthMethod,
    TimeSpan Lifetime);

/// <summary>会话状态快照，用于每请求的吊销与等级判定。</summary>
public sealed record SessionState(
    string Sid,
    Guid UserId,
    string? DeviceId,
    string LoginMethod,
    DateTime? PasswordVerifiedAtUtc,
    DateTime? StrongAuthAtUtc,
    string? StrongAuthMethod,
    DateTime LastActivityAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc);

public sealed record SessionItem(
    string Sid,
    Guid UserId,
    string UserName,
    string DisplayName,
    string? DeviceId,
    DeviceInfoSummary? Device,
    string LoginMethod,
    IReadOnlyList<string> Clients,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAtUtc,
    DateTime LastActivityAtUtc,
    DateTime ExpiresAtUtc,
    bool IsOnline,
    bool IsRevoked,
    DateTime? RevokedAtUtc,
    string? RevokedReason,
    int Level,
    bool IsCurrent);

public sealed record DeviceItem(
    string DeviceId,
    DeviceInfoSummary? Device,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    bool IsOnline,
    bool IsTrusted,
    int ActiveSessionCount,
    IReadOnlyList<SessionItem> Sessions,
    bool IsCurrentDevice);

/// <summary>登录时的设备信任判定结果。</summary>
public sealed record DeviceTrust(bool IsKnown, bool IsTrusted, string Reason)
{
    public static DeviceTrust Unknown { get; } = new(false, false, "device_unknown");
}

/// <summary>428 响应体：告知前端需要哪种额外验证。</summary>
public sealed record StepUpChallenge(
    string StepUpToken,
    IReadOnlyList<string> Channels,
    string? MaskedTarget,
    int ExpiresInSeconds,
    string Reason);

/// <summary>吊销原因，写入会话记录与安全审计。</summary>
public sealed record RevokeRequest(string? Reason);

public sealed record SendStepUpCodeRequest(string StepUpToken, string Channel);
public sealed record VerifyStepUpRequest(
    string StepUpToken,
    string Channel,
    string Credential,
    string Mode = "cookie",
    bool RememberMe = false);

/// <summary>会话内 step-up：channel=password 提升 L2，其余提升 L3。</summary>
public sealed record SessionStepUpRequest(string Channel, string Credential);

/// <summary>当前登录凭证的实时状态，供前端展示等级徽章与设备信息。</summary>
public sealed record CurrentSessionInfo(
    string Sid,
    int Level,
    string Acr,
    string LoginMethod,
    string? DeviceId,
    DeviceInfoSummary? Device,
    DateTime? PasswordVerifiedAtUtc,
    DateTime? StrongAuthAtUtc,
    string? StrongAuthMethod,
    IReadOnlyList<string> Clients,
    DateTime CreatedAtUtc,
    DateTime LastActivityAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>
/// step-up 校验通过的结果。DeviceInfoJson 由创建挑战时随 stepUpToken 一起封存，
/// 因为登录请求体在 428 之后不会重发。token 重发由控制器调用 IAdminAuthenticationService 完成。
/// </summary>
public sealed record StepUpVerification(Guid UserId, string? DeviceId, string Channel, string? DeviceInfoJson);

/// <summary>step-up 流程中的可预期失败，Code 直接作为 HTTP 响应体的 error 字段。</summary>
public sealed class StepUpException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public const string InvalidToken = "invalid_token";
    public const string Expired = "expired";
    public const string Consumed = "consumed";
    public const string ChannelUnavailable = "channel_unavailable";
    public const string NeedBindEmail = "need_bind_email";
    public const string RateLimited = "rate_limited";
    public const string InvalidCode = "invalid_code";
    public const string InvalidPassword = "invalid_password";
    public const string TooManyAttempts = "too_many_attempts";
}

public interface ISessionService
{
    Task<string> CreateAsync(CreateSessionContext context, CancellationToken cancellationToken);
    Task<SessionState?> GetStateAsync(string? sid, CancellationToken cancellationToken);

    /// <summary>当前会话的实时状态；sid 缺失、已吊销或已过期时返回 null。</summary>
    Task<CurrentSessionInfo?> GetCurrentAsync(string? sid, CancellationToken cancellationToken);
    Task<bool> IsRevokedAsync(string? sid, CancellationToken cancellationToken);

    /// <summary>当前等级；sid 为空或已吊销时返回 0。</summary>
    Task<int> GetLevelAsync(string? sid, CancellationToken cancellationToken);

    /// <summary>
    /// 每请求热路径：一次读取完成"是否有效 + 当前等级 + 更新活跃时间"。
    /// 返回 null 表示 sid 缺失、不存在、已吊销或已过期（应拒绝请求）。
    /// </summary>
    Task<int?> ValidateAndTouchAsync(string? sid, CancellationToken cancellationToken);

    /// <summary>更新活跃时间（服务端节流）。若活跃间隔超过 L2 窗口，粘性清除密码核验事实。</summary>
    Task TouchAsync(string? sid, CancellationToken cancellationToken);

    Task RevokeAsync(string sid, string reason, CancellationToken cancellationToken);
    Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken);
    Task<int> RevokeAllForDeviceAsync(Guid userId, string deviceId, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionItem>> ListAsync(Guid? userId, bool includeRevoked, string? currentSid, CancellationToken cancellationToken);
    Task RecordClientAsync(string sid, string clientId, CancellationToken cancellationToken);
    Task GrantPasswordVerifiedAsync(string sid, CancellationToken cancellationToken);
    Task GrantStrongAuthAsync(string sid, string method, CancellationToken cancellationToken);
}

public interface IDeviceTrustService
{
    Task<DeviceTrust> EvaluateAsync(Guid userId, string? deviceId, CancellationToken cancellationToken);
    Task MarkTrustedAsync(Guid userId, string deviceId, string? deviceInfoJson, string? sid, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceItem>> ListDevicesAsync(Guid userId, string? currentDeviceId, string? currentSid, CancellationToken cancellationToken);
    Task RevokeDeviceAsync(Guid userId, string deviceId, string reason, CancellationToken cancellationToken);
}

public interface IStepUpService
{
    /// <summary>密码校验通过但设备不受信时创建挑战。</summary>
    Task<StepUpChallenge> CreateLoginChallengeAsync(
        Guid userId, string? deviceId, string? deviceInfoJson, string reason, CancellationToken cancellationToken);

    Task SendCodeAsync(string stepUpToken, string channel, CancellationToken cancellationToken);

    /// <summary>校验登录挑战，成功后该 stepUpToken 立即失效（一次性）。</summary>
    Task<StepUpVerification> VerifyLoginAsync(string stepUpToken, string channel, string credential, CancellationToken cancellationToken);

    /// <summary>为已登录会话发送强认证验证码（会话内提升到 L3）。</summary>
    Task<StepUpChallenge> SendSessionCodeAsync(string sid, string channel, CancellationToken cancellationToken);

    /// <summary>会话内 step-up：password 通道核验密码并提升 L2，强认证通道提升 L3。</summary>
    Task<StepUpVerification> VerifyInSessionAsync(string sid, SessionStepUpRequest request, CancellationToken cancellationToken);
}
