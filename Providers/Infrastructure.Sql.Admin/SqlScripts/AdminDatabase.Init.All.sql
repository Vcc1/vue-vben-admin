/*
  Complete Admin.Host initialization for ordinary SQL Server clients.
  This is plain T-SQL: SQLCMD mode is not required.

  Target database: RoomMapping
  Default user:    admin
  Default password: Admin@123456

  The script creates only dbo.Identity_* and dbo.Scheduler_* objects, then
  creates the default administrator when it does not already exist.
*/

USE [RoomMapping];

/*
  Admin.Host Identity and scheduler metadata tables.
  Run manually against the RoomMapping database. This script is idempotent and
  intentionally does not alter or reference existing business tables.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Identity_Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_Roles
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_Roles PRIMARY KEY,
        Name nvarchar(256) NULL,
        NormalizedName nvarchar(256) NULL,
        ConcurrencyStamp nvarchar(max) NULL
    );
    CREATE UNIQUE INDEX UX_Identity_Roles_NormalizedName
        ON dbo.Identity_Roles(NormalizedName) WHERE NormalizedName IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.Identity_Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_Users
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_Users PRIMARY KEY,
        DisplayName nvarchar(100) NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_Identity_Users_IsEnabled DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL,
        LastLoginAtUtc datetime2(3) NULL,
        UserName nvarchar(256) NULL,
        NormalizedUserName nvarchar(256) NULL,
        Email nvarchar(256) NULL,
        NormalizedEmail nvarchar(256) NULL,
        EmailConfirmed bit NOT NULL,
        PasswordHash nvarchar(max) NULL,
        SecurityStamp nvarchar(max) NULL,
        ConcurrencyStamp nvarchar(max) NULL,
        PhoneNumber nvarchar(max) NULL,
        PhoneNumberConfirmed bit NOT NULL,
        TwoFactorEnabled bit NOT NULL,
        LockoutEnd datetimeoffset NULL,
        LockoutEnabled bit NOT NULL,
        AccessFailedCount int NOT NULL
    );
    CREATE UNIQUE INDEX UX_Identity_Users_NormalizedUserName
        ON dbo.Identity_Users(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
    CREATE INDEX IX_Identity_Users_NormalizedEmail ON dbo.Identity_Users(NormalizedEmail);
END;

IF OBJECT_ID(N'dbo.Identity_RoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_RoleClaims
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Identity_RoleClaims PRIMARY KEY,
        RoleId uniqueidentifier NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT FK_Identity_RoleClaims_Roles FOREIGN KEY(RoleId)
            REFERENCES dbo.Identity_Roles(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Identity_RoleClaims_RoleId ON dbo.Identity_RoleClaims(RoleId);
END;

IF OBJECT_ID(N'dbo.Identity_UserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_UserClaims
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Identity_UserClaims PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        ClaimType nvarchar(max) NULL,
        ClaimValue nvarchar(max) NULL,
        CONSTRAINT FK_Identity_UserClaims_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Identity_UserClaims_UserId ON dbo.Identity_UserClaims(UserId);
END;

IF OBJECT_ID(N'dbo.Identity_UserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_UserLogins
    (
        LoginProvider nvarchar(128) NOT NULL,
        ProviderKey nvarchar(128) NOT NULL,
        ProviderDisplayName nvarchar(max) NULL,
        UserId uniqueidentifier NOT NULL,
        CONSTRAINT PK_Identity_UserLogins PRIMARY KEY(LoginProvider, ProviderKey),
        CONSTRAINT FK_Identity_UserLogins_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Identity_UserLogins_UserId ON dbo.Identity_UserLogins(UserId);
END;

IF OBJECT_ID(N'dbo.Identity_UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_UserRoles
    (
        UserId uniqueidentifier NOT NULL,
        RoleId uniqueidentifier NOT NULL,
        CONSTRAINT PK_Identity_UserRoles PRIMARY KEY(UserId, RoleId),
        CONSTRAINT FK_Identity_UserRoles_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Identity_UserRoles_Roles FOREIGN KEY(RoleId)
            REFERENCES dbo.Identity_Roles(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_Identity_UserRoles_RoleId ON dbo.Identity_UserRoles(RoleId);
END;

IF OBJECT_ID(N'dbo.Identity_UserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_UserTokens
    (
        UserId uniqueidentifier NOT NULL,
        LoginProvider nvarchar(128) NOT NULL,
        Name nvarchar(128) NOT NULL,
        Value nvarchar(max) NULL,
        CONSTRAINT PK_Identity_UserTokens PRIMARY KEY(UserId, LoginProvider, Name),
        CONSTRAINT FK_Identity_UserTokens_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'dbo.Identity_RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_RefreshTokens
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_RefreshTokens PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        TokenHash varchar(64) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        ExpiresAtUtc datetime2(3) NOT NULL,
        RevokedAtUtc datetime2(3) NULL,
        ReplacedByTokenId uniqueidentifier NULL,
        CreatedByIp nvarchar(64) NULL,
        CONSTRAINT FK_Identity_RefreshTokens_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_Identity_RefreshTokens_TokenHash
        ON dbo.Identity_RefreshTokens(TokenHash);
END;

IF OBJECT_ID(N'dbo.Identity_ApiKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_ApiKeys
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_ApiKeys PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        Name nvarchar(100) NOT NULL,
        Prefix nvarchar(24) NOT NULL,
        SecretHash varchar(64) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        ExpiresAtUtc datetime2(3) NULL,
        LastUsedAtUtc datetime2(3) NULL,
        RevokedAtUtc datetime2(3) NULL,
        CreatedByUserId uniqueidentifier NOT NULL,
        CONSTRAINT FK_Identity_ApiKeys_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id)
    );
    CREATE UNIQUE INDEX UX_Identity_ApiKeys_Prefix ON dbo.Identity_ApiKeys(Prefix);
END;

IF OBJECT_ID(N'dbo.Identity_UserAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_UserAuditLogs
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Identity_UserAuditLogs PRIMARY KEY,
        TargetUserId uniqueidentifier NULL,
        Action nvarchar(50) NOT NULL,
        ActorType nvarchar(20) NOT NULL,
        ActorId nvarchar(100) NOT NULL,
        ActorName nvarchar(100) NOT NULL,
        AuthenticationType nvarchar(50) NOT NULL,
        BeforeJson nvarchar(max) NULL,
        AfterJson nvarchar(max) NULL,
        OccurredAtUtc datetime2(3) NOT NULL,
        IpAddress nvarchar(64) NULL,
        UserAgent nvarchar(512) NULL,
        CorrelationId nvarchar(100) NULL
    );
    CREATE INDEX IX_Identity_UserAuditLogs_Target_Time
        ON dbo.Identity_UserAuditLogs(TargetUserId, OccurredAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.Scheduler_Tasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Scheduler_Tasks
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Scheduler_Tasks PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Description nvarchar(1000) NULL,
        TaskType varchar(20) NOT NULL,
        CronExpression varchar(120) NOT NULL,
        TimeZoneId nvarchar(100) NOT NULL,
        MisfirePolicy varchar(30) NOT NULL,
        IsEnabled bit NOT NULL,
        TimeoutSeconds int NOT NULL,
        RetryCount int NOT NULL,
        RetryIntervalSeconds int NOT NULL,
        RetryOnTimeout bit NOT NULL,
        ConfigurationJson nvarchar(max) NOT NULL,
        CreatedByActorId nvarchar(100) NOT NULL,
        CreatedByActorName nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        UpdatedByActorId nvarchar(100) NOT NULL,
        UpdatedByActorName nvarchar(100) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_Scheduler_Tasks_IsDeleted DEFAULT (0),
        RowVersion rowversion NOT NULL
    );
    CREATE UNIQUE INDEX UX_Scheduler_Tasks_Name
        ON dbo.Scheduler_Tasks(Name) WHERE IsDeleted = 0;
    CREATE INDEX IX_Scheduler_Tasks_Enabled
        ON dbo.Scheduler_Tasks(IsEnabled, IsDeleted);
END;

IF OBJECT_ID(N'dbo.Scheduler_TaskRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Scheduler_TaskRuns
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Scheduler_TaskRuns PRIMARY KEY,
        TaskId uniqueidentifier NOT NULL,
        RootRunId uniqueidentifier NOT NULL,
        AttemptNumber int NOT NULL,
        TriggerType varchar(30) NOT NULL,
        Status varchar(30) NOT NULL,
        ScheduledAtUtc datetime2(3) NULL,
        QueuedAtUtc datetime2(3) NOT NULL,
        StartedAtUtc datetime2(3) NULL,
        LastHeartbeatAtUtc datetime2(3) NULL,
        FinishedAtUtc datetime2(3) NULL,
        CancellationRequestedAtUtc datetime2(3) NULL,
        TriggeredByActorType nvarchar(20) NULL,
        TriggeredByActorId nvarchar(100) NULL,
        TriggeredByActorName nvarchar(100) NULL,
        AuthenticationType nvarchar(50) NULL,
        HostInstanceId nvarchar(200) NULL,
        QuartzFireInstanceId nvarchar(200) NULL,
        DefinitionSnapshotJson nvarchar(max) NOT NULL,
        ResultSummary nvarchar(max) NULL,
        ErrorSummary nvarchar(max) NULL,
        LogRelativePath nvarchar(300) NOT NULL,
        LogLengthBytes bigint NOT NULL CONSTRAINT DF_Scheduler_TaskRuns_LogLength DEFAULT (0),
        IsLogTruncated bit NOT NULL CONSTRAINT DF_Scheduler_TaskRuns_LogTruncated DEFAULT (0),
        LogDeletedAtUtc datetime2(3) NULL,
        CONSTRAINT FK_Scheduler_TaskRuns_Tasks FOREIGN KEY(TaskId)
            REFERENCES dbo.Scheduler_Tasks(Id)
    );
    CREATE INDEX IX_Scheduler_TaskRuns_Task_Time
        ON dbo.Scheduler_TaskRuns(TaskId, StartedAtUtc DESC);
    CREATE INDEX IX_Scheduler_TaskRuns_Status_Time
        ON dbo.Scheduler_TaskRuns(Status, StartedAtUtc DESC);
    CREATE INDEX IX_Scheduler_TaskRuns_RootRunId
        ON dbo.Scheduler_TaskRuns(RootRunId);
END;

IF OBJECT_ID(N'dbo.Scheduler_TaskAudits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Scheduler_TaskAudits
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Scheduler_TaskAudits PRIMARY KEY,
        TaskId uniqueidentifier NULL,
        RunId uniqueidentifier NULL,
        Action nvarchar(50) NOT NULL,
        ActorType nvarchar(20) NOT NULL,
        ActorId nvarchar(100) NOT NULL,
        ActorName nvarchar(100) NOT NULL,
        AuthenticationType nvarchar(50) NOT NULL,
        BeforeJson nvarchar(max) NULL,
        AfterJson nvarchar(max) NULL,
        OccurredAtUtc datetime2(3) NOT NULL,
        IpAddress nvarchar(64) NULL,
        UserAgent nvarchar(512) NULL,
        CorrelationId nvarchar(100) NULL
    );
    CREATE INDEX IX_Scheduler_TaskAudits_Task_Time
        ON dbo.Scheduler_TaskAudits(TaskId, OccurredAtUtc DESC);
    CREATE INDEX IX_Scheduler_TaskAudits_RunId ON dbo.Scheduler_TaskAudits(RunId);
END;

COMMIT TRANSACTION;

/* Quartz.NET SQL Server schema for table prefix dbo.Scheduler_QRTZ_. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_JOB_DETAILS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_JOB_DETAILS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    JOB_NAME nvarchar(150) NOT NULL,
    JOB_GROUP nvarchar(150) NOT NULL,
    DESCRIPTION nvarchar(250) NULL,
    JOB_CLASS_NAME nvarchar(250) NOT NULL,
    IS_DURABLE bit NOT NULL,
    IS_NONCONCURRENT bit NOT NULL,
    IS_UPDATE_DATA bit NOT NULL,
    REQUESTS_RECOVERY bit NOT NULL,
    JOB_DATA varbinary(max) NULL,
    CONSTRAINT PK_Scheduler_QRTZ_JOB_DETAILS PRIMARY KEY(SCHED_NAME, JOB_NAME, JOB_GROUP)
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_TRIGGERS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    TRIGGER_NAME nvarchar(150) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    JOB_NAME nvarchar(150) NOT NULL,
    JOB_GROUP nvarchar(150) NOT NULL,
    DESCRIPTION nvarchar(250) NULL,
    NEXT_FIRE_TIME bigint NULL,
    PREV_FIRE_TIME bigint NULL,
    PRIORITY int NULL,
    TRIGGER_STATE nvarchar(16) NOT NULL,
    TRIGGER_TYPE nvarchar(8) NOT NULL,
    START_TIME bigint NOT NULL,
    END_TIME bigint NULL,
    CALENDAR_NAME nvarchar(200) NULL,
    MISFIRE_INSTR smallint NULL,
    JOB_DATA varbinary(max) NULL,
    CONSTRAINT PK_Scheduler_QRTZ_TRIGGERS PRIMARY KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
    CONSTRAINT FK_Scheduler_QRTZ_TRIGGERS_JOBS FOREIGN KEY(SCHED_NAME, JOB_NAME, JOB_GROUP)
        REFERENCES dbo.Scheduler_QRTZ_JOB_DETAILS(SCHED_NAME, JOB_NAME, JOB_GROUP)
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_SIMPLE_TRIGGERS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_SIMPLE_TRIGGERS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    TRIGGER_NAME nvarchar(150) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    REPEAT_COUNT bigint NOT NULL,
    REPEAT_INTERVAL bigint NOT NULL,
    TIMES_TRIGGERED bigint NOT NULL,
    CONSTRAINT PK_Scheduler_QRTZ_SIMPLE_TRIGGERS PRIMARY KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
    CONSTRAINT FK_Scheduler_QRTZ_SIMPLE_TRIGGERS_TRIGGERS FOREIGN KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        REFERENCES dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_SIMPROP_TRIGGERS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_SIMPROP_TRIGGERS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    TRIGGER_NAME nvarchar(150) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    STR_PROP_1 nvarchar(512) NULL, STR_PROP_2 nvarchar(512) NULL, STR_PROP_3 nvarchar(512) NULL,
    INT_PROP_1 int NULL, INT_PROP_2 int NULL,
    LONG_PROP_1 bigint NULL, LONG_PROP_2 bigint NULL,
    DEC_PROP_1 numeric(13,4) NULL, DEC_PROP_2 numeric(13,4) NULL,
    BOOL_PROP_1 bit NULL, BOOL_PROP_2 bit NULL,
    TIME_ZONE_ID nvarchar(80) NULL,
    CONSTRAINT PK_Scheduler_QRTZ_SIMPROP_TRIGGERS PRIMARY KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
    CONSTRAINT FK_Scheduler_QRTZ_SIMPROP_TRIGGERS_TRIGGERS FOREIGN KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        REFERENCES dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_CRON_TRIGGERS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_CRON_TRIGGERS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    TRIGGER_NAME nvarchar(150) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    CRON_EXPRESSION nvarchar(120) NOT NULL,
    TIME_ZONE_ID nvarchar(80) NULL,
    CONSTRAINT PK_Scheduler_QRTZ_CRON_TRIGGERS PRIMARY KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
    CONSTRAINT FK_Scheduler_QRTZ_CRON_TRIGGERS_TRIGGERS FOREIGN KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        REFERENCES dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_BLOB_TRIGGERS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_BLOB_TRIGGERS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    TRIGGER_NAME nvarchar(150) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    BLOB_DATA varbinary(max) NULL,
    CONSTRAINT PK_Scheduler_QRTZ_BLOB_TRIGGERS PRIMARY KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
    CONSTRAINT FK_Scheduler_QRTZ_BLOB_TRIGGERS_TRIGGERS FOREIGN KEY(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
        REFERENCES dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_CALENDARS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_CALENDARS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    CALENDAR_NAME nvarchar(200) NOT NULL,
    CALENDAR varbinary(max) NOT NULL,
    CONSTRAINT PK_Scheduler_QRTZ_CALENDARS PRIMARY KEY(SCHED_NAME, CALENDAR_NAME)
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_PAUSED_TRIGGER_GRPS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_PAUSED_TRIGGER_GRPS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    CONSTRAINT PK_Scheduler_QRTZ_PAUSED_TRIGGER_GRPS PRIMARY KEY(SCHED_NAME, TRIGGER_GROUP)
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_FIRED_TRIGGERS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    ENTRY_ID nvarchar(140) NOT NULL,
    TRIGGER_NAME nvarchar(150) NOT NULL,
    TRIGGER_GROUP nvarchar(150) NOT NULL,
    INSTANCE_NAME nvarchar(200) NOT NULL,
    FIRED_TIME bigint NOT NULL,
    SCHED_TIME bigint NOT NULL,
    PRIORITY int NOT NULL,
    STATE nvarchar(16) NOT NULL,
    JOB_NAME nvarchar(150) NULL,
    JOB_GROUP nvarchar(150) NULL,
    IS_NONCONCURRENT bit NULL,
    REQUESTS_RECOVERY bit NULL,
    CONSTRAINT PK_Scheduler_QRTZ_FIRED_TRIGGERS PRIMARY KEY(SCHED_NAME, ENTRY_ID)
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_SCHEDULER_STATE', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_SCHEDULER_STATE
(
    SCHED_NAME nvarchar(120) NOT NULL,
    INSTANCE_NAME nvarchar(200) NOT NULL,
    LAST_CHECKIN_TIME bigint NOT NULL,
    CHECKIN_INTERVAL bigint NOT NULL,
    CONSTRAINT PK_Scheduler_QRTZ_SCHEDULER_STATE PRIMARY KEY(SCHED_NAME, INSTANCE_NAME)
);

IF OBJECT_ID(N'dbo.Scheduler_QRTZ_LOCKS', N'U') IS NULL
CREATE TABLE dbo.Scheduler_QRTZ_LOCKS
(
    SCHED_NAME nvarchar(120) NOT NULL,
    LOCK_NAME nvarchar(40) NOT NULL,
    CONSTRAINT PK_Scheduler_QRTZ_LOCKS PRIMARY KEY(SCHED_NAME, LOCK_NAME)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_NEXT_FIRE_TIME')
    CREATE INDEX IX_Scheduler_QRTZ_T_NEXT_FIRE_TIME ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, NEXT_FIRE_TIME);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_STATE')
    CREATE INDEX IX_Scheduler_QRTZ_T_STATE ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_STATE);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_FT_INSTANCE')
    CREATE INDEX IX_Scheduler_QRTZ_FT_INSTANCE ON dbo.Scheduler_QRTZ_FIRED_TRIGGERS(SCHED_NAME, INSTANCE_NAME);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_JOB')
    CREATE INDEX IX_Scheduler_QRTZ_T_JOB ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, JOB_NAME, JOB_GROUP);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_JOB_GROUP')
    CREATE INDEX IX_Scheduler_QRTZ_T_JOB_GROUP ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, JOB_GROUP);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_CALENDAR')
    CREATE INDEX IX_Scheduler_QRTZ_T_CALENDAR ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, CALENDAR_NAME);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_GROUP')
    CREATE INDEX IX_Scheduler_QRTZ_T_GROUP ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_GROUP);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_NAME_STATE')
    CREATE INDEX IX_Scheduler_QRTZ_T_NAME_STATE ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, TRIGGER_STATE);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_GROUP_STATE')
    CREATE INDEX IX_Scheduler_QRTZ_T_GROUP_STATE ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_GROUP, TRIGGER_STATE);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_STATE_NEXT')
    CREATE INDEX IX_Scheduler_QRTZ_T_STATE_NEXT ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_STATE, NEXT_FIRE_TIME);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_MISFIRE_NEXT')
    CREATE INDEX IX_Scheduler_QRTZ_T_MISFIRE_NEXT ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_MISFIRE_NEXT_STATE')
    CREATE INDEX IX_Scheduler_QRTZ_T_MISFIRE_NEXT_STATE ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME, TRIGGER_STATE);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_T_MISFIRE_NEXT_GROUP_STATE')
    CREATE INDEX IX_Scheduler_QRTZ_T_MISFIRE_NEXT_GROUP_STATE ON dbo.Scheduler_QRTZ_TRIGGERS(SCHED_NAME, MISFIRE_INSTR, NEXT_FIRE_TIME, TRIGGER_GROUP, TRIGGER_STATE);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_FT_INSTANCE_RECOVERY')
    CREATE INDEX IX_Scheduler_QRTZ_FT_INSTANCE_RECOVERY ON dbo.Scheduler_QRTZ_FIRED_TRIGGERS(SCHED_NAME, INSTANCE_NAME, REQUESTS_RECOVERY);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_FT_JOB')
    CREATE INDEX IX_Scheduler_QRTZ_FT_JOB ON dbo.Scheduler_QRTZ_FIRED_TRIGGERS(SCHED_NAME, JOB_NAME, JOB_GROUP);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_FT_JOB_GROUP')
    CREATE INDEX IX_Scheduler_QRTZ_FT_JOB_GROUP ON dbo.Scheduler_QRTZ_FIRED_TRIGGERS(SCHED_NAME, JOB_GROUP);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_FT_TRIGGER')
    CREATE INDEX IX_Scheduler_QRTZ_FT_TRIGGER ON dbo.Scheduler_QRTZ_FIRED_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Scheduler_QRTZ_FIRED_TRIGGERS') AND name = N'IX_Scheduler_QRTZ_FT_TRIGGER_GROUP')
    CREATE INDEX IX_Scheduler_QRTZ_FT_TRIGGER_GROUP ON dbo.Scheduler_QRTZ_FIRED_TRIGGERS(SCHED_NAME, TRIGGER_GROUP);

COMMIT TRANSACTION;

/*
  Bootstrap administrator for Admin.Host.

  User name: admin
  Password:  Admin@123456

  Run AdminDatabase.Init.All.sql first. This script is idempotent: it does not
  reset the password when the admin user already exists.
*/


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

