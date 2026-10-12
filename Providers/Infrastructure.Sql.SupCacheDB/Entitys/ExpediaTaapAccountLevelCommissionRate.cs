using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.SupCacheDB.Entitys;

[Table("ExpediaTaapAccountLevelCommissionRates")]
public class ExpediaTaapAccountLevelCommissionRate
{
    [Key]
    public int Id { get; set; }

    public int AccountLevelId { get; set; }

    [StringLength(50)]
    public string ProductCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,4)")]
    public decimal CommissionRate { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(500)]
    public string Remark { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
