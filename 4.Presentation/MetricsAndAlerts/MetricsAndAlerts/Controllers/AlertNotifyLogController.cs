using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/alert-notify-log")]
public sealed class AlertNotifyLogController : ControllerBase
{
    readonly AlertQueryService _service;

    public AlertNotifyLogController(AlertQueryService service)
    {
        _service = service;
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<AlertNotifyLogDto>>> List(
        [FromQuery] string? targetKey,
        [FromQuery] MonitorTargetType? targetType,
        [FromQuery] AlertType? alertType,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAlertNotifyLogsAsync(new AlertNotifyLogListQuery
        {
            TargetKey = targetKey,
            TargetType = targetType,
            AlertType = alertType,
            StartTime = startTime,
            EndTime = endTime
        }, cancellationToken));
    }
}
