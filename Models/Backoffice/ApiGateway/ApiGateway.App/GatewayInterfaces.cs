using Microsoft.AspNetCore.Http;

namespace ApiGateway.App;

// ──────────────────── Service Interfaces ────────────────────

public interface IGatewayApiService
{
    Task<IReadOnlyList<GatewayApiItem>> ListApisAsync(CancellationToken ct);
    Task<GatewayApiDetail> GetApiDetailAsync(Guid apiId, CancellationToken ct);
    Task<GatewayApiItem> CreateApiAsync(CreateGatewayApiRequest request, CancellationToken ct);
    Task<GatewayApiItem> UpdateApiAsync(Guid apiId, UpdateGatewayApiRequest request, CancellationToken ct);
    Task DeleteApiAsync(Guid apiId, CancellationToken ct);
    Task SetApiEnabledAsync(Guid apiId, bool isEnabled, CancellationToken ct);
    /// <summary>比对前端提交的端点与现有库，返回新增/修改 diff</summary>
    Task<ImportDiffResult> GetImportDiffAsync(Guid apiId, ImportDiffRequest request, CancellationToken ct);
    /// <summary>应用用户确认的变更</summary>
    Task ApplyImportAsync(Guid apiId, ApplyImportRequest request, CancellationToken ct);
}

public interface IGatewayModuleService
{
    Task<IReadOnlyList<GatewayModuleItem>> ListModulesAsync(Guid apiId, CancellationToken ct);
    Task<GatewayModuleItem> CreateModuleAsync(Guid apiId, CreateGatewayModuleRequest request, CancellationToken ct);
    Task<GatewayModuleItem> UpdateModuleAsync(Guid moduleId, UpdateGatewayModuleRequest request, CancellationToken ct);
    Task DeleteModuleAsync(Guid moduleId, CancellationToken ct);
}

public interface IGatewayEndpointService
{
    Task<IReadOnlyList<GatewayEndpointItem>> ListEndpointsAsync(Guid moduleId, CancellationToken ct);
    Task<GatewayEndpointItem> CreateEndpointAsync(Guid moduleId, CreateGatewayEndpointRequest request, CancellationToken ct);
    Task<IReadOnlyList<GatewayEndpointItem>> BatchCreateEndpointsAsync(Guid moduleId, IReadOnlyList<CreateGatewayEndpointRequest> requests, CancellationToken ct);
    Task DeleteEndpointAsync(Guid endpointId, CancellationToken ct);
}

public interface IGatewayPermissionService
{
    Task<IReadOnlyList<GatewayPermissionItem>> ListPermissionsAsync(Guid? moduleId, CancellationToken ct);
    Task<GatewayPermissionItem> GrantAsync(CreateGatewayPermissionRequest request, CancellationToken ct);
    Task<IReadOnlyList<GatewayPermissionItem>> BatchGrantAsync(BatchGrantPermissionRequest request, CancellationToken ct);
    Task RevokeAsync(Guid permissionId, CancellationToken ct);
    Task SetEnabledAsync(Guid permissionId, bool isEnabled, CancellationToken ct);
}

public interface IGatewayAuditService
{
    Task<IReadOnlyList<GatewayAuditLogItem>> QueryAsync(Guid? apiId, Guid? moduleId, string? actorName, int take, CancellationToken ct);
}

public interface IGatewayProxyService
{
    Task<GatewayProxyResult> ProxyRequestAsync(GatewayProxyContext context, CancellationToken ct);
}

// ──────────────────── Proxy Models ────────────────────

public sealed record GatewayProxyContext(
    Guid ApiId,
    string ApiName,
    string ApiKey,
    string BaseUrl,
    Guid ModuleId,
    string ModuleName,
    string ModulePathPrefix,
    Guid? EndpointId,
    string HttpMethod,
    string UpstreamPath,
    Dictionary<string, string> MergedHeaders,
    string ActorType,
    string ActorId,
    string ActorName,
    string AuthenticationType,
    string? IpAddress,
    string? UserAgent,
    HttpRequest IncomingRequest);

public sealed record GatewayProxyResult(
    int StatusCode,
    byte[] Body,
    string? ContentType,
    Dictionary<string, string[]> ResponseHeaders,
    long DurationMs);
