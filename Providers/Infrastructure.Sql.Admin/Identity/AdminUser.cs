using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Sql.Admin.Identity;

public sealed class AdminUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}
