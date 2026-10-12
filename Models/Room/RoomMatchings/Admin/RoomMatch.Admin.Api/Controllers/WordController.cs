using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using RoomMatch.Admin.App;
using RoomMatch.Admin.Contracts;

namespace RoomMatch.Admin.Api.Controllers;

[ApiController]
[Route("api/HotelRoom/Word")]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
public sealed class WordController : ControllerBase
{
    private readonly WordApp _wordApp;

    public WordController(WordApp wordApp)
    {
        _wordApp = wordApp;
    }

    [HttpGet("GetData")]
    public async Task<IActionResult> GetData(CancellationToken cancellationToken = default)
    {
        var result = await _wordApp.GetData(cancellationToken);
        return Ok(result);
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(
        [FromBody] List<WordDto> data,
        CancellationToken cancellationToken = default)
    {
        await _wordApp.Update(data, cancellationToken);
        return Ok(new { message = "更新成功" });
    }
}
