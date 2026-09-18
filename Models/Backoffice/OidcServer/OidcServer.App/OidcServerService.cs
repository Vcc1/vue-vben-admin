using AdminIdentity.App;

namespace OidcServer.App;

public sealed class OidcServerService(
    IOidcClientStore clientStore,
    IOidcScopeStore scopeStore,
    ICurrentActorAccessor actorAccessor) : IOidcClientService, IOidcScopeService
{
    public Task<IReadOnlyList<OidcClientItem>> ListAsync(CancellationToken cancellationToken) =>
        clientStore.ListAsync(cancellationToken);

    public Task<OidcClientItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        clientStore.GetAsync(id, cancellationToken);

    public Task<OidcClientItem> CreateAsync(CreateOidcClientRequest request, CancellationToken cancellationToken) =>
        clientStore.CreateAsync(request, cancellationToken);

    public Task<OidcClientItem> UpdateAsync(Guid id, UpdateOidcClientRequest request, CancellationToken cancellationToken) =>
        clientStore.UpdateAsync(id, request, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        clientStore.DeleteAsync(id, cancellationToken);

    public Task<string> RegenerateSecretAsync(Guid id, CancellationToken cancellationToken) =>
        clientStore.RegenerateSecretAsync(id, cancellationToken);

    Task<IReadOnlyList<OidcScopeItem>> IOidcScopeService.ListAsync(CancellationToken cancellationToken) =>
        scopeStore.ListAsync(cancellationToken);

    Task<OidcScopeItem> IOidcScopeService.CreateAsync(CreateOidcScopeRequest request, CancellationToken cancellationToken) =>
        scopeStore.CreateAsync(request, cancellationToken);

    Task IOidcScopeService.DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        scopeStore.DeleteAsync(id, cancellationToken);
}
