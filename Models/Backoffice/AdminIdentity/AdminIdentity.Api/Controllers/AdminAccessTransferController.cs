using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/access-transfer")]
public sealed class AdminAccessTransferController(IAdminAccessTransferService service) : ControllerBase
{
    [HttpGet("{kind}")]
    public async Task<ActionResult<AccessTransferDocument>> ExportAsync(string kind, CancellationToken cancellationToken) =>
        Ok(await service.ExportAsync(kind, cancellationToken));

    [HttpPost("compare")]
    [RequestSizeLimit(4_194_304)]
    public async Task<ActionResult<AccessTransferPreview>> CompareAsync(
        AccessTransferDocument document, CancellationToken cancellationToken) =>
        Ok(await service.CompareAsync(document, cancellationToken));

    [HttpPost("apply")]
    [RequestSizeLimit(4_194_304)]
    public async Task<ActionResult<AccessTransferApplyResult>> ApplyAsync(
        AccessTransferApplyRequest request, CancellationToken cancellationToken) =>
        Ok(await service.ApplyAsync(request, cancellationToken));
}
