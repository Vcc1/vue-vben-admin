using AgentPlatform.App;
using AgentPlatform.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentPlatform.Api;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/agents")]
public sealed class AgentController(
    IAgentService agentService,
    ILogger<AgentController> logger) : ControllerBase
{
    static readonly JsonSerializerOptions StreamJsonOptions = CreateStreamJsonOptions();

    [HttpPost("{agentName}/runs")]
    [ProducesResponseType<AgentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentResponse>> RunAsync(
        string agentName,
        [FromBody] AgentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await agentService.RunAsync(
                request with { AgentName = agentName },
                cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid agent request",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (AgentPlatformConfigurationException exception)
        {
            logger.LogWarning(
                "Agent service is not configured. AgentName={AgentName}, Reason={Reason}",
                agentName,
                exception.Message);
            return Problem(
                title: "Agent service unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("{agentName}/runs/stream")]
    [Produces("application/x-ndjson")]
    public async Task RunStreamingAsync(
        string agentName,
        [FromBody] AgentRequest request,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson; charset=utf-8";

        try
        {
            await foreach (var item in agentService.RunStreamingAsync(
                request with { AgentName = agentName },
                cancellationToken))
            {
                await WriteEventAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller closed the streaming connection.
        }
        catch (AgentPlatformConfigurationException exception)
        {
            logger.LogWarning(
                "Agent service is not configured. AgentName={AgentName}, Reason={Reason}",
                agentName,
                exception.Message);
            await WriteEventAsync(new AgentStreamEvent
            {
                Kind = AgentStreamEventKind.Error,
                ConversationId = request.ConversationId ?? string.Empty,
                Error = exception.Message
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Agent streaming request failed. AgentName={AgentName}", agentName);
            await WriteEventAsync(new AgentStreamEvent
            {
                Kind = AgentStreamEventKind.Error,
                ConversationId = request.ConversationId ?? string.Empty,
                Error = "Agent execution failed."
            }, cancellationToken);
        }
    }

    async Task WriteEventAsync(AgentStreamEvent item, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(
            Response.Body,
            item,
            StreamJsonOptions,
            cancellationToken);
        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    static JsonSerializerOptions CreateStreamJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
