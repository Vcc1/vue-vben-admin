using GrafanaAlertApp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;

namespace GrafanaAlertWebhook.Controllers;

[ApiController]
[Route("api/grafana/alerts")]
public sealed class GrafanaAlertController : ControllerBase
{
    readonly IGrafanaAlertForwarder _forwarder;
    readonly ILogger<GrafanaAlertController> _logger;

    public GrafanaAlertController(
        IGrafanaAlertForwarder forwarder,
        ILogger<GrafanaAlertController> logger)
    {
        _forwarder = forwarder;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] GrafanaWebhookPayload? payload,
        CancellationToken cancellationToken)
    {
        var requestBody = await ReadRequestBodyAsync(cancellationToken);
        _logger.LogInformation("Received Grafana alert webhook. Body={RequestBody}", requestBody);

        if (payload is null)
        {
            return BadRequest(new { message = "Grafana webhook payload is required." });
        }

        try
        {
            await _forwarder.ForwardAsync(payload, cancellationToken);
            return Ok(new { message = "Grafana alert forwarded." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid Grafana alert payload. Body={RequestBody}", requestBody);
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to forward Grafana alert to internal alert API. Body={RequestBody}", requestBody);
            return Ok(new { message = "Grafana alert received, but internal alert API request failed." });
        }
    }

    async Task<string> ReadRequestBodyAsync(CancellationToken cancellationToken)
    {
        if (!Request.Body.CanSeek)
        {
            Request.EnableBuffering();
        }

        Request.Body.Position = 0;
        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;
        return body;
    }
}
