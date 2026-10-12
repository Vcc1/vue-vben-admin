using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.SupCacheDB.Entitys;

[Table("ExpediaTaapAccountLevels")]
public class ExpediaTaapAccountLevel
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string LevelCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string LevelName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinAnnualGbv { get; set; }

    [StringLength(100)]
    public string GbvRequirementText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(500)]
    public string Remark { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}
