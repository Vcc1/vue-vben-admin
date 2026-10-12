using HotelBusiness.App;
using HotelBusiness.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HotelBusiness.Api;

[ApiController]
[Route("api/hotel-business")]
public sealed class HotelBusinessController(
    IHotelBusinessService service,
    ILogger<HotelBusinessController> logger) : ControllerBase
{
    [HttpGet("recent")]
    [ProducesResponseType<IReadOnlyList<HotelBusinessRecentItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HotelBusinessRecentItem>>> GetRecentAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.GetRecentAsync(cancellationToken));
        }
        catch (HotelBusinessStorageException exception)
        {
            return StorageProblem(exception);
        }
    }

    [HttpGet("{mtHotelCode}")]
    [ProducesResponseType<HotelBusinessDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HotelBusinessDetail>> GetAsync(
        string mtHotelCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var hotel = await service.GetAsync(mtHotelCode, cancellationToken);
            return hotel is null ? NotFound() : Ok(hotel);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception);
        }
        catch (HotelBusinessStorageException exception)
        {
            return StorageProblem(exception);
        }
    }

    [HttpGet("{mtHotelCode}/operations/{operationType}/results/latest")]
    public Task<ActionResult> GetLatestResultAsync(
        string mtHotelCode,
        string operationType,
        CancellationToken cancellationToken)
        => GetResultCoreAsync(mtHotelCode, operationType, null, cancellationToken);

    [HttpGet("{mtHotelCode}/operations/{operationType}/results/{resultVersion:int}")]
    public Task<ActionResult> GetResultAsync(
        string mtHotelCode,
        string operationType,
        int resultVersion,
        CancellationToken cancellationToken)
        => GetResultCoreAsync(mtHotelCode, operationType, resultVersion, cancellationToken);

    [HttpGet("{mtHotelCode}/operations/{operationType}/runs/{runId}/conversation")]
    [ProducesResponseType<HotelBusinessRunConversation>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HotelBusinessRunConversation>> GetRunConversationAsync(
        string mtHotelCode,
        string operationType,
        string runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await service.GetRunConversationAsync(
                mtHotelCode,
                operationType,
                runId,
                cancellationToken);
            return conversation is null ? NotFound() : Ok(conversation);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception);
        }
        catch (HotelBusinessStorageException exception)
        {
            return StorageProblem(exception);
        }
    }

    async Task<ActionResult> GetResultCoreAsync(
        string mtHotelCode,
        string operationType,
        int? resultVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetResultAsync(
                mtHotelCode,
                operationType,
                resultVersion,
                cancellationToken);
            return result is null ? NotFound() : Ok(result.Value);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception);
        }
        catch (HotelBusinessStorageException exception)
        {
            return StorageProblem(exception);
        }
    }

    ObjectResult InvalidRequest(Exception exception)
        => Problem(
            title: "Invalid hotel business request",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest);

    ObjectResult StorageProblem(HotelBusinessStorageException exception)
    {
        logger.LogError(exception, "Hotel business file storage failed.");
        return Problem(
            title: "Hotel business storage unavailable",
            detail: "The hotel business files could not be read or written.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
