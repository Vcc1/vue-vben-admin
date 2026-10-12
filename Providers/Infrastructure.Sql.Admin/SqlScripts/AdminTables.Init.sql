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

IF OBJECT_ID(N'dbo.Notify_Types', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notify_Types
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Notify_Types PRIMARY KEY,
        TypeCode varchar(50) NOT NULL,
        DisplayName nvarchar(100) NOT NULL,
        Description nvarchar(500) NULL,
        AllowedChannelsJson nvarchar(500) NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_Notify_Types_IsEnabled DEFAULT (1),
        IsSystem bit NOT NULL CONSTRAINT DF_Notify_Types_IsSystem DEFAULT (0),
        CreatedByActorId nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        UpdatedByActorId nvarchar(100) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_Notify_Types_IsDeleted DEFAULT (0),
        RowVersion rowversion NOT NULL
    );
    CREATE UNIQUE INDEX UX_Notify_Types_TypeCode
        ON dbo.Notify_Types(TypeCode) WHERE IsDeleted = 0;
    CREATE INDEX IX_Notify_Types_Enabled
        ON dbo.Notify_Types(IsEnabled, IsDeleted);
END;

IF OBJECT_ID(N'dbo.Notify_Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notify_Notifications
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Notify_Notifications PRIMARY KEY,
        NotificationTypeId uniqueidentifier NOT NULL,
        Title nvarchar(200) NOT NULL,
        Summary nvarchar(500) NOT NULL,
        ContentJson nvarchar(max) NOT NULL,
        LinkUrl nvarchar(500) NULL,
        Scope varchar(20) NOT NULL,
        TargetIdsJson nvarchar(4000) NOT NULL,
        Source varchar(20) NOT NULL,
        SourceModule nvarchar(100) NULL,
        SourceEventKey nvarchar(200) NULL,
        CreatedByActorId nvarchar(100) NOT NULL,
        CreatedByActorName nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        ExpiresAtUtc datetime2(3) NULL,
        CONSTRAINT FK_Notify_Notifications_Types FOREIGN KEY(NotificationTypeId)
            REFERENCES dbo.Notify_Types(Id)
    );
    CREATE INDEX IX_Notify_Notifications_CreatedAt
        ON dbo.Notify_Notifications(CreatedAtUtc DESC);
    CREATE INDEX IX_Notify_Notifications_Source
        ON dbo.Notify_Notifications(SourceModule, SourceEventKey);
END;

IF OBJECT_ID(N'dbo.Notify_Recipients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notify_Recipients
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notify_Recipients PRIMARY KEY,
        NotificationId uniqueidentifier NOT NULL,
        UserId uniqueidentifier NOT NULL,
        IsRead bit NOT NULL CONSTRAINT DF_Notify_Recipients_IsRead DEFAULT (0),
        ReadAtUtc datetime2(3) NULL,
        DeliveredChannelsJson nvarchar(200) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        CONSTRAINT FK_Notify_Recipients_Notifications FOREIGN KEY(NotificationId)
            REFERENCES dbo.Notify_Notifications(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_Notify_Recipients_Notification_User
        ON dbo.Notify_Recipients(NotificationId, UserId);
    CREATE INDEX IX_Notify_Recipients_User_Read_Time
        ON dbo.Notify_Recipients(UserId, IsRead, CreatedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.Notify_UserPreferences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notify_UserPreferences
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Notify_UserPreferences PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        NotificationTypeId uniqueidentifier NOT NULL,
        EnabledChannelsJson nvarchar(200) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL,
        CONSTRAINT FK_Notify_UserPreferences_Types FOREIGN KEY(NotificationTypeId)
            REFERENCES dbo.Notify_Types(Id)
    );
    CREATE UNIQUE INDEX UX_Notify_UserPreferences_User_Type
        ON dbo.Notify_UserPreferences(UserId, NotificationTypeId);
END;

COMMIT TRANSACTION;
