namespace TaskScheduling.Infra;

internal static class TaskSchedulingTimeZone
{
    public const string DefaultId = "Asia/Shanghai";

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Asia/Shanghai"] = "China Standard Time",
            ["China Standard Time"] = "Asia/Shanghai"
        };

    public static TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new TimeZoneNotFoundException("A task time zone is required.");

        var normalized = id.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch (TimeZoneNotFoundException) when (Aliases.TryGetValue(normalized, out var alias))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(alias);
        }
    }
}
