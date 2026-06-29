public sealed class RoomMatchingStatisticsOptions
{
    public const string SectionName = "RoomMatchingStatistics";

    public string Cron { get; set; } = "0 */10 * * * *";

    public bool RunImmediatelyOnStartup { get; set; } = true;

    public int ExpirationWindowDays { get; set; } = 7;

    public int ErrorRetryDelaySeconds { get; set; } = 30;
}
