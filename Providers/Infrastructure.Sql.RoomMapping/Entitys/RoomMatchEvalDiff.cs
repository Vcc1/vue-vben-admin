using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys;

[Table("RoomMatchEvalDiff")]
public class RoomMatchEvalDiff
{
    [Key]
    public long Id { get; set; }

    public long RunId { get; set; }
    public int SupRoomId { get; set; }

    [Required, MaxLength(50)]
    public string MTHotelCode { get; set; } = string.Empty;

    public int SupId { get; set; }

    [Required, MaxLength(32)]
    public string DiffType { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Category { get; set; }

    public int? OldOTARoomId { get; set; }
    public int? NewOTARoomId { get; set; }
    public int? OldMatchType { get; set; }
    public int? NewMatchType { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? OldMatchScore { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? NewMatchScore { get; set; }

    [MaxLength(200)]
    public string? SupRoomNameEN { get; set; }

    [MaxLength(200)]
    public string? SupRoomNameCN { get; set; }

    [MaxLength(200)]
    public string? OldOTARoomNameEN { get; set; }

    [MaxLength(200)]
    public string? OldOTARoomNameCN { get; set; }

    [MaxLength(200)]
    public string? NewOTARoomNameEN { get; set; }

    [MaxLength(200)]
    public string? NewOTARoomNameCN { get; set; }

    public string? Detail { get; set; }
}
