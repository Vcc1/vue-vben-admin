namespace QueryPriceSupRoomBuilder;

public static class RoomBuildTaskParser
{
    public static IReadOnlyList<DateWindowRule> ParseDateWindows(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [new DateWindowRule(1, 1, 59)];
        }

        return value
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseRule)
            .ToArray();
    }

    static DateWindowRule ParseRule(string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var nights)
            || !int.TryParse(parts[1], out var from)
            || !int.TryParse(parts[2], out var to)
            || nights <= 0
            || from < 0
            || to < from)
        {
            throw new FormatException($"Invalid date window '{value}'. Expected format is stayNights:fromDayOffset:toDayOffset.");
        }

        return new DateWindowRule(nights, from, to);
    }
}
