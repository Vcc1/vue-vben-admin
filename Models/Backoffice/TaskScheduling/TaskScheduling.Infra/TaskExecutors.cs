using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskScheduling.App;
using TaskScheduling.Contracts;

namespace TaskScheduling.Infra;

internal interface IScheduledTaskExecutor
{
    Task<TaskExecutionResult> ExecuteAsync(ScheduledTaskDefinition task, Guid runId, CancellationToken cancellationToken);
}

internal sealed class ScheduledTaskExecutor(
    ProcessTaskExecutor process,
    SqlTaskExecutor sql,
    ProgramTaskExecutor program) : IScheduledTaskExecutor
{
    public Task<TaskExecutionResult> ExecuteAsync(ScheduledTaskDefinition task, Guid runId, CancellationToken cancellationToken) =>
        task.TaskType switch
        {
            ScheduledTaskTypes.Process => process.ExecuteAsync(task, cancellationToken),
            ScheduledTaskTypes.Sql => sql.ExecuteAsync(task, cancellationToken),
            ScheduledTaskTypes.Program => program.ExecuteAsync(task, runId, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown scheduled task type '{task.TaskType}'.")
        };
}

internal sealed class ProcessTaskExecutor(ILogger<ProcessTaskExecutor> logger)
{
    public async Task<TaskExecutionResult> ExecuteAsync(ScheduledTaskDefinition task, CancellationToken cancellationToken)
    {
        var config = ScheduledTaskDefinitionValidator.Deserialize<ProcessTaskConfiguration>(task.Configuration);
        var startInfo = CreateStartInfo(config);
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException($"Unable to start process '{config.Executable}'.");

        logger.LogInformation("Process started. ProcessId={ProcessId} Executable={Executable}", process.Id, config.Executable);
        var stdout = PumpAsync(process.StandardOutput, line => logger.LogInformation("{ProcessOutput}", line), cancellationToken);
        var stderr = PumpAsync(process.StandardError, line => logger.LogWarning("{ProcessError}", line), cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Process exited with code {process.ExitCode}.");
        return new TaskExecutionResult($"Process exited with code {process.ExitCode}.");
    }

    private static ProcessStartInfo CreateStartInfo(ProcessTaskConfiguration config)
    {
        var info = new ProcessStartInfo
        {
            WorkingDirectory = Path.GetFullPath(config.WorkingDirectory),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        switch (config.Shell)
        {
            case "None":
                info.FileName = config.Executable;
                foreach (var argument in config.Arguments) info.ArgumentList.Add(argument);
                break;
            case "Cmd":
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Cmd tasks require Windows.");
                info.FileName = "cmd.exe";
                info.ArgumentList.Add("/d"); info.ArgumentList.Add("/s"); info.ArgumentList.Add("/c");
                info.ArgumentList.Add(BuildShellCommand(config));
                break;
            case "PowerShell":
                info.FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
                info.ArgumentList.Add("-NoProfile"); info.ArgumentList.Add("-NonInteractive"); info.ArgumentList.Add("-Command");
                info.ArgumentList.Add(BuildShellCommand(config));
                break;
            case "Bash":
                if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Bash tasks require Linux.");
                info.FileName = "/bin/bash";
                info.ArgumentList.Add("-lc"); info.ArgumentList.Add(BuildShellCommand(config));
                break;
        }
        if (config.Environment is not null)
            foreach (var pair in config.Environment) info.Environment[pair.Key] = pair.Value;
        return info;
    }

    private static string BuildShellCommand(ProcessTaskConfiguration config) =>
        string.Join(' ', new[] { config.Executable }.Concat(config.Arguments).Select(Quote));

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static async Task PumpAsync(StreamReader reader, Action<string> write, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line) write(line);
    }
}

internal sealed class SqlTaskExecutor(
    TaskCatalogProvider catalog,
    IConfiguration configuration,
    ILogger<SqlTaskExecutor> logger)
{
    public async Task<TaskExecutionResult> ExecuteAsync(ScheduledTaskDefinition task, CancellationToken cancellationToken)
    {
        var config = ScheduledTaskDefinitionValidator.Deserialize<SqlTaskConfiguration>(task.Configuration);
        var database = catalog.GetDatabase(config.DatabaseKey);
        var connectionString = configuration.GetConnectionString(database.ExecutionConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{database.ExecutionConnectionStringName}' was not found.");

        await using DbConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = config.CommandText;
        command.CommandTimeout = Math.Clamp(config.CommandTimeoutSeconds ?? task.TimeoutSeconds, 1, 86400);
        if (config.Parameters is not null)
        {
            foreach (var pair in config.Parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = pair.Key.StartsWith('@') ? pair.Key : $"@{pair.Key}";
                parameter.Value = ConvertParameter(pair.Value) ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        logger.LogInformation("SQL command started. DatabaseKey={DatabaseKey} ParameterCount={ParameterCount}",
            config.DatabaseKey, command.Parameters.Count);
        var stopwatch = Stopwatch.StartNew();
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("SQL command completed. DatabaseKey={DatabaseKey} RowsAffected={RowsAffected} DurationMs={DurationMs}",
            config.DatabaseKey, affected, stopwatch.ElapsedMilliseconds);
        return new TaskExecutionResult($"SQL completed; rows affected: {affected}.");
    }

    private static object? ConvertParameter(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String when value.TryGetDateTime(out var dateTime) => dateTime,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.GetRawText()
    };
}

internal sealed class ProgramTaskExecutor(
    TaskCatalogProvider catalog,
    IServiceProvider serviceProvider,
    ILogger<ProgramTaskExecutor> logger)
{
    public async Task<TaskExecutionResult> ExecuteAsync(ScheduledTaskDefinition task, Guid runId, CancellationToken cancellationToken)
    {
        var config = ScheduledTaskDefinitionValidator.Deserialize<ProgramTaskConfiguration>(task.Configuration);
        var registration = catalog.GetHandler(config.HandlerKey);
        var handler = (IScheduledTaskHandler)serviceProvider.GetRequiredService(registration.HandlerType);
        logger.LogInformation("Program handler started. HandlerKey={HandlerKey}", registration.Key);
        await handler.ExecuteAsync(new ScheduledTaskContext(
            runId, task.Id, task.Name, config.Parameters, serviceProvider), cancellationToken);
        logger.LogInformation("Program handler completed. HandlerKey={HandlerKey}", registration.Key);
        return new TaskExecutionResult($"Program handler '{registration.Key}' completed.");
    }
}
