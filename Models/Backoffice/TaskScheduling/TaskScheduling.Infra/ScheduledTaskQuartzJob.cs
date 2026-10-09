using System.Text.Json;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduling.App;

namespace TaskScheduling.Infra;

[DisallowConcurrentExecution]
internal sealed class ScheduledTaskQuartzJob(
    AdminDbContext dbContext,
    ITaskSchedulingStore store,
    ITaskSchedulerCoordinator coordinator,
    IScheduledTaskExecutor executor,
    ScheduledRunContextAccessor runContext,
    RunFileLogStore logStore,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ScheduledTaskQuartzJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var taskId = Guid.Parse(context.MergedJobDataMap.GetString("TaskId")!);
        // Cron triggers do not carry a RunId; manual/retry triggers do.
        var runIdText = context.MergedJobDataMap.ContainsKey("RunId")
            ? context.MergedJobDataMap.GetString("RunId")
            : null;
        ScheduledTaskRunEntity run;

        if (Guid.TryParse(runIdText, out var queuedRunId))
        {
            run = await dbContext.ScheduledTaskRuns.SingleAsync(x => x.Id == queuedRunId, cancellationToken);
            if (run.Status == ScheduledTaskStatuses.Cancelled || run.CancellationRequestedAtUtc.HasValue) return;
        }
        else
        {
            var queued = await store.QueueRunAsync(
                taskId, "Scheduled", CurrentActor.System, null, 1,
                context.ScheduledFireTimeUtc?.UtcDateTime, cancellationToken);
            run = await dbContext.ScheduledTaskRuns.SingleAsync(x => x.Id == queued.Id, cancellationToken);
        }

        var task = JsonSerializer.Deserialize<ScheduledTaskDefinition>(run.DefinitionSnapshotJson, JsonDefaults.Options)
            ?? throw new InvalidOperationException($"Run '{run.Id}' has an invalid task definition snapshot.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        run.Status = ScheduledTaskStatuses.Running;
        run.StartedAtUtc = now;
        run.LastHeartbeatAtUtc = now;
        run.HostInstanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
        run.QuartzFireInstanceId = context.FireInstanceId;
        await dbContext.SaveChangesAsync(cancellationToken);

        using var logScope = runContext.Push(run.Id, run.LogRelativePath, TaskLogFilterParser.Parse(task.Configuration));
        logger.LogInformation(
            "Scheduled task started. RunId={RunId} TaskId={TaskId} TaskName={TaskName} TaskType={TaskType} TriggerType={TriggerType} Actor={Actor}",
            run.Id, task.Id, task.Name, task.TaskType, run.TriggerType,
            run.TriggeredByActorName ?? "system");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(task.TimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var monitorStop = new CancellationTokenSource();
        var monitor = MonitorAsync(run.Id, linked, monitorStop.Token);
        string finalStatus;
        string? error = null;
        string? result = null;
        try
        {
            result = (await executor.ExecuteAsync(task, run.Id, linked.Token)).Summary;
            finalStatus = ScheduledTaskStatuses.Succeeded;
            logger.LogInformation("Scheduled task completed. RunId={RunId} Result={Result}", run.Id, result);
        }
        catch (OperationCanceledException exception)
        {
            await dbContext.Entry(run).ReloadAsync(CancellationToken.None);
            finalStatus = run.CancellationRequestedAtUtc.HasValue
                ? ScheduledTaskStatuses.Cancelled
                : timeout.IsCancellationRequested
                    ? ScheduledTaskStatuses.TimedOut
                    : ScheduledTaskStatuses.Interrupted;
            error = exception.Message;
            logger.LogWarning(exception, "Scheduled task stopped. RunId={RunId} Status={Status}", run.Id, finalStatus);
        }
        catch (Exception exception)
        {
            finalStatus = ScheduledTaskStatuses.Failed;
            error = exception.ToString();
            logger.LogError(exception, "Scheduled task failed. RunId={RunId}", run.Id);
        }
        finally
        {
            monitorStop.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }

        run.Status = finalStatus;
        run.ResultSummary = result;
        run.ErrorSummary = error is null ? null : error[..Math.Min(error.Length, 8000)];
        run.FinishedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var file = logStore.GetFileState(run.LogRelativePath);
        run.LogLengthBytes = file.Length;
        run.IsLogTruncated = file.Truncated;
        await dbContext.SaveChangesAsync(CancellationToken.None);

        try
        {
            await logStore.CompressAsync(run.LogRelativePath);
            run.LogRelativePath += ".gz";
            var compressed = logStore.GetFileState(run.LogRelativePath);
            run.LogLengthBytes = compressed.Length;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // Compression is best-effort; the uncompressed log remains readable.
        }

        var shouldRetry = run.AttemptNumber <= task.RetryCount
            && (finalStatus == ScheduledTaskStatuses.Failed
                || finalStatus == ScheduledTaskStatuses.TimedOut && task.RetryOnTimeout);
        if (shouldRetry)
        {
            var retry = await store.QueueRunAsync(
                task.Id, "AutoRetry", CurrentActor.System, run.RootRunId,
                run.AttemptNumber + 1, null, CancellationToken.None);
            await coordinator.TriggerRunAsync(
                task.Id, retry.Id, timeProvider.GetUtcNow().UtcDateTime.AddSeconds(task.RetryIntervalSeconds), CancellationToken.None);
        }
    }

    private async Task MonitorAsync(Guid runId, CancellationTokenSource execution, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), timeProvider);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var run = await db.ScheduledTaskRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
            if (run is null) return;
            if (run.CancellationRequestedAtUtc.HasValue)
            {
                execution.Cancel();
                return;
            }
            run.LastHeartbeatAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
