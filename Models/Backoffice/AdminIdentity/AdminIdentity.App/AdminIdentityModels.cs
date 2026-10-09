namespace AdminIdentity.App;

public static class AdminRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Operator, Viewer];
}

public static class AdminPolicies
{
    public const string View = "Admin.View";
    public const string Operate = "Admin.Operate";
    public const string ManageTasks = "Admin.ManageTasks";
    public const string ManageUsers = "Admin.ManageUsers";
    public const string ManageSessions = "Admin.ManageSessions";
    public const string ManageNotifications = "Admin.ManageNotifications";
}

public sealed record CurrentActor(
    string ActorType,
    string ActorId,
    string ActorName,
    string AuthenticationType,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId)
{
    public static CurrentActor System { get; } = new(
        "System", "system", "system", "System", null, null, null);
}

public sealed record AdminUserItem(
    Guid Id,
    string UserName,
    string DisplayName,
    string Name,
    string? Email,
    string? PhoneNumber,
    bool IsEnabled,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

/// <summary>
/// 登录请求。
/// <paramref name="DeviceId"/> 留空时由服务端从 AS 侧 cookie 读取（首次登录自动下发），
/// 只有原生客户端等拿不到 cookie 的场景才需要显式传值。
/// <paramref name="DeviceInfo"/> 是浏览器上报的展示信息（平台、系统等），仅用于列表页可读性。
/// </summary>
public sealed record LoginRequest(
    string UserName,
    string Password,
    string Mode = "cookie",
    bool RememberMe = false,
    string? DeviceId = null,
    System.Text.Json.JsonElement? DeviceInfo = null,
    string? CaptchaId = null,
    string? CaptchaAnswer = null);

public sealed record TokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

/// <summary>登录结果。StepUp 非空表示密码已通过但设备不受信，需二次验证（HTTP 428）。sid 不下发给前端。</summary>
public sealed record LoginResult(AdminUserItem User, TokenPair? Tokens, StepUpChallenge? StepUp = null);

public sealed record LoginOutcome(LoginResult? Result, LoginFailureReason Reason = LoginFailureReason.None);

public enum LoginFailureReason
{
    None,
    CaptchaRequired,
    CaptchaWrong,
    InvalidCredentials,
    AccountLockedOut
}
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record CreateAdminUserRequest(
    string UserName,
    string DisplayName,
    string Name,
    string? Email,
    string Password,
    IReadOnlyList<string> Roles);
public sealed record SetUserEnabledRequest(bool IsEnabled);
public sealed record ResetPasswordRequest(string NewPassword);
public sealed record SetUserRolesRequest(IReadOnlyList<string> Roles);
public sealed record UpdateProfileRequest(string DisplayName);
public sealed record UpdateUserRequest(string Name, string DisplayName, string? Email);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AdminRoleItem(string Name, bool IsSystem, int UserCount, IReadOnlyList<string> Permissions);
public sealed record CreateAdminRoleRequest(string Name);
public sealed record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);
public sealed record CreateAdminPermissionRequest(string Code, string Name, string? Description, bool IsEnabled = true);
public sealed record UpdateAdminPermissionRequest(string Name, string? Description, bool IsEnabled);
public sealed record CreateApiKeyRequest(Guid UserId, string Name, DateTime? ExpiresAtUtc);
public sealed record CreatedApiKey(Guid Id, string Name, string Prefix, string Secret, DateTime? ExpiresAtUtc);
public sealed record ApiKeyItem(Guid Id, Guid UserId, string Name, string Prefix, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc, DateTime? LastUsedAtUtc, DateTime? RevokedAtUtc);

public interface ICurrentActorAccessor
{
    CurrentActor Current { get; }
}

public interface IAdminAuthenticationService
{
    Task<LoginOutcome> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<TokenPair?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task<AdminUserItem?> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetCurrentUserPermissionsAsync(CancellationToken cancellationToken);

    /// <summary>登出并吊销指定凭证（sid 为空时仅签出 cookie，兼容旧调用）。</summary>
    Task LogoutAsync(string? sid, CancellationToken cancellationToken);

    /// <summary>二次验证通过后完成登录：创建会话、签入 cookie、按 mode 签发 token，并把设备标记为受信。</summary>
    Task<LoginResult> CompleteStepUpLoginAsync(
        StepUpVerification verification, string mode, bool rememberMe, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>会话内 step-up 成功后重发 token，并用最新等级事实刷新 cookie claim。</summary>
    Task<LoginResult?> ReissueAsync(string? sid, string mode, string? ipAddress, CancellationToken cancellationToken);
}

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserItem>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserItem> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken);
    Task SetEnabledAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);
    Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken cancellationToken);
    Task<AdminUserItem> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<AdminUserItem> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

public interface IAdminAccessManagementService
{
    Task<IReadOnlyList<AdminRoleItem>> ListRolesAsync(CancellationToken cancellationToken);
    Task<AdminRoleItem> CreateRoleAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken);
    Task SetRolePermissionsAsync(string roleName, IReadOnlyList<string> permissions, CancellationToken cancellationToken);
    Task DeleteRoleAsync(string roleName, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminPermissionNode>> ListPermissionsAsync(CancellationToken cancellationToken);
    Task<AdminPermissionNode> CreatePermissionAsync(CreateAdminPermissionRequest request, CancellationToken cancellationToken);
    Task<AdminPermissionNode> UpdatePermissionAsync(string code, UpdateAdminPermissionRequest request, CancellationToken cancellationToken);
    Task DeletePermissionAsync(string code, CancellationToken cancellationToken);
}

public interface IAdminApiKeyService
{
    Task<IReadOnlyList<ApiKeyItem>> ListAsync(CancellationToken cancellationToken);
    Task<CreatedApiKey> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken);
}
