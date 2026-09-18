/*
  Reset the existing admin password.

  User name: admin
  Password:  Admin@123456

  This is plain T-SQL and does not require SQLCMD mode.
  The password hash is an ASP.NET Core Identity V3 hash.
*/

USE [RoomMapping];

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();
DECLARE @userId uniqueidentifier;

SELECT @userId = Id
FROM dbo.Identity_Users WITH (UPDLOCK, HOLDLOCK)
WHERE NormalizedUserName = N'ADMIN';

IF @userId IS NULL
    THROW 51003, 'The admin user does not exist. Run the bootstrap administrator script first.', 1;

UPDATE dbo.Identity_Users
SET PasswordHash = N'AQAAAAIAAYagAAAAEKejj2WbJ6RIqLcH8wu37PChpLW7VPYiW+2Mr72PhODXguR7MEACrDQIcfLpS9W2BQ==',
    SecurityStamp = CONVERT(nvarchar(36), NEWID()),
    ConcurrencyStamp = CONVERT(nvarchar(36), NEWID()),
    IsEnabled = 1,
    LockoutEnd = NULL,
    AccessFailedCount = 0,
    UpdatedAtUtc = @now
WHERE Id = @userId;

IF OBJECT_ID(N'dbo.Identity_UserAuditLogs', N'U') IS NOT NULL
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
        N'ResetPassword',
        N'System',
        N'sql-bootstrap',
        N'sql-bootstrap',
        N'SqlBootstrap',
        NULL,
        N'{"userName":"admin","reason":"manual-reset"}',
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
    N'Admin@123456' AS TemporaryPassword,
    N'Password reset completed. Change it after login.' AS Result;
