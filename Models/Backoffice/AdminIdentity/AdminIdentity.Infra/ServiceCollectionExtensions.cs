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
                options.ExpireTimeSpan = TimeSpan.FromDays(identityOptions.Session.LifetimeDays);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    var path = context.Request.Path.Value ?? "";
                    if (path.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
                    {
                        var oidcOptions = context.HttpContext.RequestServices
                            .GetRequiredService<IOptions<OidcServerOptions>>().Value;
                        // 使用配置的 Issuer 构建 returnUrl，避免反向代理导致的 scheme/host 不正确
                        var request = context.Request;
                        var issuer = oidcOptions.Issuer.TrimEnd('/');
                        var returnUrl = $"{issuer}{request.Path}{request.QueryString}";
                        // 添加 error 参数让前端知道是 OIDC 流程失败，避免无限循环
                        var loginUrl = $"{oidcOptions.AdminLoginUrl}?error=oidc_auth_failed&returnUrl={Uri.EscapeDataString(returnUrl)}";
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
                    var sessionValid = await ValidateSessionAsync(
                        context.HttpContext, context.Principal?.FindFirstValue(SessionClaims.Sid));
                    if (user?.IsEnabled == true && sessionValid) return;
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
                        if (user?.IsEnabled != true)
                        {
                            context.Fail("The administrator account is disabled.");
                            return;
                        }
                        if (!await ValidateSessionAsync(context.HttpContext, context.Principal?.FindFirstValue(SessionClaims.Sid)))
                        {
                            context.Fail("The login session has been revoked.");
                        }
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicies.View, policy => policy.RequireRole(AdminRoles.Admin, AdminRoles.Operator, AdminRoles.Viewer))
            .AddPolicy(AdminPolicies.Operate, policy => policy.RequireRole(AdminRoles.Admin, AdminRoles.Operator))
            .AddPolicy(AdminPolicies.ManageTasks, policy => policy.RequireRole(AdminRoles.Admin))
            .AddPolicy(AdminPolicies.ManageUsers, policy => policy.RequireRole(AdminRoles.Admin))
            .AddPolicy(AdminPolicies.ManageSessions, policy => policy.RequireRole(AdminRoles.Admin));

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

        // Redis 可选：配了连接串就由它承载会话热路径与二次验证状态（跨副本共享、重启不丢）；
        // 没配则退回每请求直查数据库 + 进程内存，功能不受影响，只是多副本下验证码不能跨实例使用。
        if (AdminIdentityRedisContext.IsConfigured(configuration))
        {
            services.AddSingleton<AdminIdentityRedisContext>();
            services.AddSingleton<ISessionStateCache, RedisSessionStateCache>();
            services.AddSingleton<IStepUpStateStore, RedisStepUpStateStore>();
            services.AddSingleton<ICaptchaFailureCounter, RedisCaptchaFailureCounter>();
            services.AddSingleton<ICaptchaTokenStore, RedisCaptchaTokenStore>();
            services.AddSingleton<IImageCaptchaAnswerStore, RedisImageCaptchaAnswerStore>();
        }
        else
        {
            services.AddSingleton<ISessionStateCache, NullSessionStateCache>();
            services.AddSingleton<IStepUpStateStore, MemoryStepUpStateStore>();
            services.AddSingleton<ICaptchaFailureCounter, MemoryCaptchaFailureCounter>();
            services.AddSingleton<ICaptchaTokenStore, MemoryCaptchaTokenStore>();
            services.AddSingleton<IImageCaptchaAnswerStore, MemoryImageCaptchaAnswerStore>();
        }

        services.AddScoped<SessionService>();
        services.AddScoped<ISessionService>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<IDeviceTrustService>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<IStepUpService, StepUpService>();
        services.AddScoped<IOtpDeliveryChannel, EmailOtpDeliveryChannel>();
        services.AddScoped<IBackChannelLogoutNotifier, BackChannelLogoutNotifier>();
        services.AddHttpClient(BackChannelLogoutNotifier.HttpClientName);

        services.AddScoped<CaptchaService>();
        services.AddScoped<ICaptchaService>(provider => provider.GetRequiredService<CaptchaService>());
        services.AddSingleton<CaptchaImageGenerator>();

        services.AddHostedService<AdminIdentityBootstrapService>();
        return services;
    }

    /// <summary>
    /// 校验 sid 是否仍有效，并把本次请求的实时等级缓存到 HttpContext，
    /// 供 <see cref="SessionLevelMiddleware"/> 复用，避免同一请求内二次查库。
    /// sid 为空表示改造前签发的旧凭据，退回原有的账号启用校验。
    /// </summary>
    private static async Task<bool> ValidateSessionAsync(HttpContext httpContext, string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            return true;
        }

        var sessions = httpContext.RequestServices.GetRequiredService<ISessionService>();
        var level = await sessions.ValidateAndTouchAsync(sid, httpContext.RequestAborted);
        SessionContext.Set(httpContext, sid, level);
        return level is not null;
    }
}
