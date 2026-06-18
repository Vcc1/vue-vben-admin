using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/target-alive-alert-rule")]
public sealed class TargetAliveAlertRuleController : ControllerBase
{
    readonly TargetAliveAlertRuleService _service;

    public TargetAliveAlertRuleController(TargetAliveAlertRuleService service)
    {
        _service = service;
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<TargetAliveAlertRuleDto>> Upsert(
        [FromBody] UpsertTargetAliveAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpsertAsync(request, cancellationToken));
    }

    [HttpGet("get")]
    public async Task<ActionResult<TargetAliveAlertRuleDto?>> Get(
        [FromQuery] string targetKey,
        [FromQuery] MonitorTargetType targetType,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAsync(targetKey, targetType, cancellationToken));
    }
}
