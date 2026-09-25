namespace OidcServer.App;

/// <summary>
/// OIDC 客户端配置。
/// <paramref name="BackChannelLogoutUri"/> 为 OIDC Back-Channel Logout 1.0 的接收端点，留空表示不接受推送；
/// <paramref name="RequiredSessionLevel"/> 是 authorize 入口的最低会话等级闸门（1/2/3）。
/// </summary>
public sealed record OidcClientItem(
    Guid Id,
    string ClientId,
    string DisplayName,
    string ClientType,
    string ConsentType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> Scopes,
    bool HasClientSecret,
    string? BackChannelLogoutUri,
    int RequiredSessionLevel,
    DateTime CreatedAtUtc);

public sealed record CreateOidcClientRequest(
    string ClientId,
    string DisplayName,
    string ClientType,
    string ConsentType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> Scopes,
    string? ClientSecret,
    string? BackChannelLogoutUri = null,
    int RequiredSessionLevel = 1);

public sealed record UpdateOidcClientRequest(
    string DisplayName,
    string ConsentType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> Scopes,
    string? BackChannelLogoutUri = null,
    int RequiredSessionLevel = 1);

public sealed record OidcScopeItem(
    Guid Id,
    string Name,
    string DisplayName,
    string Description);

public sealed record CreateOidcScopeRequest(
    string Name,
    string DisplayName,
    string Description);

public interface IOidcClientService
{
    Task<IReadOnlyList<OidcClientItem>> ListAsync(CancellationToken cancellationToken);
    Task<OidcClientItem?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<OidcClientItem> CreateAsync(CreateOidcClientRequest request, CancellationToken cancellationToken);
    Task<OidcClientItem> UpdateAsync(Guid id, UpdateOidcClientRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<string> RegenerateSecretAsync(Guid id, CancellationToken cancellationToken);
}

public interface IOidcScopeService
{
    Task<IReadOnlyList<OidcScopeItem>> ListAsync(CancellationToken cancellationToken);
    Task<OidcScopeItem> CreateAsync(CreateOidcScopeRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IOidcClientStore
{
    Task<IReadOnlyList<OidcClientItem>> ListAsync(CancellationToken cancellationToken);
    Task<OidcClientItem?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<OidcClientItem> CreateAsync(CreateOidcClientRequest request, CancellationToken cancellationToken);
    Task<OidcClientItem> UpdateAsync(Guid id, UpdateOidcClientRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<string> RegenerateSecretAsync(Guid id, CancellationToken cancellationToken);
}

public interface IOidcScopeStore
{
    Task<IReadOnlyList<OidcScopeItem>> ListAsync(CancellationToken cancellationToken);
    Task<OidcScopeItem> CreateAsync(CreateOidcScopeRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
