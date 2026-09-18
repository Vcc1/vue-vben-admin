using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskScheduling.App;

namespace TaskScheduling.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AdminPolicies.View)]
[Route("api/admin/scheduled-task-runs")]
public sealed class ScheduledTaskRunsController(ScheduledTaskService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduledTaskRun>>> ListAsync(
        [FromQuery] Guid? taskId = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await service.ListRunsAsync(taskId, take, cancellationToken));

    [HttpGet("{runId:guid}")]
    public async Task<ActionResult<ScheduledTaskRun>> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await service.GetRunAsync(runId, cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("{runId:guid}/logs")]
    public async Task<ActionResult<ScheduledTaskLogPage>> LogsAsync(
        Guid runId,
        [FromQuery] long afterSequence = 0,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        Ok(await service.ReadLogsAsync(runId, afterSequence, take, cancellationToken));

    [Authorize(Policy = AdminPolicies.Operate)]
    [HttpPost("{runId:guid}/cancel")]
    public async Task<IActionResult> CancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        await service.CancelAsync(runId, cancellationToken);
        return Accepted();
    }

    [Authorize(Policy = AdminPolicies.Operate)]
    [HttpPost("{runId:guid}/retry")]
    public async Task<ActionResult<QueuedRun>> RetryAsync(Guid runId, CancellationToken cancellationToken) =>
        Accepted(await service.RetryAsync(runId, cancellationToken));
}
