namespace ApiGateway.App;

// ──────────────────── Level 1: API ────────────────────

public sealed record GatewayApiItem(
    Guid Id, string Name, string ApiKey, string BaseUrl,
    bool IsEnabled, int ModuleCount, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record GatewayApiDetail(
    Guid Id, string Name, string ApiKey, string BaseUrl,
    Dictionary<string, string>? Headers, bool IsEnabled,
    IReadOnlyList<GatewayModuleItem> Modules,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record CreateGatewayApiRequest(
    string Name, string BaseUrl, Dictionary<string, string>? Headers);

public sealed record UpdateGatewayApiRequest(
    string Name, string BaseUrl, Dictionary<string, string>? Headers);

// ──────────────────── OpenAPI Import (前端驱动) ────────────────────

/// <summary>前端解析 Swagger 后提交的用户勾选数据</summary>
public sealed record ImportDiffRequest(
    IReadOnlyList<ImportModuleItem> Modules);

/// <summary>前端提交的单个模块（含端点）</summary>
public sealed record ImportModuleItem(
    string Name,
    string PathPrefix,
    Dictionary<string, string>? Headers,
    IReadOnlyList<ImportEndpointItem> Endpoints);

/// <summary>前端提交的单个端点</summary>
public sealed record ImportEndpointItem(
    string HttpMethod,
    string Path,
    string? OperationId,
    string? Summary);

/// <summary>比对结果：按模块分组的新增/修改/删除</summary>
public sealed record ImportDiffResult(
    IReadOnlyList<ModuleDiffItem> Modules);

/// <summary>单个模块的比对详情</summary>
public sealed record ModuleDiffItem(
    /// <summary>"Add" | "Modify"</summary>
    string Action,
    Guid? ExistingModuleId,
    string Name,
    string PathPrefix,
    Dictionary<string, string>? Headers,
    IReadOnlyList<EndpointDiffItem> Endpoints);

/// <summary>单个端点的比对详情</summary>
public sealed record EndpointDiffItem(
    /// <summary>"Add" | "Modify"</summary>
    string Action,
    Guid? ExistingEndpointId,
    string HttpMethod,
    string Path,
    string? OperationId,
    string? Summary,
    /// <summary>现有权限数量（仅 Modify 时有值）</summary>
    int? CurrentPermissionCount);

// ──────────────────── Level 2: Module ────────────────────

public sealed record GatewayModuleItem(
    Guid Id, Guid ApiId, string Name, string PathPrefix,
    Dictionary<string, string>? Headers, int EndpointCount,
    bool IsEnabled, DateTime CreatedAtUtc);

public sealed record CreateGatewayModuleRequest(
    string Name, string PathPrefix, Dictionary<string, string>? Headers);

public sealed record UpdateGatewayModuleRequest(
    string Name, string PathPrefix, Dictionary<string, string>? Headers);

// ──────────────────── Level 3: Endpoint ────────────────────

public sealed record GatewayEndpointItem(
    Guid Id, Guid ModuleId, string HttpMethod, string Path,
    string? OperationId, string? Summary, bool IsEnabled, DateTime CreatedAtUtc);

public sealed record CreateGatewayEndpointRequest(
    string HttpMethod, string Path, string? OperationId, string? Summary);

// ──────────────────── Import Apply ────────────────────

/// <summary>用户确认后的应用请求</summary>
public sealed record ApplyImportRequest(
    IReadOnlyList<ApplyModuleItem> Modules,
    /// <summary>需要删除的现有模块 ID（未包含在提交中的旧模块）</summary>
    IReadOnlyList<Guid> DeleteModuleIds);

/// <summary>要应用的模块变更</summary>
public sealed record ApplyModuleItem(
    /// <summary>"Add" | "Modify"</summary>
    string Action,
    Guid? ExistingModuleId,
    string Name,
    string PathPrefix,
    Dictionary<string, string>? Headers,
    IReadOnlyList<ApplyEndpointItem> Endpoints);

/// <summary>要应用的端点变更</summary>
public sealed record ApplyEndpointItem(
    /// <summary>"Add" | "Modify"</summary>
    string Action,
    Guid? ExistingEndpointId,
    string HttpMethod,
    string Path,
    string? OperationId,
    string? Summary);

// ──────────────────── Permission ────────────────────

public sealed record GatewayPermissionItem(
    Guid Id, string RoleName, Guid ModuleId, string ModuleName,
    Guid? EndpointId, string? EndpointSpec, bool IsEnabled, DateTime CreatedAtUtc);

public sealed record CreateGatewayPermissionRequest(
    string RoleName, Guid ModuleId, Guid? EndpointId);

public sealed record BatchGrantPermissionRequest(
    string RoleName, IReadOnlyList<Guid> ModuleIds);

// ──────────────────── Audit ────────────────────

public sealed record GatewayAuditLogItem(
    long Id, Guid ApiId, string ApiName, Guid ModuleId, string ModuleName,
    Guid? EndpointId, string ActorType, string ActorId, string ActorName,
    string HttpMethod, string Path, int StatusCode, long? DurationMs,
    DateTime OccurredAtUtc, string? IpAddress);
