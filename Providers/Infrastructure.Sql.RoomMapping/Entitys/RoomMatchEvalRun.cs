using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys;

[Table("RoomMatchEvalRun")]
public class RoomMatchEvalRun
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(32)]
    public string Status { get; set; } = "Created";

    public int TotalHotels { get; set; }
    public int TotalSupplierRooms { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int FullMatchCount { get; set; }
    public int TextMatchCount { get; set; }
    public int NotMatchCount { get; set; }
    public int DiffNewMatch { get; set; }
    public int DiffLostMatch { get; set; }
    public int DiffReassigned { get; set; }
    public int DiffStatusUp { get; set; }
    public int DiffStatusDown { get; set; }
    public long? BaselineRunId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
