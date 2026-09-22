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
    (N'paycardhub:cards:read',             N'虚拟卡查看',       N'查看虚拟卡'),
    (N'paycardhub:transactions:read',      N'交易查看',         N'查看交易记录'),
    (N'paycardhub:transactions:raw-query', N'原始交易查询',     N'查询供应商原始交易'),
    (N'paycardhub:transactions:backfill',  N'交易回填',         N'回填历史交易'),
    (N'paycardhub:cardholders:read',       N'持卡人查看',       N'查看持卡人'),
    (N'paycardhub:provider-accounts:read', N'供应商账户查看',   N'查看供应商账户'),
    (N'paycardhub:api-clients:read',       N'API客户端查看',    N'查看 API 客户端');

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
    (N'Admin',    N'*'),
    (N'Operator', N'paycardhub:cards:read'),
    (N'Operator', N'paycardhub:transactions:read'),
    (N'Operator', N'paycardhub:cardholders:read'),
    (N'Operator', N'paycardhub:provider-accounts:read'),
    (N'Viewer',   N'paycardhub:cards:read'),
    (N'Viewer',   N'paycardhub:transactions:read')
) AS v(RoleName, PermissionCode)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Identity_RolePermissions rp
    WHERE rp.RoleName = v.RoleName AND rp.PermissionCode = v.PermissionCode
);

COMMIT TRANSACTION;
