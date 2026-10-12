using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/alert-state")]
public sealed class AlertStateController : ControllerBase
{
    readonly AlertQueryService _service;

    public AlertStateController(AlertQueryService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<AlertStateDto>>> List(
        [FromQuery] string? targetKey,
        [FromQuery] MonitorTargetType? targetType,
        [FromQuery] AlertType? alertType,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAlertStatesAsync(new AlertStateListQuery
        {
            TargetKey = targetKey,
            TargetType = targetType,
            AlertType = alertType,
            IsActive = isActive
        }, cancellationToken));
    }
}
