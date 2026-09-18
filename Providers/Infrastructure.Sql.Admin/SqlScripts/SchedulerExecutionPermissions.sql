/*
  Run after the RoomMappingSchedulerExecution server login exists.
  This creates the database user, grants business-table DML, and explicitly
  protects Admin.Host metadata, Quartz metadata, and SupHotelInfo.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @user sysname = N'RoomMappingSchedulerExecution';
IF SUSER_ID(@user) IS NULL
    THROW 51000, 'The scheduler execution login does not exist.', 1;

IF DATABASE_PRINCIPAL_ID(@user) IS NULL
    EXEC(N'CREATE USER ' + QUOTENAME(@user) + N' FOR LOGIN ' + QUOTENAME(@user) + N';');

EXEC(N'GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO ' + QUOTENAME(@user) + N';');
EXEC(N'DENY ALTER, CONTROL ON SCHEMA::dbo TO ' + QUOTENAME(@user) + N';');
EXEC(N'DENY CREATE TABLE, CREATE VIEW, CREATE PROCEDURE, CREATE FUNCTION TO ' + QUOTENAME(@user) + N';');
EXEC(N'DENY ALTER ANY SCHEMA TO ' + QUOTENAME(@user) + N';');

DECLARE @protectedTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @protectedTables(TableName)
VALUES
    (N'Identity_Users'),
    (N'Identity_Roles'),
    (N'Identity_UserRoles'),
    (N'Identity_UserClaims'),
    (N'Identity_UserLogins'),
    (N'Identity_RoleClaims'),
    (N'Identity_UserTokens'),
    (N'Identity_RefreshTokens'),
    (N'Identity_ApiKeys'),
    (N'Identity_UserAuditLogs'),
    (N'Scheduler_Tasks'),
    (N'Scheduler_TaskRuns'),
    (N'Scheduler_TaskAudits'),
    (N'Scheduler_QRTZ_JOB_DETAILS'),
    (N'Scheduler_QRTZ_TRIGGERS'),
    (N'Scheduler_QRTZ_SIMPLE_TRIGGERS'),
    (N'Scheduler_QRTZ_SIMPROP_TRIGGERS'),
    (N'Scheduler_QRTZ_CRON_TRIGGERS'),
    (N'Scheduler_QRTZ_BLOB_TRIGGERS'),
    (N'Scheduler_QRTZ_CALENDARS'),
    (N'Scheduler_QRTZ_PAUSED_TRIGGER_GRPS'),
    (N'Scheduler_QRTZ_FIRED_TRIGGERS'),
    (N'Scheduler_QRTZ_SCHEDULER_STATE'),
    (N'Scheduler_QRTZ_LOCKS'),
    (N'SupHotelInfo');

DECLARE @tableName sysname;
DECLARE protected_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName FROM @protectedTables
    WHERE OBJECT_ID(N'dbo.' + TableName, N'U') IS NOT NULL;

OPEN protected_cursor;
FETCH NEXT FROM protected_cursor INTO @tableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC(N'DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.'
        + QUOTENAME(@tableName) + N' TO ' + QUOTENAME(@user) + N';');
    FETCH NEXT FROM protected_cursor INTO @tableName;
END;
CLOSE protected_cursor;
DEALLOCATE protected_cursor;

COMMIT TRANSACTION;
