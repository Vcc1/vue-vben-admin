using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using TaskScheduling.App;

namespace TaskScheduling.Infra;

internal sealed class QuartzTaskCoordinator(
    ISchedulerFactory schedulerFactory,
    IServiceScopeFactory scopeFactory) : ITaskSchedulerCoordinator
{
    private const string JobGroup = "admin-scheduled-tasks";
    private const string CronTriggerGroup = "admin-scheduled-task-cron";
    private const string RunTriggerGroup = "admin-scheduled-task-runs";

    public async Task SynchronizeTaskAsync(ScheduledTaskDefinition task, CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = GetJobKey(task.Id);
        var job = JobBuilder.Create<ScheduledTaskQuartzJob>()
            .WithIdentity(jobKey)
            .UsingJobData("TaskId", task.Id.ToString())
            .StoreDurably()
            .RequestRecovery(false)
            .Build();
        await scheduler.AddJob(job, replace: true, cancellationToken);

        var triggerKey = GetCronTriggerKey(task.Id);
        if (!task.IsEnabled)
        {
            await scheduler.UnscheduleJob(triggerKey, cancellationToken);
            return;
        }

        var schedule = CronScheduleBuilder.CronSchedule(task.CronExpression)
            .InTimeZone(TaskSchedulingTimeZone.Resolve(task.TimeZoneId));
        schedule = task.MisfirePolicy == "FireOnce"
            ? schedule.WithMisfireHandlingInstructionFireAndProceed()
            : schedule.WithMisfireHandlingInstructionDoNothing();
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithSchedule(schedule)
            .Build();

        if (await scheduler.CheckExists(triggerKey, cancellationToken))
            await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken);
        else
            await scheduler.ScheduleJob(trigger, cancellationToken);
    }

    public async Task RemoveTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.DeleteJob(GetJobKey(taskId), cancellationToken);
    }

    public async Task TriggerRunAsync(Guid taskId, Guid runId, DateTime? fireAtUtc, CancellationToken cancellationToken)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = GetJobKey(taskId);
        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<ITaskSchedulingStore>();
            var task = await store.GetTaskAsync(taskId, cancellationToken)
                ?? throw new KeyNotFoundException("Scheduled task was not found.");
            await SynchronizeTaskAsync(task, cancellationToken);
        }

        var triggerKey = GetRunTriggerKey(runId);
        if (await scheduler.CheckExists(triggerKey, cancellationToken)) return;
        var builder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .UsingJobData("RunId", runId.ToString());
        builder = fireAtUtc.HasValue
            ? builder.StartAt(new DateTimeOffset(DateTime.SpecifyKind(fireAtUtc.Value, DateTimeKind.Utc)))
            : builder.StartNow();
        await scheduler.ScheduleJob(builder.Build(), cancellationToken);
    }

    public async Task InterruptRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var run = await db.ScheduledTaskRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Run was not found.");
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        if (run.Status == ScheduledTaskStatuses.Queued)
        {
            await scheduler.UnscheduleJob(GetRunTriggerKey(runId), cancellationToken);
            run.Status = ScheduledTaskStatuses.Cancelled;
            run.FinishedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(run.QuartzFireInstanceId))
        {
            await scheduler.Interrupt(run.QuartzFireInstanceId, cancellationToken);
        }
    }

    private static JobKey GetJobKey(Guid taskId) => new($"task-{taskId:N}", JobGroup);
    private static TriggerKey GetCronTriggerKey(Guid taskId) => new($"cron-{taskId:N}", CronTriggerGroup);
    private static TriggerKey GetRunTriggerKey(Guid runId) => new($"run-{runId:N}", RunTriggerGroup);
}
