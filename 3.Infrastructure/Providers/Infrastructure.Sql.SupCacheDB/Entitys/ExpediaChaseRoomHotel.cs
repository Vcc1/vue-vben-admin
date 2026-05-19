using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.SupCacheDB.Entitys;

[Table("ExpediaChaseRoomHotels")]
public class ExpediaChaseRoomHotel
{
    [Key]
    public int Id { get; set; }

    [StringLength(100)]
    public string PropertyId { get; set; } = string.Empty;

    [StringLength(100)]
    public string MTHotelCode { get; set; } = string.Empty;

    public int Rooms { get; set; }

    public int Adults { get; set; }

    public int Priority { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public DateTime UpdateTime { get; set; }

    public int State { get; set; }

    [StringLength(100)]
    public string CompletedBy { get; set; } = string.Empty;
}