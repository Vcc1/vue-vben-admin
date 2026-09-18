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
    string? Email,
    bool IsEnabled,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

public sealed record LoginRequest(
    string UserName,
    string Password,
    string Mode = "cookie",
    bool RememberMe = false);

public sealed record TokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record LoginResult(AdminUserItem User, TokenPair? Tokens);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record CreateAdminUserRequest(
    string UserName,
    string DisplayName,
    string? Email,
    string Password,
    IReadOnlyList<string> Roles);
public sealed record SetUserEnabledRequest(bool IsEnabled);
public sealed record ResetPasswordRequest(string NewPassword);
public sealed record SetUserRolesRequest(IReadOnlyList<string> Roles);
public sealed record CreateApiKeyRequest(Guid UserId, string Name, DateTime? ExpiresAtUtc);
public sealed record CreatedApiKey(Guid Id, string Name, string Prefix, string Secret, DateTime? ExpiresAtUtc);
public sealed record ApiKeyItem(Guid Id, Guid UserId, string Name, string Prefix, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc, DateTime? LastUsedAtUtc, DateTime? RevokedAtUtc);

public interface ICurrentActorAccessor
{
    CurrentActor Current { get; }
}

public interface IAdminAuthenticationService
{
    Task<LoginResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);
    Task<TokenPair?> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task<AdminUserItem?> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserItem>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserItem> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken);
    Task SetEnabledAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);
    Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken cancellationToken);
}

public interface IAdminApiKeyService
{
    Task<IReadOnlyList<ApiKeyItem>> ListAsync(CancellationToken cancellationToken);
    Task<CreatedApiKey> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken);
}
