using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Models;
using Microsoft.AspNetCore.Mvc;

namespace HotelMatchingPipeline.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/admin/hotel-matching")]
public sealed class HotelMatchingController : ControllerBase
{
    readonly HotelDebugService _debugService;
    readonly HotelMatchingService _matchingService;

    public HotelMatchingController(
        HotelDebugService debugService,
        HotelMatchingService matchingService)
    {
        _debugService = debugService;
        _matchingService = matchingService;
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
}
