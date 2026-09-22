using System.Text;
using System.Security.Claims;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OidcServer.Infra;

namespace AdminIdentity.Infra;

public static class ServiceCollectionExtensions
{
    public const string SmartScheme = "AdminSmart";

    public static IServiceCollection AddAdminIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var identityOptions = configuration.GetSection(AdminIdentityOptions.SectionName).Get<AdminIdentityOptions>() ?? new();
        services.Configure<AdminIdentityOptions>(configuration.GetSection(AdminIdentityOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        var keyDirectory = Path.GetFullPath(identityOptions.DataProtectionKeyDirectory);
        Directory.CreateDirectory(keyDirectory);
        services.AddDataProtection()
            .SetApplicationName("MengtuV.Admin.Host")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

        services.AddIdentityCore<AdminUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AdminRole>()
            .AddEntityFrameworkStores<AdminDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = SmartScheme;
                options.DefaultAuthenticateScheme = SmartScheme;
                options.DefaultChallengeScheme = SmartScheme;
            })
            .AddPolicyScheme(SmartScheme, SmartScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }
                    if (context.Request.Headers.ContainsKey("X-Api-Key"))
                    {
                        return ApiKeyAuthenticationHandler.SchemeName;
                    }
                    return IdentityConstants.ApplicationScheme;
                };
            })
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "MengtuV.Admin.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    var path = context.Request.Path.Value ?? "";
                    if (path.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
                    {
                        var oidcOptions = context.HttpContext.RequestServices
                            .GetRequiredService<IOptions<OidcServerOptions>>().Value;
                        // 登录页与授权端点不同源（前端 5101 / Admin.Host 5100），
                        // 必须给绝对地址，否则前端 window.location.href 会按相对路径跳回自己域名。
                        var request = context.Request;
                        var returnUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
                        var loginUrl = $"{oidcOptions.AdminLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}";
                        context.Response.Redirect(loginUrl);
                        return Task.CompletedTask;
                    }
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var user = userId is null ? null : await userManager.FindByIdAsync(userId);
                    if (user?.IsEnabled == true) return;
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var jwt = identityOptions.Jwt;
                var signingKey = string.IsNullOrWhiteSpace(jwt.SigningKey)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
                        if (user?.IsEnabled != true) context.Fail("The administrator account is disabled.");
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicies.View, policy => policy.RequireRole(AdminRoles.Admin, AdminRoles.Operator, AdminRoles.Viewer))
            .AddPolicy(AdminPolicies.Operate, policy => policy.RequireRole(AdminRoles.Admin, AdminRoles.Operator))
            .AddPolicy(AdminPolicies.ManageTasks, policy => policy.RequireRole(AdminRoles.Admin))
            .AddPolicy(AdminPolicies.ManageUsers, policy => policy.RequireRole(AdminRoles.Admin));

        services.AddScoped<CurrentActorAccessor>();
        services.AddScoped<ICurrentActorAccessor>(provider => provider.GetRequiredService<CurrentActorAccessor>());
        services.AddScoped<AdminIdentityService>();
        services.AddSingleton<RolePermissionCatalog>();
        services.AddSingleton<IRolePermissionCatalog>(provider => provider.GetRequiredService<RolePermissionCatalog>());
        services.AddHostedService<RolePermissionCacheRefresher>();
        services.AddScoped<IAdminAuthenticationService>(provider => provider.GetRequiredService<AdminIdentityService>());
        services.AddScoped<IAdminUserService>(provider => provider.GetRequiredService<AdminIdentityService>());
        services.AddScoped<IAdminAccessManagementService, AdminAccessManagementService>();
        services.AddScoped<IAdminAccessTransferService, AdminAccessTransferService>();
        services.AddScoped<IAdminApiKeyService>(provider => provider.GetRequiredService<AdminIdentityService>());
        services.AddHostedService<AdminIdentityBootstrapService>();
        return services;
    }
}
