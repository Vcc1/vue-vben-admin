using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Infrastructure.Sql.Admin.Oidc;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OidcServer.App;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using HandleTokenRequestContext = OpenIddict.Server.OpenIddictServerEvents.HandleTokenRequestContext;

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
                       .SetUserInfoEndpointUris("/connect/userinfo")
                       .SetIntrospectionEndpointUris("/connect/introspect")
                       .SetRevocationEndpointUris("/connect/revoke")
                       .SetEndSessionEndpointUris("/connect/endsession")
                       .SetConfigurationEndpointUris("/.well-known/openid-configuration")
                       .SetJsonWebKeySetEndpointUris("/.well-known/jwks");

                options.AllowAuthorizationCodeFlow()
                       .AllowPasswordFlow()
                       .AllowClientCredentialsFlow()
                       .AllowRefreshTokenFlow()
                       .AllowHybridFlow();

                var lifetime = oidcOptions.TokenLifetime;
                options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(lifetime.AuthorizationCodeMinutes));
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(lifetime.AccessTokenMinutes));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(lifetime.RefreshTokenDays));

                ConfigureSigningKey(options, oidcOptions.SigningKey);
                ConfigureEncryptionKey(options, oidcOptions.EncryptionKey);

                // 资源服务（如 PayCardHub.Api）通过 JWKS 本地验签，access token 必须为签名 JWT 而非加密 JWE。
                options.DisableAccessTokenEncryption();

                options.AddEventHandler<HandleTokenRequestContext>(builder =>
                    builder.UseInlineHandler(async context =>
                    {
                        var request = context.Request;
                        if (request is null)
                        {
                            context.Reject(
                                error: Errors.InvalidRequest,
                                description: "The OpenID Connect request cannot be processed.");
                            return;
                        }

                        if (request.IsAuthorizationCodeGrantType())
                        {
                            var principal = context.Principal;
                            if (principal is null || !principal.Identity!.IsAuthenticated)
                            {
                                context.Reject(
                                    error: Errors.InvalidGrant,
                                    description: "The authorization code is invalid.");
                                return;
                            }
                            context.SignIn(principal);
                        }
                        else if (request.IsRefreshTokenGrantType())
                        {
                            var principal = context.Principal;
                            if (principal is null || !principal.Identity!.IsAuthenticated)
                            {
                                context.Reject(
                                    error: Errors.InvalidGrant,
                                    description: "The refresh token is invalid.");
                                return;
                            }
                            context.SignIn(principal);
                        }
                        else if (request.IsPasswordGrantType())
                        {
                            var httpContext = context.Transaction.GetHttpRequest()!.HttpContext;
                            var userManager = httpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
                            var signInManager = httpContext.RequestServices.GetRequiredService<SignInManager<AdminUser>>();

                            var user = await userManager.FindByNameAsync(request.Username!);
                            if (user is null || !user.IsEnabled)
                            {
                                context.Reject(
                                    error: Errors.InvalidGrant,
                                    description: "The username or password is incorrect.");
                                return;
                            }

                            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
                            if (!result.Succeeded)
                            {
                                context.Reject(
                                    error: Errors.InvalidGrant,
                                    description: "The username or password is incorrect.");
                                return;
                            }

                            var identity = new ClaimsIdentity(
                                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                                Claims.Name, Claims.Role);

                            identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
                            identity.AddClaim(new Claim(Claims.Name, user.UserName!));
                            identity.AddClaim(new Claim(Claims.PreferredUsername, user.UserName!));

                            if (!string.IsNullOrEmpty(user.DisplayName))
                            {
                                identity.AddClaim(new Claim("display_name", user.DisplayName));
                            }

                            if (!string.IsNullOrEmpty(user.Email))
                            {
                                identity.AddClaim(new Claim(Claims.Email, user.Email));
                            }

                            var roles = await userManager.GetRolesAsync(user);
                            foreach (var role in roles)
                            {
                                identity.AddClaim(new Claim(Claims.Role, role));
                            }

                            var tokenPrincipal = new ClaimsPrincipal(identity);
                            tokenPrincipal.SetScopes(request.GetScopes());

                            context.SignIn(tokenPrincipal);
                        }
                        else if (request.IsClientCredentialsGrantType())
                        {
                            // client_credentials：无用户，主体代表客户端本身。
                            // 用于服务间调用（如 PayCardHub.Api 后台定时拉取角色→权限映射）。
                            var identity = new ClaimsIdentity(
                                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                                Claims.Name, Claims.Role);

                            identity.AddClaim(new Claim(Claims.Subject, request.ClientId!));
                            identity.AddClaim(new Claim(Claims.Name, request.ClientId!));

                            var tokenPrincipal = new ClaimsPrincipal(identity);
                            tokenPrincipal.SetScopes(request.GetScopes());

                            context.SignIn(tokenPrincipal);
                        }
                        else
                        {
                            context.Reject(
                                error: Errors.UnsupportedGrantType,
                                description: "The specified grant type is not supported.");
                        }
                    }));

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough()
                       .EnableUserInfoEndpointPassthrough()
                       .EnableStatusCodePagesIntegration()
                       .DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<OidcServerStore>();
        services.AddScoped<IOidcClientStore>(sp => sp.GetRequiredService<OidcServerStore>());
        services.AddScoped<IOidcScopeStore>(sp => sp.GetRequiredService<OidcServerStore>());
        services.AddHostedService<OidcServerBootstrapService>();

        return services;
    }

    private static void ConfigureSigningKey(
        OpenIddictServerBuilder server,
        OidcServerOptions.SigningKeyOptions keyOptions)
    {
        if (!string.IsNullOrWhiteSpace(keyOptions.PfxBase64))
        {
            var pfxBytes = Convert.FromBase64String(keyOptions.PfxBase64);
            var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, keyOptions.PfxPassword);
            server.AddSigningCertificate(certificate);
            return;
        }

        if (string.Equals(keyOptions.Type, "rsa-pfx", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(keyOptions.PfxPath)
            && File.Exists(keyOptions.PfxPath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                keyOptions.PfxPath, keyOptions.PfxPassword);
            server.AddSigningCertificate(certificate);
            return;
        }

        // Development fallback: generate an ephemeral key
        server.AddEphemeralSigningKey();
    }

    private static void ConfigureEncryptionKey(
        OpenIddictServerBuilder server,
        OidcServerOptions.EncryptionKeyOptions keyOptions)
    {
        if (!string.IsNullOrWhiteSpace(keyOptions.PfxBase64))
        {
            var pfxBytes = Convert.FromBase64String(keyOptions.PfxBase64);
            var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, keyOptions.PfxPassword);
            server.AddEncryptionCertificate(certificate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(keyOptions.PfxPath)
            && File.Exists(keyOptions.PfxPath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                keyOptions.PfxPath, keyOptions.PfxPassword);
            server.AddEncryptionCertificate(certificate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(keyOptions.Key)
            && Encoding.UTF8.GetByteCount(keyOptions.Key) >= 32)
        {
            server.AddEncryptionKey(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(keyOptions.Key)));
            return;
        }

        // Development fallback: generate an ephemeral key
        server.AddEphemeralEncryptionKey();
    }
}
