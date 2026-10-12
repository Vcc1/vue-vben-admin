using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using RoomMatch.App;

namespace RoomMatch.Api.Controllers;

[ApiController]
[Route("api/HotelRoom/OTAHotelRoom")]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
public sealed class OTAHotelRoomController : ControllerBase
{
    private readonly OTAHotelRoomApp _otaHotelRoomApp;

    public OTAHotelRoomController(OTAHotelRoomApp otaHotelRoomApp)
    {
        _otaHotelRoomApp = otaHotelRoomApp;
    }

    [HttpGet("GetHotelRoomMappingV2")]
    public async Task<IActionResult> GetHotelRoomMappingV2(
        [FromQuery] string mtHotelCode,
        [FromQuery] bool isShowWord = true,
        [FromQuery] bool isOnlyOtaRooms = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _otaHotelRoomApp.GetHotelRoomMappingV2(
            mtHotelCode, isShowWord, isOnlyOtaRooms, cancellationToken);
        return Ok(result);
    }

    [HttpGet("GetHotelRoomMappingCache")]
    public async Task<IActionResult> GetHotelRoomMappingCache(
        [FromQuery] string mtHotelCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _otaHotelRoomApp.GetHotelRoomMappingCache(mtHotelCode, cancellationToken);
        return Ok(result);
    }

    [HttpGet("GetWeeklyAgentStats")]
    public async Task<IActionResult> GetWeeklyAgentStats(CancellationToken cancellationToken = default)
    {
        var result = await _otaHotelRoomApp.GetWeeklyAgentStats(cancellationToken: cancellationToken);
        return Ok(result);
    }
}
