using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskScheduling.App;

namespace TaskScheduling.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AdminPolicies.View)]
[Route("api/admin/scheduled-tasks")]
public sealed class ScheduledTasksController(ScheduledTaskService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduledTaskDefinition>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListTasksAsync(cancellationToken));

    [HttpGet("catalog")]
    public ActionResult<ScheduledTaskCatalog> Catalog() => Ok(service.GetCatalog());

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<ScheduledTaskDefinition>> GetAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await service.GetTaskAsync(taskId, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [Authorize(Policy = AdminPolicies.ManageTasks)]
    [HttpPost]
    public async Task<ActionResult<ScheduledTaskDefinition>> CreateAsync(ScheduledTaskInput input, CancellationToken cancellationToken)
    {
        var task = await service.CreateTaskAsync(input, cancellationToken);
        return Created($"/api/admin/scheduled-tasks/{task.Id}", task);
    }

    [Authorize(Policy = AdminPolicies.ManageTasks)]
    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<ScheduledTaskDefinition>> UpdateAsync(
        Guid taskId, UpdateScheduledTaskRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateTaskAsync(taskId, request, cancellationToken));

    [Authorize(Policy = AdminPolicies.ManageTasks)]
    [HttpPut("{taskId:guid}/enabled")]
    public async Task<ActionResult<ScheduledTaskDefinition>> SetEnabledAsync(
        Guid taskId, SetTaskEnabledRequest request, CancellationToken cancellationToken) =>
        Ok(await service.SetEnabledAsync(taskId, request, cancellationToken));

    [Authorize(Policy = AdminPolicies.ManageTasks)]
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid taskId, [FromQuery] string rowVersion, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(taskId, rowVersion, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = AdminPolicies.Operate)]
    [HttpPost("{taskId:guid}/run")]
    public async Task<ActionResult<QueuedRun>> RunNowAsync(Guid taskId, CancellationToken cancellationToken) =>
        Accepted(await service.RunNowAsync(taskId, cancellationToken));

    [HttpGet("{taskId:guid}/runs")]
    public async Task<ActionResult<IReadOnlyList<ScheduledTaskRun>>> RunsAsync(
        Guid taskId, [FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await service.ListRunsAsync(taskId, take, cancellationToken));

    [Authorize(Policy = AdminPolicies.ManageTasks)]
    [HttpGet("{taskId:guid}/audits")]
    public async Task<ActionResult<IReadOnlyList<ScheduledTaskAudit>>> AuditsAsync(
        Guid taskId, [FromQuery] int take = 100, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAuditsAsync(taskId, take, cancellationToken));
}
