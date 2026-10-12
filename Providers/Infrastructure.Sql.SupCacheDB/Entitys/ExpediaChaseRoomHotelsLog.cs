using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.SupCacheDB.Entitys;

[Table("ExpediaChaseRoomHotelsLog")]
public class ExpediaChaseRoomHotelsLog
{
    [Key]
    public int Id { get; set; }

    public int HotelTaskId { get; set; }

    public DateTime LogTime { get; set; }

    [StringLength(100)]
    public string MTHotelCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string PropertyId { get; set; } = string.Empty;

    public int Rooms { get; set; }

    public int Adults { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public int Priority { get; set; }

    [StringLength(100)]
    public string CompletedBy { get; set; } = string.Empty;

    public bool? IsTherePrice { get; set; }
}