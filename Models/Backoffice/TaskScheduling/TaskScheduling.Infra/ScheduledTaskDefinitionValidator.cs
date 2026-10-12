using System.Text.Json;
using Microsoft.Extensions.Options;
using Quartz;
using TaskScheduling.App;

namespace TaskScheduling.Infra;

internal sealed class ScheduledTaskDefinitionValidator(
    TaskCatalogProvider catalog,
    IOptions<TaskSchedulingOptions> options) : IScheduledTaskDefinitionValidator
{
    public void Validate(ScheduledTaskInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 200)
            throw new ArgumentException("Task name is required and cannot exceed 200 characters.");
        if (!ScheduledTaskTypes.All.Contains(input.TaskType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown task type '{input.TaskType}'.");
        if (!CronExpression.IsValidExpression(input.CronExpression))
            throw new ArgumentException("Cron expression is invalid.");
        _ = TaskSchedulingTimeZone.Resolve(input.TimeZoneId);
        if (input.MisfirePolicy is not ("DoNothing" or "FireOnce"))
            throw new ArgumentException("MisfirePolicy must be DoNothing or FireOnce.");
        if (input.TimeoutSeconds is < 1 or > 86400)
            throw new ArgumentException("TimeoutSeconds must be between 1 and 86400.");
        if (input.RetryCount is < 0 or > 20)
            throw new ArgumentException("RetryCount must be between 0 and 20.");
        if (input.RetryIntervalSeconds is < 1 or > 86400)
            throw new ArgumentException("RetryIntervalSeconds must be between 1 and 86400.");

        switch (input.TaskType)
        {
            case ScheduledTaskTypes.Process:
                ValidateProcess(Deserialize<ProcessTaskConfiguration>(input.Configuration));
                break;
            case ScheduledTaskTypes.Sql:
                ValidateSql(Deserialize<SqlTaskConfiguration>(input.Configuration));
                break;
            case ScheduledTaskTypes.Program:
                _ = catalog.GetHandler(Deserialize<ProgramTaskConfiguration>(input.Configuration).HandlerKey);
                break;
        }
    }

    private void ValidateProcess(ProcessTaskConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Executable))
            throw new ArgumentException("Process executable is required.");
        if (!options.Value.Process.AllowedExecutables.Any(x =>
                string.Equals(x, config.Executable, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Executable '{config.Executable}' is not in the process allowlist.");

        var directory = Path.GetFullPath(config.WorkingDirectory);
        if (!options.Value.Process.AllowedWorkingDirectories.Any(root => IsWithin(directory, Path.GetFullPath(root))))
            throw new ArgumentException($"Working directory '{config.WorkingDirectory}' is not in the process allowlist.");
        if (config.Shell is not ("None" or "Cmd" or "PowerShell" or "Bash"))
            throw new ArgumentException("Shell must be None, Cmd, PowerShell, or Bash.");
    }

    private void ValidateSql(SqlTaskConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.CommandText))
            throw new ArgumentException("SQL command text is required.");
        var database = catalog.GetDatabase(config.DatabaseKey);
        if (!database.HasExecutionConnection)
            throw new InvalidOperationException(
                $"SQL target '{database.Key}' requires connection string '{database.ExecutionConnectionStringName}'.");
    }

    internal static T Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(JsonDefaults.Options)
        ?? throw new ArgumentException($"Invalid {typeof(T).Name} configuration.");

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
