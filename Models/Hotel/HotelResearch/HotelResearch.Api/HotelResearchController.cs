using AgentPlatform.App;
using HotelBusiness.App;
using HotelResearch.App;
using HotelResearch.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HotelResearch.Api;

[ApiController]
[Route("api/hotel-research")]
public sealed class HotelResearchController(
    IHotelResearchService researchService,
    IHotelBusinessDataReader businessDataReader,
    IHotelPhoneNormalizationService phoneNormalizationService,
    ILogger<HotelResearchController> logger) : ControllerBase
{
    [HttpGet("{mtHotelCode}/business-data")]
    [ProducesResponseType<HotelBusinessDataSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HotelBusinessDataSnapshot>> GetBusinessDataAsync(
        string mtHotelCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await businessDataReader.ReadAsync(mtHotelCode, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid hotel business data request",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HotelResearchConfigurationException exception)
        {
            return Problem(
                title: "Hotel business data unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hotel business data query failed. MTHotelCode={MTHotelCode}",
                mtHotelCode);
            return Problem(
                title: "Hotel business data unavailable",
                detail: "RoomMapping did not complete the hotel business data query.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("phone-normalizations")]
    [ProducesResponseType<HotelPhoneNormalizationBatchResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<HotelPhoneNormalizationBatchResult> NormalizePhones(
        [FromBody] HotelPhoneNormalizationBatchRequest request)
    {
        if (request.Items.Count > 1000)
        {
            return Problem(
                title: "Invalid phone normalization request",
                detail: "items cannot contain more than 1000 phone records.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.Items.Any(item =>
            string.IsNullOrWhiteSpace(item.Id)
            || item.Id.Length > 200
            || string.IsNullOrWhiteSpace(item.Raw)
            || item.Raw.Length > 500
            || item.CountryCode?.Length > 128))
        {
            return Problem(
                title: "Invalid phone normalization request",
                detail: "Each item requires a valid id and raw phone value.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.Items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != request.Items.Count)
        {
            return Problem(
                title: "Invalid phone normalization request",
                detail: "Phone normalization item ids must be unique.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(phoneNormalizationService.Normalize(request));
    }

    [HttpPost("resolve")]
    [ProducesResponseType<HotelResearchResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HotelResearchResult>> ResolveAsync(
        [FromBody] HotelResearchResolveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await researchService.ResolveAsync(request, cancellationToken);
            return result is null
                ? HotelNotFound(request.MTHotelCode)
                : Ok(result);
        }
        catch (HotelResearchResultUnavailableException exception)
        {
            return Problem(
                title: "Hotel research result unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid hotel research request",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost]
    [ProducesResponseType<HotelResearchResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HotelResearchResult>> ResearchAsync(
        [FromBody] HotelResearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await researchService.ResearchAsync(request, cancellationToken);
            return result is null
                ? HotelNotFound(request.MTHotelCode)
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid hotel research request",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HotelOperationAlreadyRunningException exception)
        {
            return Problem(
                title: "Hotel research already running",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (HotelBusinessStorageException exception)
        {
            logger.LogError(
                exception,
                "Hotel business file storage failed. MTHotelCode={MTHotelCode}",
                request.MTHotelCode);
            return Problem(
                title: "Hotel business storage unavailable",
                detail: "The hotel business files could not be read or written.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (
            exception is HotelResearchConfigurationException or AgentPlatformConfigurationException)
        {
            logger.LogWarning(
                "Hotel research is not configured. MTHotelCode={MTHotelCode}, Reason={Reason}",
                request.MTHotelCode,
                exception.Message);
            return Problem(
                title: "Hotel research unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HotelResearchExecutionException exception)
        {
            logger.LogError(
                exception,
                "Hotel research returned an invalid result. MTHotelCode={MTHotelCode}",
                request.MTHotelCode);
            return Problem(
                title: "Hotel research failed",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hotel research request failed. MTHotelCode={MTHotelCode}",
                request.MTHotelCode);
            return Problem(
                title: "Hotel research failed",
                detail: "RoomMapping or MAF did not complete the hotel research request.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    ObjectResult HotelNotFound(string? mtHotelCode)
        => Problem(
            title: "Hotel not found",
            detail: $"OTAHotelInfo does not contain MTHotelCode '{mtHotelCode?.Trim()}'.",
            statusCode: StatusCodes.Status404NotFound);
}

[ApiController]
[Route("api/hotel-facts")]
public sealed class HotelFactsController(IHotelResearchService researchService) : ControllerBase
{
    [HttpGet("{mtHotelCode}")]
    [ProducesResponseType<HotelFactsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<HotelFactsResult>> GetLatestAsync(
        string mtHotelCode,
        CancellationToken cancellationToken)
        => GetCoreAsync(mtHotelCode, null, cancellationToken);

    [HttpGet("{mtHotelCode}/versions/{resultVersion:int}")]
    [ProducesResponseType<HotelFactsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<HotelFactsResult>> GetVersionAsync(
        string mtHotelCode,
        int resultVersion,
        CancellationToken cancellationToken)
        => GetCoreAsync(mtHotelCode, resultVersion, cancellationToken);

    [HttpPost("batch")]
    [ProducesResponseType<IReadOnlyList<HotelFactsBatchItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HotelFactsBatchItem>>> ResolveBatchAsync(
        [FromBody] HotelFactsBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MTHotelCodes.Count is 0 or > 100)
        {
            return Problem(
                title: "Invalid hotel facts batch",
                detail: "mthotelCodes must contain between 1 and 100 items.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var items = new List<HotelFactsBatchItem>(request.MTHotelCodes.Count);
        foreach (var code in request.MTHotelCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var result = await researchService.ResolveAsync(
                new HotelResearchResolveRequest { MTHotelCode = code, Mode = request.Mode },
                cancellationToken);
            var facts = result is null
                ? null
                : await researchService.GetFactsAsync(code, result.ResultVersion, cancellationToken);
            items.Add(new HotelFactsBatchItem
            {
                MTHotelCode = code.Trim().ToUpperInvariant(),
                Facts = facts,
                NotFound = result is null
            });
        }

        return Ok(items);
    }

    async Task<ActionResult<HotelFactsResult>> GetCoreAsync(
        string mtHotelCode,
        int? resultVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await researchService.GetFactsAsync(
                mtHotelCode,
                resultVersion,
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                title: "Invalid hotel facts request",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
