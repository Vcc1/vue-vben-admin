namespace OidcServer.App;

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
    string? ClientSecret);

public sealed record UpdateOidcClientRequest(
    string DisplayName,
    string ConsentType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> Scopes);

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
