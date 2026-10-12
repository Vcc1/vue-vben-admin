using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/target-url-monitor-rule")]
public sealed class TargetUrlMonitorRuleController : ControllerBase
{
    readonly TargetUrlMonitorRuleService _service;

    public TargetUrlMonitorRuleController(TargetUrlMonitorRuleService service)
    {
        _service = service;
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<TargetUrlMonitorRuleDto>> Upsert(
        [FromBody] UpsertTargetUrlMonitorRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpsertAsync(request, cancellationToken));
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<TargetUrlMonitorRuleDto>>> List(
        [FromQuery] string targetKey,
        [FromQuery] MonitorTargetType targetType,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAsync(targetKey, targetType, cancellationToken));
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] long id, CancellationToken cancellationToken)
    {
        return await _service.DeleteAsync(id, cancellationToken)
            ? Ok(new { message = "Target URL monitor rule disabled." })
            : NotFound(new { message = "Target URL monitor rule not found." });
    }
}
