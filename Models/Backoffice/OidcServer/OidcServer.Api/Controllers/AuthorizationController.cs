using System.Security.Claims;
using AdminIdentity.App;
using Infrastructure.Sql.Admin.Identity;
using Infrastructure.Sql.Admin.Oidc;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcServer.Api.Controllers;

public sealed class AuthorizationController(
    UserManager<AdminUser> userManager,
    ISessionService sessions,
    IOpenIddictApplicationManager applicationManager,
    IConfiguration configuration) : Controller
{
    private const string OidcServerOptionsSection = "OidcServer";

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> HandleAuthorizeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Retrieve the user principal from the authentication middleware.
        var result = await HttpContext.AuthenticateAsync();
        var principal = result.Principal;

        // If the user is not authenticated, redirect to the admin login page.
        if (principal is null || !principal.Identity?.IsAuthenticated == true)
        {
            return RedirectToLogin();
        }

        var sid = principal.FindFirstValue(SessionClaims.Sid);
        if (sid is null)
        {
            // 改造前签发的 cookie 没有 sid，无法参与吊销与等级治理：强制重新登录换取带 sid 的凭据。
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return RedirectToLogin();
        }

        // 等级闸门只在 authorize 入口校验一次；token 生命周期内等级自然衰减由资源服务自行判断。
        var level = await sessions.GetLevelAsync(sid, cancellationToken);
        var required = await ResolveRequiredLevelAsync(request, cancellationToken);
        if (required > level)
        {
            return RedirectToStepUp(required);
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(Claims.Subject)
            ?? string.Empty;

        var identity = new ClaimsIdentity(
            principal.Identity!.AuthenticationType,
            Claims.Name, Claims.Role);

        var claimTypeMap = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = Claims.Subject,
            [ClaimTypes.Name] = Claims.PreferredUsername,
            [ClaimTypes.Email] = Claims.Email,
            [ClaimTypes.Role] = Claims.Role,
            [ClaimTypes.GivenName] = Claims.GivenName,
        };

        foreach (var claim in principal.Claims)
        {
            if (claimTypeMap.TryGetValue(claim.Type, out var mappedType))
            {
                identity.AddClaim(new Claim(mappedType, claim.Value));
            }
            else if (claim.Type is "display_name" or SessionClaims.Amr)
            {
                identity.AddClaim(new Claim(claim.Type, claim.Value));
            }
        }

        if (!identity.HasClaim(c => c.Type == Claims.Subject))
        {
            identity.AddClaim(new Claim(Claims.Subject, userId));
        }

        // sid/acr/amr/auth_time 都是标准 OIDC claim，第三方可以直接消费。
        // pva/saa/device_id 是自有内部事实，不外泄到第三方 token。
        identity.AddClaim(new Claim(SessionClaims.Sid, sid));
        identity.AddClaim(new Claim(SessionClaims.Acr, SessionLevels.ToAcr(level)));

        var authTimeValue = principal.FindFirstValue(SessionClaims.AuthTime);
        if (!long.TryParse(authTimeValue, out _))
        {
            authTimeValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        }
        identity.AddClaim(new Claim(SessionClaims.AuthTime, authTimeValue, ClaimValueTypes.Integer64));

        // Enrich claims from database for claims not present in the cookie.
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                if (!identity.HasClaim(c => c.Type == Claims.Email)
                    && !string.IsNullOrEmpty(user.Email))
                {
                    identity.AddClaim(new Claim(Claims.Email, user.Email));
                }

                if (!identity.HasClaim(c => c.Type == "display_name")
                    && !string.IsNullOrEmpty(user.DisplayName))
                {
                    identity.AddClaim(new Claim("display_name", user.DisplayName));
                }

                // Cookie principal from AddIdentityCore does not carry role claims;
                // load them here so the issued access token includes `role`.
                var roles = await userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    if (!identity.HasClaim(c => c.Type == Claims.Role && c.Value == role))
                    {
                        identity.AddClaim(new Claim(Claims.Role, role));
                    }
                }
            }
        }

        foreach (var scope in request.GetScopes())
        {
            identity.AddClaim(new Claim(Claims.Scope, scope));
        }

        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Set the authorization response properties.
        claimsPrincipal.SetScopes(request.GetScopes());

        // OpenIddict strips principal claims from the access token unless a
        // destination is set. The resource server (PayCardHub.Api) and
        // /connect/userinfo both read `role` from the access token, so expose
        // every claim except scope (OpenIddict manages scope separately).
        foreach (var claim in claimsPrincipal.Claims)
        {
            if (claim.Type == Claims.Scope)
            {
                continue;
            }

            claim.SetDestinations(Destinations.AccessToken);
        }

        // 记录该 sid 向哪些客户端签发过凭据，吊销时才知道该给谁推 logout_token。
        if (request.ClientId is { } clientId)
        {
            await sessions.RecordClientAsync(sid, clientId, cancellationToken);
        }

        // Return the sign-in result to let OpenIddict issue the authorization code.
        return SignIn(claimsPrincipal, "OpenIddict.Server.AspNetCore");
    }

    /// <summary>
    /// 客户端配置的最低等级与请求参数 acr_values 取较大者。
    /// acr_values 是标准参数；无法识别的值按 L1 处理，因此第三方不传也完全不影响流程。
    /// </summary>
    private async Task<int> ResolveRequiredLevelAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        var required = SessionLevels.L1;

        if (request.ClientId is { } clientId
            && await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is OidcApplication application)
        {
            required = Math.Max(required, SessionLevels.Clamp(application.RequiredSessionLevel));
        }

        foreach (var value in (request.AcrValues ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            required = Math.Max(required, SessionLevels.FromAcr(value));
        }

        return required;
    }

    private IActionResult RedirectToLogin() => Challenge(new AuthenticationProperties
    {
        RedirectUri = Request.Path + Request.QueryString
    });

    /// <summary>
    /// 等级不足时把用户送回登录页做 step-up，完成后回到本授权端点继续签发。
    /// 不在授权端点内嵌验证码表单，避免把自有 UI 塞进标准协议流程。
    /// </summary>
    private IActionResult RedirectToStepUp(int requiredLevel)
    {
        var loginUrl = configuration[$"{OidcServerOptionsSection}:AdminLoginUrl"];
        if (string.IsNullOrWhiteSpace(loginUrl))
        {
            return Forbid();
        }

        var returnUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        return Redirect($"{loginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}&requiredLevel={requiredLevel}");
    }
}
