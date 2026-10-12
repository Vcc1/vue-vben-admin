using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Notification.App;

namespace Notification.Infra;

internal sealed class CurrentUserIdAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserIdAccessor
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("No authenticated user.");
            }

            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("NameIdentifier claim not found.");
            return Guid.Parse(id);
        }
    }
}
