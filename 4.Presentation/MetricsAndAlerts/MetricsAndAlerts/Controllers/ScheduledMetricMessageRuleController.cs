using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/scheduled-message-rule")]
public sealed class ScheduledMetricMessageRuleController : ControllerBase
{
    readonly ScheduledMetricMessageRuleService _service;

    public ScheduledMetricMessageRuleController(ScheduledMetricMessageRuleService service)
    {
        _service = service;
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<ScheduledMetricMessageRuleDto>> Upsert(
        [FromBody] UpsertScheduledMetricMessageRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpsertAsync(request, cancellationToken));
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<ScheduledMetricMessageRuleDto>>> List(
        [FromQuery] string? targetKey,
        [FromQuery] MonitorTargetType? targetType,
        [FromQuery] bool includeDisabled,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAsync(new ScheduledMetricMessageRuleListQuery
        {
            TargetKey = targetKey,
            TargetType = targetType,
            IncludeDisabled = includeDisabled
        }, cancellationToken));
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] long id, CancellationToken cancellationToken)
    {
        return await _service.DeleteAsync(id, cancellationToken)
            ? Ok(new { message = "Scheduled metric message rule disabled." })
            : NotFound(new { message = "Scheduled metric message rule not found." });
    }

    [HttpPost("preview")]
    public async Task<ActionResult<ScheduledMetricMessagePreviewDto>> Preview(
        [FromBody] ScheduledMetricMessagePreviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.PreviewAsync(request, cancellationToken));
    }

    [HttpPost("test-send")]
    public async Task<ActionResult<TestSendMessageDto>> TestSend(
        [FromBody] ScheduledMetricMessageRuleTestSendRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.TestSendAsync(request, cancellationToken));
    }
}
