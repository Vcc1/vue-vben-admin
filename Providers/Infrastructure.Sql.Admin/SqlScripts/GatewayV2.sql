-- =============================================
-- API Gateway V2 — 三级层次结构
-- Gateway_Apis → Gateway_Modules → Gateway_Endpoints
-- =============================================

-- 清理旧表
IF OBJECT_ID('dbo.Gateway_AuditLogs', 'U') IS NOT NULL DROP TABLE dbo.Gateway_AuditLogs;
IF OBJECT_ID('dbo.Gateway_Permissions', 'U') IS NOT NULL DROP TABLE dbo.Gateway_Permissions;
IF OBJECT_ID('dbo.Gateway_Endpoints', 'U') IS NOT NULL DROP TABLE dbo.Gateway_Endpoints;
IF OBJECT_ID('dbo.Gateway_Modules', 'U') IS NOT NULL DROP TABLE dbo.Gateway_Modules;
IF OBJECT_ID('dbo.Gateway_Apis', 'U') IS NOT NULL DROP TABLE dbo.Gateway_Apis;
IF OBJECT_ID('dbo.Gateway_Services', 'U') IS NOT NULL DROP TABLE dbo.Gateway_Services;
GO

-- 1. Gateway_Apis — Level 1: 上游站点
CREATE TABLE dbo.Gateway_Apis (
    Id              UNIQUEIDENTIFIER  NOT NULL PRIMARY KEY,
    Name            NVARCHAR(200)     NOT NULL,
    ApiKey          NVARCHAR(100)     NOT NULL,
    BaseUrl         NVARCHAR(500)     NOT NULL,
    HeadersJson     NVARCHAR(4000)    NULL,
    IsEnabled       BIT               NOT NULL DEFAULT 1,
    CreatedAtUtc    DATETIME2(3)      NOT NULL,
    UpdatedAtUtc    DATETIME2(3)      NOT NULL
);
CREATE UNIQUE INDEX IX_GatewayApis_ApiKey ON dbo.Gateway_Apis (ApiKey);
CREATE INDEX IX_GatewayApis_IsEnabled ON dbo.Gateway_Apis (IsEnabled);
GO

-- 2. Gateway_Modules — Level 2: 功能模块
CREATE TABLE dbo.Gateway_Modules (
    Id              UNIQUEIDENTIFIER  NOT NULL PRIMARY KEY,
    ApiId           UNIQUEIDENTIFIER  NOT NULL,
    Name            NVARCHAR(200)     NOT NULL,
    PathPrefix      NVARCHAR(500)     NOT NULL,
    HeadersJson     NVARCHAR(4000)    NULL,
    IsEnabled       BIT               NOT NULL DEFAULT 1,
    CreatedAtUtc    DATETIME2(3)      NOT NULL,
    CONSTRAINT FK_GatewayModules_Api FOREIGN KEY (ApiId) REFERENCES dbo.Gateway_Apis(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_GatewayModules_Api_PathPrefix ON dbo.Gateway_Modules (ApiId, PathPrefix);
CREATE INDEX IX_GatewayModules_Api_IsEnabled ON dbo.Gateway_Modules (ApiId, IsEnabled);
GO

-- 3. Gateway_Endpoints — Level 3: 具体接口
CREATE TABLE dbo.Gateway_Endpoints (
    Id              UNIQUEIDENTIFIER  NOT NULL PRIMARY KEY,
    ModuleId        UNIQUEIDENTIFIER  NOT NULL,
    HttpMethod      NVARCHAR(10)      NOT NULL,
    Path            NVARCHAR(500)     NOT NULL,
    OperationId     NVARCHAR(200)     NULL,
    Summary         NVARCHAR(1000)    NULL,
    IsEnabled       BIT               NOT NULL DEFAULT 1,
    CreatedAtUtc    DATETIME2(3)      NOT NULL,
    CONSTRAINT FK_GatewayEndpoints_Module FOREIGN KEY (ModuleId) REFERENCES dbo.Gateway_Modules(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_GatewayEndpoints_Module_Method_Path ON dbo.Gateway_Endpoints (ModuleId, HttpMethod, Path);
CREATE INDEX IX_GatewayEndpoints_Module_IsEnabled ON dbo.Gateway_Endpoints (ModuleId, IsEnabled);
GO

-- 4. Gateway_Permissions — 角色权限 (Module 级 + Endpoint 级)
CREATE TABLE dbo.Gateway_Permissions (
    Id              UNIQUEIDENTIFIER  NOT NULL PRIMARY KEY,
    RoleName        NVARCHAR(50)      NOT NULL,
    ModuleId        UNIQUEIDENTIFIER  NOT NULL,
    EndpointId      UNIQUEIDENTIFIER  NULL,   -- NULL = 该 Module 全部端点
    IsEnabled       BIT               NOT NULL DEFAULT 1,
    CreatedAtUtc    DATETIME2(3)      NOT NULL
);
CREATE UNIQUE INDEX IX_GatewayPermissions_Role_Module_Endpoint
    ON dbo.Gateway_Permissions (RoleName, ModuleId, EndpointId);
CREATE INDEX IX_GatewayPermissions_Module_IsEnabled
    ON dbo.Gateway_Permissions (ModuleId, IsEnabled);
GO

-- 5. Gateway_AuditLogs — 代理请求审计
CREATE TABLE dbo.Gateway_AuditLogs (
    Id                  BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ApiId               UNIQUEIDENTIFIER  NOT NULL,
    ApiName             NVARCHAR(200)     NOT NULL,
    ModuleId            UNIQUEIDENTIFIER  NOT NULL,
    ModuleName          NVARCHAR(200)     NOT NULL,
    EndpointId          UNIQUEIDENTIFIER  NULL,
    ActorType           NVARCHAR(20)      NOT NULL,
    ActorId             NVARCHAR(100)     NOT NULL,
    ActorName           NVARCHAR(100)     NOT NULL,
    AuthenticationType  NVARCHAR(50)      NOT NULL,
    HttpMethod          NVARCHAR(10)      NOT NULL,
    Path                NVARCHAR(1000)    NOT NULL,
    StatusCode          INT               NOT NULL,
    DurationMs          BIGINT            NULL,
    OccurredAtUtc       DATETIME2(3)      NOT NULL,
    IpAddress           NVARCHAR(64)      NULL,
    UserAgent           NVARCHAR(512)     NULL
);
CREATE INDEX IX_GatewayAuditLogs_Api_OccurredAt ON dbo.Gateway_AuditLogs (ApiId, OccurredAtUtc);
CREATE INDEX IX_GatewayAuditLogs_Module_OccurredAt ON dbo.Gateway_AuditLogs (ModuleId, OccurredAtUtc);
CREATE INDEX IX_GatewayAuditLogs_OccurredAt ON dbo.Gateway_AuditLogs (OccurredAtUtc);
GO
