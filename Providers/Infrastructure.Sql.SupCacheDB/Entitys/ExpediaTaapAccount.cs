using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.SupCacheDB.Entitys;

[Table("ExpediaTaapAccounts")]
public class ExpediaTaapAccount
{
    [Key]
    public int Id { get; set; }

    public int AccountLevelId { get; set; }

    [StringLength(100)]
    public string AccountName { get; set; } = string.Empty;

    [StringLength(200)]
    public string SigninEmailAddress { get; set; } = string.Empty;

    [StringLength(200)]
    public string SigninPassword { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string CookieHeader { get; set; } = string.Empty;

    [StringLength(128)]
    public string Sha256Hash { get; set; } = string.Empty;

    [StringLength(200)]
    public string ClientInfoToken { get; set; } = string.Empty;

    [StringLength(100)]
    public string Duaid { get; set; } = string.Empty;

    public int Eapid { get; set; }

    public DateTimeOffset? LoginStateExpiresAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public int WorkerConcurrency { get; set; } = 1;

    public bool CanRefreshSharedSha256Hash { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(500)]
    public string Remark { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
