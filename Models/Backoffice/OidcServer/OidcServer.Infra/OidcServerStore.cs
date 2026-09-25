using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Oidc;
using Microsoft.EntityFrameworkCore;
using OidcServer.App;

namespace OidcServer.Infra;

internal sealed class OidcServerStore(AdminDbContext dbContext) : IOidcClientStore, IOidcScopeStore
{
    public async Task<IReadOnlyList<OidcClientItem>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.OidcApplications
            .OrderBy(x => x.DisplayName)
            .Select(x => new OidcClientItem(
                x.Id,
                x.ClientId ?? string.Empty,
                x.DisplayName ?? x.ClientId ?? string.Empty,
                x.ClientType ?? string.Empty,
                x.ConsentType ?? string.Empty,
                DeserializeList(x.RedirectUris),
                DeserializeList(x.PostLogoutRedirectUris),
                ExtractGrantTypes(x.Permissions),
                ExtractScopes(x.Permissions),
                !string.IsNullOrEmpty(x.ClientSecret),
                x.BackChannelLogoutUri,
                x.RequiredSessionLevel,
                DateTime.UtcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<OidcClientItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var app = await dbContext.OidcApplications.FindAsync([id], cancellationToken);
        if (app is null) return null;

        return new OidcClientItem(
            app.Id,
            app.ClientId ?? string.Empty,
            app.DisplayName ?? app.ClientId ?? string.Empty,
            app.ClientType ?? string.Empty,
            app.ConsentType ?? string.Empty,
            DeserializeList(app.RedirectUris),
            DeserializeList(app.PostLogoutRedirectUris),
            ExtractGrantTypes(app.Permissions),
            ExtractScopes(app.Permissions),
            !string.IsNullOrEmpty(app.ClientSecret),
            app.BackChannelLogoutUri,
            app.RequiredSessionLevel,
            DateTime.UtcNow);
    }

    public async Task<OidcClientItem> CreateAsync(CreateOidcClientRequest request, CancellationToken cancellationToken)
    {
        var permissions = BuildPermissions(request.GrantTypes, request.Scopes);

        var app = new OidcApplication
        {
            ClientId = request.ClientId.Trim(),
            DisplayName = request.DisplayName.Trim(),
            ClientType = request.ClientType,
            ConsentType = request.ConsentType,
            RedirectUris = SerializeList(request.RedirectUris),
            PostLogoutRedirectUris = SerializeList(request.PostLogoutRedirectUris),
            Permissions = SerializeList(permissions),
            Requirements = "[]",
            Properties = "{}",
            BackChannelLogoutUri = NormalizeLogoutUri(request.BackChannelLogoutUri),
            RequiredSessionLevel = NormalizeLevel(request.RequiredSessionLevel)
        };

        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            app.ClientSecret = HashClientSecret(request.ClientSecret);
        }

        dbContext.OidcApplications.Add(app);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToItem(app, request.RedirectUris, request.PostLogoutRedirectUris, request.GrantTypes, request.Scopes);
    }

    public async Task<OidcClientItem> UpdateAsync(Guid id, UpdateOidcClientRequest request, CancellationToken cancellationToken)
    {
        var app = await dbContext.OidcApplications.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"OIDC client {id} not found.");

        var permissions = BuildPermissions(request.GrantTypes, request.Scopes);

