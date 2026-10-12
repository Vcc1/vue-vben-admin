namespace AdminIdentity.App;

public sealed record TransferPermission(string Code, string Name, string? Description, bool IsEnabled);
public sealed record TransferRole(string Name, IReadOnlyList<string> Permissions);

public sealed record AccessTransferDocument(
    string Format, int Version, string Kind,
    IReadOnlyList<TransferPermission>? Permissions,
    IReadOnlyList<TransferRole>? Roles);

public sealed record AccessTransferChange(
    string Key, string Action,
    TransferPermission? BeforePermission, TransferPermission? AfterPermission,
    TransferRole? BeforeRole, TransferRole? AfterRole,
    string? BlockReason, string? Impact);

public sealed record AccessTransferPreview(string Baseline, IReadOnlyList<AccessTransferChange> Changes);
public sealed record AccessTransferApplyRequest(
    AccessTransferDocument Document, string Baseline, IReadOnlyList<string> SelectedKeys);
public sealed record AccessTransferApplyResult(int AppliedCount);

public interface IAdminAccessTransferService
{
    Task<AccessTransferDocument> ExportAsync(string kind, CancellationToken cancellationToken);
    Task<AccessTransferPreview> CompareAsync(AccessTransferDocument document, CancellationToken cancellationToken);
    Task<AccessTransferApplyResult> ApplyAsync(AccessTransferApplyRequest request, CancellationToken cancellationToken);
}
