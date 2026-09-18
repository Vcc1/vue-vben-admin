using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskScheduling.App;

namespace TaskScheduling.Infra;

internal sealed class TaskSchedulingReconciler(
    IServiceScopeFactory scopeFactory,
    ITaskSchedulerCoordinator coordinator,
    RunFileLogStore logStore,
    IOptions<TaskSchedulingOptions> options,
    TimeProvider timeProvider,
    ILogger<TaskSchedulingReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A transient database/Quartz failure must not permanently stop automatic scheduling.
                logger.LogError(exception, "Scheduled task reconciliation failed; retrying on the next cycle.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ITaskSchedulingStore>();
        foreach (var task in await store.ListTasksAsync(cancellationToken))
        {
            try
            {
                await coordinator.SynchronizeTaskAsync(task, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Unable to synchronize scheduled task {TaskId} ({TaskName}); other tasks will continue.",
                    task.Id, task.Name);
            }
        }

        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var staleBefore = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-2);
        var stale = await db.ScheduledTaskRuns
            .Where(x => x.Status == ScheduledTaskStatuses.Running && x.LastHeartbeatAtUtc < staleBefore)
            .ToListAsync(cancellationToken);
        foreach (var run in stale)
        {
            run.Status = ScheduledTaskStatuses.Interrupted;
            run.FinishedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            run.ErrorSummary = "The scheduler instance stopped updating its execution heartbeat.";
        }
        if (stale.Count != 0) await db.SaveChangesAsync(cancellationToken);

        var queued = await db.ScheduledTaskRuns.AsNoTracking()
            .Where(x => x.Status == ScheduledTaskStatuses.Queued && x.CancellationRequestedAtUtc == null)
            .Take(100).ToListAsync(cancellationToken);
        foreach (var run in queued)
        {
            try
            {
                await coordinator.TriggerRunAsync(run.TaskId, run.Id, run.ScheduledAtUtc, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Unable to dispatch queued scheduled task run {RunId}; it will be retried.", run.Id);
            }
        }

        await CleanupLogsAsync(db, cancellationToken);
    }

    private async Task CleanupLogsAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var files = new DirectoryInfo(logStore.Root).EnumerateFiles("*.ndjson", SearchOption.AllDirectories)
            .OrderByDescending(x => x.LastWriteTimeUtc).ToList();
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-Math.Max(1, settings.LogRetentionDays));
        long retained = 0;
        var deleted = new List<string>();
        foreach (var file in files)
        {
            var shouldDelete = file.LastWriteTimeUtc < cutoff || retained + file.Length > settings.MaxTotalLogBytes;
            if (!shouldDelete)
            {
                retained += file.Length;
                continue;
            }
            var relative = Path.GetRelativePath(logStore.Root, file.FullName);
            try { file.Delete(); deleted.Add(relative); }
            catch (IOException exception) { logger.LogWarning(exception, "Unable to delete scheduled task log {LogPath}.", relative); }
        }
        if (deleted.Count == 0) return;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var runs = await db.ScheduledTaskRuns.Where(x => deleted.Contains(x.LogRelativePath)).ToListAsync(cancellationToken);
        foreach (var run in runs) run.LogDeletedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
    }
}
