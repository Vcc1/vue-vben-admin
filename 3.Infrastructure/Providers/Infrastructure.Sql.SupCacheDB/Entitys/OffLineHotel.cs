using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.SupCacheDB.Entitys;

[Table("OffLine")]
public class OffLineHotel
{
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    public string MTHotelCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string SHotelCode { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }
}