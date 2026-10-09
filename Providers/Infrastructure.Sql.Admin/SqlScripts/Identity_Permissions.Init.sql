/*
  Admin.Host 角色权限落库脚本。
  手动对 RoomMapping 数据库执行。脚本幂等，可重复运行。

  设计：全部使用自然键（权限用 Code、角色用 RoleName），不含任何 Guid 主键，
        因此可以把线下的权限/关系数据直接导入线上，不会因 Guid 不一致产生版本冲突。
        Admin.Host 通过 RolePermissionCatalog 定时读取到内存缓存；代码中不再保留默认映射。
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Identity_Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_Permissions
    (
        Code nvarchar(100) NOT NULL CONSTRAINT PK_Identity_Permissions PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Description nvarchar(500) NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_Identity_Permissions_IsEnabled DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Identity_Permissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Identity_Permissions_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END;

IF OBJECT_ID(N'dbo.Identity_RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_RolePermissions
    (
        RoleName nvarchar(256) NOT NULL,
        PermissionCode nvarchar(100) NOT NULL,
        CONSTRAINT PK_Identity_RolePermissions PRIMARY KEY(RoleName, PermissionCode)
    );
END;

-- 旧表升级：先解除外键，再移除目录中的旧通配符；角色上的 * 授权原样保留。
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Identity_RolePermissions_Permissions'
    AND parent_object_id = OBJECT_ID(N'dbo.Identity_RolePermissions'))
    ALTER TABLE dbo.Identity_RolePermissions DROP CONSTRAINT FK_Identity_RolePermissions_Permissions;

IF COL_LENGTH(N'dbo.Identity_Permissions', N'Description') IS NULL
    ALTER TABLE dbo.Identity_Permissions ADD Description nvarchar(500) NULL;

IF COL_LENGTH(N'dbo.Identity_Permissions', N'GroupName') IS NOT NULL
    ALTER TABLE dbo.Identity_Permissions DROP COLUMN GroupName;

DELETE FROM dbo.Identity_Permissions WHERE Code = N'*';

-- 目录只存具体权限，层级由 Code 的冒号分段推导，授权表可存 * 和前缀:*。
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Identity_Permissions_Code'
    AND parent_object_id = OBJECT_ID(N'dbo.Identity_Permissions'))
    ALTER TABLE dbo.Identity_Permissions ADD CONSTRAINT CK_Identity_Permissions_Code CHECK
        (LEN(Code) > 0 AND CHARINDEX(N'*', Code) = 0
         AND LEFT(Code, 1) <> N':' AND RIGHT(Code, 1) <> N':' AND Code NOT LIKE N'%::%');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Identity_RolePermissions_Code'
    AND parent_object_id = OBJECT_ID(N'dbo.Identity_RolePermissions'))
    ALTER TABLE dbo.Identity_RolePermissions ADD CONSTRAINT CK_Identity_RolePermissions_Code CHECK
        (LEN(PermissionCode) > 0 AND PermissionCode NOT LIKE N'%::%'
         AND (PermissionCode = N'*' OR
              (LEFT(PermissionCode, 1) <> N':' AND
               ((CHARINDEX(N'*', PermissionCode) = 0 AND RIGHT(PermissionCode, 1) <> N':') OR
                (RIGHT(PermissionCode, 2) = N':*' AND
                 CHARINDEX(N'*', LEFT(PermissionCode, LEN(PermissionCode) - 1)) = 0)))));
GO
SET XACT_ABORT ON;

-- 目录只存具体权限，层级由 Code 的冒号分段推导，授权表可存 * 和前缀:*。
-- 用表变量做 upsert：新增缺失的码，并回填已存在行的 Name/Description（便于线下数据反复导入线上）。
-- 注意：上面新增 Description 列的 ALTER 必须与下面引用它的 DML 用 GO 分批，否则同批编译会报“列名无效”。
DECLARE @perm TABLE (Code nvarchar(100), Name nvarchar(200), Description nvarchar(500));
INSERT INTO @perm(Code, Name, Description) VALUES
    -- paycardhub 权限
    (N'paycardhub:cards:read',             N'虚拟卡查看',       N'查看虚拟卡'),
    (N'paycardhub:transactions:read',      N'交易查看',         N'查看交易记录'),
    (N'paycardhub:transactions:raw-query', N'原始交易查询',     N'查询供应商原始交易'),
    (N'paycardhub:transactions:backfill',  N'交易回填',         N'回填历史交易'),
    (N'paycardhub:cardholders:read',       N'持卡人查看',       N'查看持卡人'),
    (N'paycardhub:provider-accounts:read', N'供应商账户查看',   N'查看供应商账户'),
    (N'paycardhub:api-clients:read',       N'API客户端查看',    N'查看 API 客户端'),
    -- admin 用户管理
    (N'admin:users:read',                  N'用户查看',         N'查看用户列表和详情'),
    (N'admin:users:create',                N'用户创建',         N'创建新用户'),
    (N'admin:users:update',                N'用户编辑',         N'编辑用户信息'),
    (N'admin:users:delete',                N'用户删除',         N'删除用户'),
    -- admin 角色管理
    (N'admin:roles:read',                  N'角色查看',         N'查看角色列表'),
    (N'admin:roles:create',                N'角色创建',         N'创建新角色'),
    (N'admin:roles:update',                N'角色编辑',         N'编辑角色信息'),
    (N'admin:roles:delete',                N'角色删除',         N'删除角色'),
    (N'admin:roles:assign',                N'角色授权',         N'给角色分配权限'),
    -- admin 权限管理
    (N'admin:permissions:read',            N'权限查看',         N'查看权限列表'),
    (N'admin:permissions:create',          N'权限创建',         N'创建新权限'),
    (N'admin:permissions:update',          N'权限编辑',         N'编辑权限信息'),
    (N'admin:permissions:delete',          N'权限删除',         N'删除权限'),
    -- admin 会话管理
    (N'admin:sessions:read',               N'会话查看',         N'查看在线会话'),
    (N'admin:sessions:revoke',             N'会话吊销',         N'吊销用户会话'),
    -- admin OIDC 客户端管理
    (N'admin:oidc:clients:read',           N'OIDC客户端查看',   N'查看OIDC客户端列表'),
    (N'admin:oidc:clients:create',         N'OIDC客户端创建',   N'创建OIDC客户端'),
    (N'admin:oidc:clients:update',         N'OIDC客户端编辑',   N'编辑OIDC客户端'),
    (N'admin:oidc:clients:delete',         N'OIDC客户端删除',   N'删除OIDC客户端'),
    -- admin OIDC 作用域管理
    (N'admin:oidc:scopes:read',            N'OIDC作用域查看',   N'查看OIDC作用域列表'),
    (N'admin:oidc:scopes:create',          N'OIDC作用域创建',   N'创建OIDC作用域'),
    (N'admin:oidc:scopes:update',          N'OIDC作用域编辑',   N'编辑OIDC作用域'),
    (N'admin:oidc:scopes:delete',          N'OIDC作用域删除',   N'删除OIDC作用域'),
    -- admin 定时任务管理
    (N'admin:scheduling:read',             N'任务查看',         N'查看定时任务列表'),
    (N'admin:scheduling:create',           N'任务创建',         N'创建定时任务'),
    (N'admin:scheduling:update',           N'任务编辑',         N'编辑定时任务'),
    (N'admin:scheduling:delete',           N'任务删除',         N'删除定时任务'),
    (N'admin:scheduling:trigger',          N'任务触发',         N'手动触发定时任务'),
    -- admin 审计日志
    (N'admin:audit:read',                  N'审计日志查看',     N'查看安全和操作审计日志'),
    -- admin 通知管理
    (N'admin:notification:read',           N'通知查看',         N'查看通知列表'),
    (N'admin:notification:create',         N'通知创建',         N'创建通知'),
    (N'admin:notification:update',         N'通知编辑',         N'编辑通知'),
    (N'admin:notification:delete',         N'通知删除',         N'删除通知'),
    (N'admin:notification:publish',        N'通知发布',         N'发布通知'),
    -- admin 指标查看
    (N'admin:observability:read',          N'指标查看',         N'查看服务器指标和日志');

INSERT INTO dbo.Identity_Permissions(Code, Name, Description)
SELECT v.Code, v.Name, v.Description
FROM @perm v
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Identity_Permissions p WHERE p.Code = v.Code
);

UPDATE p SET p.Name = v.Name, p.Description = v.Description, p.UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.Identity_Permissions p
JOIN @perm v ON p.Code = v.Code
WHERE p.Name <> v.Name OR ISNULL(p.Description, N'') <> ISNULL(v.Description, N'');

-- 角色 → 权限关系（直接用角色名，无需依赖 Identity_Roles 的 Guid）
INSERT INTO dbo.Identity_RolePermissions(RoleName, PermissionCode)
SELECT v.RoleName, v.PermissionCode
FROM (VALUES
    -- Admin 拥有所有权限
    (N'Admin',    N'*'),
    -- Operator: paycardhub 基础权限 + admin 查看+部分操作权限
    (N'Operator', N'paycardhub:cards:read'),
    (N'Operator', N'paycardhub:transactions:read'),
    (N'Operator', N'paycardhub:cardholders:read'),
    (N'Operator', N'paycardhub:provider-accounts:read'),
    (N'Operator', N'admin:users:read'),
    (N'Operator', N'admin:roles:read'),
    (N'Operator', N'admin:permissions:read'),
    (N'Operator', N'admin:sessions:read'),
    (N'Operator', N'admin:sessions:revoke'),
    (N'Operator', N'admin:oidc:clients:read'),
    (N'Operator', N'admin:oidc:scopes:read'),
    (N'Operator', N'admin:scheduling:read'),
    (N'Operator', N'admin:scheduling:trigger'),
    (N'Operator', N'admin:audit:read'),
    (N'Operator', N'admin:notification:read'),
    (N'Operator', N'admin:notification:publish'),
    (N'Operator', N'admin:observability:read'),
    -- Viewer: paycardhub 只读 + admin 全部只读权限
    (N'Viewer',   N'paycardhub:cards:read'),
    (N'Viewer',   N'paycardhub:transactions:read'),
    (N'Viewer',   N'admin:users:read'),
    (N'Viewer',   N'admin:roles:read'),
    (N'Viewer',   N'admin:permissions:read'),
    (N'Viewer',   N'admin:sessions:read'),
    (N'Viewer',   N'admin:oidc:clients:read'),
    (N'Viewer',   N'admin:oidc:scopes:read'),
    (N'Viewer',   N'admin:scheduling:read'),
    (N'Viewer',   N'admin:audit:read'),
    (N'Viewer',   N'admin:notification:read'),
    (N'Viewer',   N'admin:observability:read')
) AS v(RoleName, PermissionCode)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Identity_RolePermissions rp
    WHERE rp.RoleName = v.RoleName AND rp.PermissionCode = v.PermissionCode
);

COMMIT TRANSACTION;
