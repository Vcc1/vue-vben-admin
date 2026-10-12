using Backoffice.App.HotelPriceComparison;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backoffice.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/admin/hotel-price-comparison/run-histories")]
public sealed class HotelPriceComparisonRunHistoryController(
    HotelPriceComparisonRunHistoryService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HotelPriceComparisonRunHistoryPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HotelPriceComparisonRunHistoryPage>> ListAsync(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await service.ListAsync(
                new HotelPriceComparisonRunHistoryQuery(startDate, endDate, page, pageSize),
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid hotel price comparison history query",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