        app.DisplayName = request.DisplayName.Trim();
        app.ConsentType = request.ConsentType;
        app.RedirectUris = SerializeList(request.RedirectUris);
        app.PostLogoutRedirectUris = SerializeList(request.PostLogoutRedirectUris);
        app.Permissions = SerializeList(permissions);
        app.BackChannelLogoutUri = NormalizeLogoutUri(request.BackChannelLogoutUri);
        app.RequiredSessionLevel = NormalizeLevel(request.RequiredSessionLevel);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToItem(app, request.RedirectUris, request.PostLogoutRedirectUris, request.GrantTypes, request.Scopes);
    }

    private static OidcClientItem ToItem(
        OidcApplication app,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        IReadOnlyList<string> grantTypes,
        IReadOnlyList<string> scopes) => new(
            app.Id, app.ClientId ?? string.Empty, app.DisplayName ?? app.ClientId ?? string.Empty,
            app.ClientType ?? string.Empty, app.ConsentType ?? string.Empty,
            redirectUris, postLogoutRedirectUris, grantTypes, scopes,
            !string.IsNullOrEmpty(app.ClientSecret),
            app.BackChannelLogoutUri,
            app.RequiredSessionLevel,
            DateTime.UtcNow);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var app = await dbContext.OidcApplications.FindAsync([id], cancellationToken);
        if (app is null) return;

        dbContext.OidcApplications.Remove(app);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> RegenerateSecretAsync(Guid id, CancellationToken cancellationToken)
    {
        var app = await dbContext.OidcApplications.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"OIDC client {id} not found.");

        var rawSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        app.ClientSecret = HashClientSecret(rawSecret);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rawSecret;
    }

    // --- Scope operations ---

    async Task<IReadOnlyList<OidcScopeItem>> IOidcScopeStore.ListAsync(CancellationToken cancellationToken)
    {
        var items = await dbContext.OidcScopes
            .OrderBy(x => x.Name)
            .Select(x => new OidcScopeItem(x.Id, x.Name ?? string.Empty, x.DisplayName ?? x.Name ?? string.Empty, x.Description ?? string.Empty))
            .ToListAsync(cancellationToken);
        return items;
    }

    async Task<OidcScopeItem> IOidcScopeStore.CreateAsync(CreateOidcScopeRequest request, CancellationToken cancellationToken)
    {
        var scope = new OidcScope
        {
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim(),
            Resources = "[]",
            Properties = "{}"
        };

        dbContext.OidcScopes.Add(scope);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new OidcScopeItem(scope.Id, scope.Name ?? string.Empty, scope.DisplayName ?? scope.Name ?? string.Empty, scope.Description ?? string.Empty);
    }

    async Task IOidcScopeStore.DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var scope = await dbContext.OidcScopes.FindAsync([id], cancellationToken);
        if (scope is null) return;

        dbContext.OidcScopes.Remove(scope);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // --- Helpers ---

    private static string SerializeList(IReadOnlyList<string> items) =>
        JsonSerializer.Serialize(items);

    /// <summary>
    /// logout_token 会带上 sid 与 sub，推到攻击者控制的地址等于泄露会话标识，
    /// 因此只接受绝对 http/https 地址；生产为内网 HTTP，由运维在后台逐项确认后录入。
    /// </summary>
    private static string? NormalizeLogoutUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var trimmed = uri.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Back-channel logout URI 必须是绝对的 http/https 地址。", nameof(uri));
        }

        if (trimmed.Length > 400)
        {
            throw new ArgumentException("Back-channel logout URI 过长。", nameof(uri));
        }

        return trimmed;
    }

    private static int NormalizeLevel(int level) => level <= 0 ? SessionLevels.L1 : SessionLevels.Clamp(level);

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Build OpenIddict permissions from grant types and scopes.
    /// OpenIddict uses a unified permissions model:
    ///   Grant types: "ept:token", "ept:authorize", etc.
    ///   Scopes: "scope:name"
    /// </summary>
    private static IReadOnlyList<string> BuildPermissions(
        IReadOnlyList<string> grantTypes, IReadOnlyList<string> scopes)
    {
        var permissions = new List<string>();

        // Map grant types to endpoint permissions.
        foreach (var gt in grantTypes)
        {
            switch (gt)
            {
                case "authorization_code":
                    permissions.Add("ept:authorization");
                    permissions.Add("gt:authorization_code");
                    break;
                case "client_credentials":
                    permissions.Add("ept:token");
                    permissions.Add("gt:client_credentials");
                    break;
                case "password":
                    permissions.Add("ept:token");
                    permissions.Add("gt:password");
                    break;
                case "refresh_token":
                    permissions.Add("ept:token");
                    permissions.Add("gt:refresh_token");
                    break;
            }
        }

        // Add scope permissions.
        foreach (var scope in scopes)
        {
            permissions.Add($"scp:{scope}");
        }

        return permissions.Distinct().ToList();
    }

    private static IReadOnlyList<string> ExtractGrantTypes(string? permissionsJson)
    {
        var permissions = DeserializeList(permissionsJson);
        return permissions
            .Where(p => p.StartsWith("gt:", StringComparison.Ordinal))
            .Select(p => p["gt:".Length..])
            .ToList();
    }

    private static IReadOnlyList<string> ExtractScopes(string? permissionsJson)
    {
        var permissions = DeserializeList(permissionsJson);
        return permissions
            .Where(p => p.StartsWith("scp:", StringComparison.Ordinal))
            .Select(p => p["scp:".Length..])
            .ToList();
    }

    private static string HashClientSecret(string secret)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