/*
  Session management and graded login tables (Identity_Sessions_Tables.Init.sql).
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Identity_Sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_Sessions
    (
        Sid varchar(64) NOT NULL CONSTRAINT PK_Identity_Sessions PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        DeviceId nvarchar(64) NULL,
        DeviceInfoJson nvarchar(max) NULL,
        LoginMethod varchar(20) NOT NULL CONSTRAINT DF_Identity_Sessions_LoginMethod DEFAULT('pwd'),
        PasswordVerifiedAtUtc datetime2(3) NULL,
        StrongAuthAtUtc datetime2(3) NULL,
        StrongAuthMethod varchar(20) NULL,
        ClientIdsJson nvarchar(1000) NULL,
        CreatedByIp nvarchar(64) NULL,
        UserAgent nvarchar(512) NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        LastActivityAtUtc datetime2(3) NOT NULL,
        ExpiresAtUtc datetime2(3) NOT NULL,
        RevokedAtUtc datetime2(3) NULL,
        RevokedReason nvarchar(200) NULL,
        RevokedByActorId nvarchar(100) NULL,
        RevokedByActorName nvarchar(100) NULL
    );
    CREATE INDEX IX_Identity_Sessions_User_Revoked
        ON dbo.Identity_Sessions(UserId, RevokedAtUtc);
    CREATE INDEX IX_Identity_Sessions_DeviceId
        ON dbo.Identity_Sessions(DeviceId) WHERE DeviceId IS NOT NULL;
    CREATE INDEX IX_Identity_Sessions_LastActivity
        ON dbo.Identity_Sessions(LastActivityAtUtc);
END;

IF OBJECT_ID(N'dbo.Identity_TrustedDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_TrustedDevices
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_TrustedDevices PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        DeviceId nvarchar(64) NOT NULL,
        DeviceInfoJson nvarchar(max) NULL,
        FirstSeenUtc datetime2(3) NOT NULL,
        LastSeenUtc datetime2(3) NOT NULL,
        LastLoginSid varchar(64) NULL,
        Status varchar(20) NOT NULL CONSTRAINT DF_Identity_TrustedDevices_Status DEFAULT('trusted'),
        RevokedReason nvarchar(200) NULL,
        RevokedAtUtc datetime2(3) NULL,
        RevokedByActorId nvarchar(100) NULL,
        CONSTRAINT FK_Identity_TrustedDevices_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_Identity_TrustedDevices_User_Device
        ON dbo.Identity_TrustedDevices(UserId, DeviceId);
END;

IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'Sid') IS NULL
    ALTER TABLE dbo.Identity_RefreshTokens ADD Sid varchar(64) NULL;

IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'DeviceId') IS NULL
    ALTER TABLE dbo.Identity_RefreshTokens ADD DeviceId nvarchar(64) NULL;

IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'UserAgent') IS NULL
    ALTER TABLE dbo.Identity_RefreshTokens ADD UserAgent nvarchar(512) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Identity_RefreshTokens_Sid'
               AND object_id = OBJECT_ID(N'dbo.Identity_RefreshTokens'))
    CREATE INDEX IX_Identity_RefreshTokens_Sid
        ON dbo.Identity_RefreshTokens(Sid) WHERE Sid IS NOT NULL;

IF OBJECT_ID(N'dbo.IdentityOIDC_Applications', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'BackChannelLogoutUri') IS NULL
        ALTER TABLE dbo.IdentityOIDC_Applications ADD BackChannelLogoutUri nvarchar(400) NULL;

    IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'RequiredSessionLevel') IS NULL
        ALTER TABLE dbo.IdentityOIDC_Applications
            ADD RequiredSessionLevel int NOT NULL CONSTRAINT DF_IdentityOIDC_Applications_Level DEFAULT(1);
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
