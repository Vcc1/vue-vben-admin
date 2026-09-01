using AgentPlatform.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Api;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/agents/conversations")]
public sealed class AgentConversationController(
    IAgentConversationService conversationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AgentConversationSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentConversationSummary>>> ListAsync(
        [FromQuery] string? query,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await conversationService.ListAsync(query, take, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Problem(
                title: "Invalid conversation query",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("{conversationId}")]
    [ProducesResponseType<AgentConversation>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentConversation>> GetAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await conversationService.GetAsync(conversationId, cancellationToken);
            return conversation is null ? NotFound() : Ok(conversation);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid conversation id",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{conversationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await conversationService.DeleteAsync(conversationId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid conversation id",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
