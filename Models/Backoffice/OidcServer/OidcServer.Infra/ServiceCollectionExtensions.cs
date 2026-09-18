using System.Security.Cryptography.X509Certificates;
using System.Text;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Oidc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OidcServer.App;

namespace OidcServer.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOidcServerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OidcServerOptions>(
            configuration.GetSection(OidcServerOptions.SectionName));

        var oidcOptions = configuration.GetSection(OidcServerOptions.SectionName)
            .Get<OidcServerOptions>() ?? new();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<AdminDbContext>()
                       .ReplaceDefaultEntities<OidcApplication, OidcAuthorization, OidcScope, OidcToken, Guid>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetIntrospectionEndpointUris("/connect/introspect")
                       .SetRevocationEndpointUris("/connect/revoke")
                       .SetEndSessionEndpointUris("/connect/endsession")
                       .SetConfigurationEndpointUris("/.well-known/openid-configuration")
                       .SetJsonWebKeySetEndpointUris("/.well-known/jwks");

                options.AllowAuthorizationCodeFlow()
                       .AllowClientCredentialsFlow()
                       .AllowRefreshTokenFlow()
                       .AllowHybridFlow();

                ConfigureSigningKey(options, oidcOptions.SigningKey);

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough()
                       .EnableStatusCodePagesIntegration();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<OidcServerStore>();
        services.AddScoped<IOidcClientStore>(sp => sp.GetRequiredService<OidcServerStore>());
        services.AddScoped<IOidcScopeStore>(sp => sp.GetRequiredService<OidcServerStore>());

        return services;
    }

    private static void ConfigureSigningKey(
        OpenIddictServerBuilder server,
        OidcServerOptions.SigningKeyOptions keyOptions)
    {
        if (string.Equals(keyOptions.Type, "rsa-pfx", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(keyOptions.PfxPath)
            && File.Exists(keyOptions.PfxPath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                keyOptions.PfxPath, keyOptions.PfxPassword);
            server.AddSigningCertificate(certificate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(keyOptions.Key)
            && Encoding.UTF8.GetByteCount(keyOptions.Key) >= 32)
        {
            server.AddSigningKey(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(keyOptions.Key)));
            return;
        }

        // Development fallback: generate an ephemeral key
        server.AddEphemeralSigningKey();
    }
}
