using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Infrastructure.Sql.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdminIdentity.Infra;

internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AdminDbContext dbContext,
    UserManager<Infrastructure.Sql.Admin.Identity.AdminUser> userManager,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return AuthenticateResult.NoResult();
        }

        var secret = values.ToString().Trim();
        var separator = secret.IndexOf('.');
        if (separator <= 0)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var prefix = secret[..separator];
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var key = await dbContext.ApiKeys.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Prefix == prefix, Context.RequestAborted);
        if (key is null || key.RevokedAtUtc is not null || key.ExpiresAtUtc <= now || !key.User.IsEnabled)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var expectedHash = Convert.FromHexString(key.SecretHash);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var roles = await userManager.GetRolesAsync(key.User);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.UserId.ToString()),
            new(ClaimTypes.Name, key.User.UserName ?? key.UserId.ToString()),
            new("display_name", key.User.DisplayName),
            new("api_key_id", key.Id.ToString()),
            new("api_key_name", key.Name)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        key.LastUsedAtUtc = now;
        await dbContext.SaveChangesAsync(Context.RequestAborted);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
