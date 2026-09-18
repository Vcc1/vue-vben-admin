/*
  Bootstrap administrator for Admin.Host.

  User name: admin
  Password:  Admin@123456

  Run AdminDatabase.Init.All.sql first. This script is idempotent: it does not
  reset the password when the admin user already exists.
*/

USE [RoomMapping];

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Identity_Users', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Identity_Roles', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Identity_UserRoles', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Identity_UserAuditLogs', N'U') IS NULL
BEGIN
    THROW 51002, 'Admin.Host Identity tables do not exist. Run AdminDatabase.Init.All.sql first.', 1;
END;

DECLARE @now datetime2(3) = SYSUTCDATETIME();
DECLARE @userId uniqueidentifier;
DECLARE @roleId uniqueidentifier;
DECLARE @userCreated bit = 0;
DECLARE @roleAssigned bit = 0;

SELECT @roleId = Id
FROM dbo.Identity_Roles WITH (UPDLOCK, HOLDLOCK)
WHERE NormalizedName = N'ADMIN';

IF @roleId IS NULL
BEGIN
    SET @roleId = NEWID();
    INSERT INTO dbo.Identity_Roles
    (
        Id,
        Name,
        NormalizedName,
        ConcurrencyStamp
    )
    VALUES
    (
        @roleId,
        N'Admin',
        N'ADMIN',
        CONVERT(nvarchar(36), NEWID())
    );
END;

SELECT @userId = Id
FROM dbo.Identity_Users WITH (UPDLOCK, HOLDLOCK)
WHERE NormalizedUserName = N'ADMIN';

IF @userId IS NULL
BEGIN
    SET @userId = NEWID();
    SET @userCreated = 1;

    INSERT INTO dbo.Identity_Users
    (
        Id,
        DisplayName,
        IsEnabled,
        CreatedAtUtc,
        UpdatedAtUtc,
        LastLoginAtUtc,
        UserName,
        NormalizedUserName,
        Email,
        NormalizedEmail,
        EmailConfirmed,
        PasswordHash,
        SecurityStamp,
        ConcurrencyStamp,
        PhoneNumber,
        PhoneNumberConfirmed,
        TwoFactorEnabled,
        LockoutEnd,
        LockoutEnabled,
        AccessFailedCount
    )
    VALUES
    (
        @userId,
        N'系统管理员',
        1,
        @now,
        @now,
        NULL,
        N'admin',
        N'ADMIN',
        NULL,
        NULL,
        0,
        N'AQAAAAIAAYagAAAAEKejj2WbJ6RIqLcH8wu37PChpLW7VPYiW+2Mr72PhODXguR7MEACrDQIcfLpS9W2BQ==',
        CONVERT(nvarchar(36), NEWID()),
        CONVERT(nvarchar(36), NEWID()),
        NULL,
        0,
        0,
        NULL,
        1,
        0
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.Identity_UserRoles WITH (UPDLOCK, HOLDLOCK)
    WHERE UserId = @userId
      AND RoleId = @roleId
)
BEGIN
    INSERT INTO dbo.Identity_UserRoles(UserId, RoleId)
    VALUES(@userId, @roleId);
    SET @roleAssigned = 1;
END;

IF @userCreated = 1 OR @roleAssigned = 1
BEGIN
    INSERT INTO dbo.Identity_UserAuditLogs
    (
        TargetUserId,
        Action,
        ActorType,
        ActorId,
        ActorName,
        AuthenticationType,
        BeforeJson,
        AfterJson,
        OccurredAtUtc,
        IpAddress,
        UserAgent,
        CorrelationId
    )
    VALUES
    (
        @userId,
        CASE WHEN @userCreated = 1 THEN N'BootstrapCreate' ELSE N'BootstrapAssignAdminRole' END,
        N'System',
        N'sql-bootstrap',
        N'sql-bootstrap',
        N'SqlBootstrap',
        NULL,
        N'{"userName":"admin","roles":["Admin"]}',
        @now,
        NULL,
        NULL,
        NULL
    );
END;

COMMIT TRANSACTION;

SELECT
    @userId AS UserId,
    N'admin' AS UserName,
    CAST(@userCreated AS bit) AS UserCreated,
    CAST(@roleAssigned AS bit) AS AdminRoleAssigned,
    CASE
        WHEN @userCreated = 1 THEN N'Password is Admin@123456. Change it immediately after login.'
        ELSE N'Existing password was not changed.'
    END AS Result;
