namespace TaskScheduling.Infra;

public sealed class TaskSchedulingOptions
{
    public const string SectionName = "TaskScheduling";

    public string MetadataConnectionStringName { get; set; } = "RoomMapping";
    public string LogDirectory { get; set; } = Path.Combine("data", "scheduler-logs");
    public int LogRetentionDays { get; set; } = 30;
    public long MaxTotalLogBytes { get; set; } = 20L * 1024 * 1024 * 1024;
    public long MaxRunLogBytes { get; set; } = 100L * 1024 * 1024;
    public string[] AllowedDatabaseKeys { get; set; } = [];
    public Dictionary<string, SqlTargetOptions> SqlTargets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ProcessOptions Process { get; set; } = new();

    public sealed class SqlTargetOptions
    {
        public string ExecutionConnectionStringName { get; set; } = string.Empty;
    }

    public sealed class ProcessOptions
    {
        public string[] AllowedExecutables { get; set; } = [];
        public string[] AllowedWorkingDirectories { get; set; } = [];
    }
}
