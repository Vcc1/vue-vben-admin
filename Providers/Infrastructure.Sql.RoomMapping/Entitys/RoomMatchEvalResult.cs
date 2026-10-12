using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys;

[Table("RoomMatchEvalResult")]
public class RoomMatchEvalResult
{
    [Key]
    public long Id { get; set; }

    public long RunId { get; set; }

    [Required, MaxLength(50)]
    public string MTHotelCode { get; set; } = string.Empty;

    public int SupId { get; set; }
    public int SupRoomId { get; set; }
    public int? OTARoomId { get; set; }
    public int MatchType { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? MatchScore { get; set; }

    [MaxLength(200)]
    public string? SupRoomNameEN { get; set; }

    [MaxLength(200)]
    public string? SupRoomNameCN { get; set; }

    [MaxLength(200)]
    public string? OTARoomNameEN { get; set; }

    [MaxLength(200)]
    public string? OTARoomNameCN { get; set; }
}
