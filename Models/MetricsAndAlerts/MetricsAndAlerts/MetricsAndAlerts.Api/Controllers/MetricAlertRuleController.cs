using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
[Route("api/metric-alert-rule")]
public sealed class MetricAlertRuleController : ControllerBase
{
    readonly MetricAlertRuleService _service;

    public MetricAlertRuleController(MetricAlertRuleService service)
    {
        _service = service;
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<MetricAlertRuleDto>> Upsert(
        [FromBody] UpsertMetricAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpsertAsync(request, cancellationToken));
    }

    [HttpGet("list")]
    public async Task<ActionResult<IReadOnlyList<MetricAlertRuleDto>>> List(
        [FromQuery] string? targetKey,
        [FromQuery] MonitorTargetType? targetType,
        [FromQuery] bool includeDisabled,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAsync(new MetricAlertRuleListQuery
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
            ? Ok(new { message = "Metric alert rule disabled." })
            : NotFound(new { message = "Metric alert rule not found." });
    }

    [HttpPost("preview")]
    public async Task<ActionResult<MetricAlertPreviewDto>> Preview(
        [FromBody] MetricAlertRulePreviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.PreviewAsync(request, cancellationToken));
    }

    [HttpPost("test-send")]
    public async Task<ActionResult<TestSendMessageDto>> TestSend(
        [FromBody] MetricAlertRuleTestSendRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.TestSendAsync(request, cancellationToken));
    }
}
