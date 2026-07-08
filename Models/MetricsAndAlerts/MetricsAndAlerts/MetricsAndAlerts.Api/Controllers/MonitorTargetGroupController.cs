using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/monitor-target-group")]
public sealed class MonitorTargetGroupController : ControllerBase
{
    readonly MonitorTargetGroupService _service;

    public MonitorTargetGroupController(MonitorTargetGroupService service)
    {
        _service = service;
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<MonitorTargetGroupDto>> Upsert(
        [FromBody] UpsertMonitorTargetGroupRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpsertAsync(request, cancellationToken));
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<MonitorTargetGroupDto>>> List(
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.ListAsync(includeDisabled, cancellationToken));
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] long id, CancellationToken cancellationToken)
    {
        return await _service.DeleteAsync(id, cancellationToken)
            ? Ok(new { message = "Monitor target group disabled." })
            : NotFound(new { message = "Monitor target group not found." });
    }
}
