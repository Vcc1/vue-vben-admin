using System.Text.Json;
using SupRoomInfoIngestion.Contracts;

namespace SupRoomInfoIngestion;

public sealed class BedInfoRestoreStep : IBedInfoRestoreStep
{
    private static readonly Dictionary<int, string> BedTypeNames = new()
    {
        [0] = "Unknow",
        [1] = "Semi_double",
        [2] = "Spacecapsule",
        [3] = "Sofabed",
        [4] = "Bunkbeds",
        [5] = "Single",
        [6] = "Double",
        [7] = "Queen",
        [8] = "King",
        [9] = "Lowerberth",
        [10] = "Roundbed",
        [11] = "Tatami",
        [12] = "Passshop",
        [13] = "Upperberth",
        [14] = "Rollawaybed",
        [15] = "Squarebed",
        [16] = "Capsule",
        [17] = "Waterbed",
        [18] = "Futon",
        [19] = "Twinbed",
        [20] = "Kang"
    };

    public string Restore(string? bedInfoKey)
    {
        if (string.IsNullOrWhiteSpace(bedInfoKey))
        {
            return "[]";
        }

        var bedGroups = bedInfoKey
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(group => new BedInfoGroup
            {
                Beds = group
                    .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(ParseBed)
                    .Where(bed => bed is not null)
                    .Cast<BedInfoItem>()
                    .OrderBy(bed => bed.MTBedId)
                    .ToList()
            })
            .Where(group => group.Beds.Count > 0)
            .ToList();

        return JsonSerializer.Serialize(bedGroups);
    }

    private static BedInfoItem? ParseBed(string bedSegment)
    {
        var parts = bedSegment.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var bedType) || !int.TryParse(parts[1], out var num))
        {
            return null;
        }

        if (!BedTypeNames.TryGetValue(bedType, out var description))
        {
            throw new InvalidOperationException($"未知床型类型: {bedType}");
        }

        return new BedInfoItem
        {
            MTBedId = bedType,
            Description = description,
            Num = num
        };
    }

    private sealed class BedInfoGroup
    {
        public List<BedInfoItem> Beds { get; set; } = [];
    }

    private sealed class BedInfoItem
    {
        public int MTBedId { get; set; }

        public string Description { get; set; } = string.Empty;

        public int Num { get; set; }
    }
}
