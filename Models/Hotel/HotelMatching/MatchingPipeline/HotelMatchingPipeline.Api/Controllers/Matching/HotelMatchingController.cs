using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HotelMatchingPipeline.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/admin/hotel-matching")]
public sealed class HotelMatchingController : ControllerBase
{
    readonly HotelDebugService _debugService;
    readonly HotelMatchingService _matchingService;
    readonly HotelMatchVetoService _vetoService;

    public HotelMatchingController(
        HotelDebugService debugService,
        HotelMatchingService matchingService,
        HotelMatchVetoService vetoService)
    {
        _debugService = debugService;
        _matchingService = matchingService;
        _vetoService = vetoService;
    }

    [HttpPost("analyze")]
    public Task<HotelDebugAnalysis> AnalyzeAsync(
        [FromBody] HotelDebugRequest request,
        CancellationToken cancellationToken)
        => _debugService.AnalyzeAsync(request, cancellationToken);

    [HttpPost("reviews/confirm")]
    public Task<HotelManualReviewResult> ConfirmAsync(
        [FromBody] HotelManualReviewRequest request,
        CancellationToken cancellationToken)
        => _matchingService.ConfirmAsync(request, cancellationToken);

    [HttpPost("reviews/reject")]
    public Task<HotelManualReviewResult> RejectAsync(
        [FromBody] HotelManualReviewRequest request,
        CancellationToken cancellationToken)
        => _matchingService.RejectAsync(request, cancellationToken);

    [HttpPost("vetoes")]
    [ProducesResponseType<HotelMatchVetoResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<HotelMatchVetoResult>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HotelMatchVetoResult>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HotelMatchVetoResult>> VetoAsync(
        [FromBody] HotelMatchVetoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _vetoService.VetoAsync(request, cancellationToken);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.Status == "currentMatchChanged"
                ? Conflict(result)
                : NotFound(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid hotel match veto request",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
