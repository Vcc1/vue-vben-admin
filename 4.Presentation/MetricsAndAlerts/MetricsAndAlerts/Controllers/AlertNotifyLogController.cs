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
    public async Task<ActionResult<PagedResultDto<AlertNotifyLogDto>>> List(
        [FromQuery] string? targetKey,
        [FromQuery] MonitorTargetType? targetType,
        [FromQuery] AlertType? alertType,
        [FromQuery] long? ruleId,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAlertNotifyLogsAsync(new AlertNotifyLogListQuery
        {
            TargetKey = targetKey,
            TargetType = targetType,
            AlertType = alertType,
            RuleId = ruleId,
            StartTime = startTime,
            EndTime = endTime,
            Page = page ?? 1,
            PageSize = pageSize ?? 20
        }, cancellationToken));
    }
}
