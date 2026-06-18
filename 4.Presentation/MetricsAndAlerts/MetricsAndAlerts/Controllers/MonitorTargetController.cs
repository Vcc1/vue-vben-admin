using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/monitor-target")]
public sealed class MonitorTargetController : ControllerBase
{
    readonly MonitorTargetService _service;

    public MonitorTargetController(MonitorTargetService service)
    {
        _service = service;
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<MonitorTargetListItemDto>> Upsert(
        [FromBody] UpsertMonitorTargetRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpsertAsync(request, cancellationToken));
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<MonitorTargetListItemDto>>> List(
        [FromQuery] MonitorTargetType? targetType,
        [FromQuery] string? keyword,
        [FromQuery] long? groupId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAsync(new MonitorTargetListQuery
        {
            TargetType = targetType,
            Keyword = keyword,
            GroupId = groupId
        }, cancellationToken));
    }

    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<MonitorTargetListItemDto>>> Applications(CancellationToken cancellationToken)
    {
        return Ok(await _service.ListApplicationsAsync(cancellationToken));
    }

    [HttpGet("instances")]
    public async Task<ActionResult<IReadOnlyList<TargetInstanceDto>>> Instances(
        [FromQuery] string targetKey,
        [FromQuery] MonitorTargetType targetType,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetInstancesAsync(targetKey, targetType, cancellationToken));
    }
}
