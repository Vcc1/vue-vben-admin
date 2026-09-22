/*
  Auditing tables: security events and HTTP request audit logs.
  Run manually against the RoomMapping database. This script is idempotent and
  intentionally does not alter or reference existing business tables.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Auditing_SecurityEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Auditing_SecurityEvents
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Auditing_SecurityEvents PRIMARY KEY,
        EventType nvarchar(100) NOT NULL,
        Severity nvarchar(20) NOT NULL,
        Summary nvarchar(500) NULL,
        ActorType nvarchar(20) NOT NULL,
        ActorId nvarchar(100) NOT NULL,
        ActorName nvarchar(100) NOT NULL,
        AuthenticationType nvarchar(50) NOT NULL,
        IpAddress nvarchar(64) NULL,
        UserAgent nvarchar(512) NULL,
        CorrelationId nvarchar(100) NULL,
        DetailJson nvarchar(max) NULL,
        OccurredAtUtc datetime2(3) NOT NULL
    );
    CREATE INDEX IX_Auditing_SecurityEvents_Type_Time
        ON dbo.Auditing_SecurityEvents(EventType, OccurredAtUtc DESC);
    CREATE INDEX IX_Auditing_SecurityEvents_Time
        ON dbo.Auditing_SecurityEvents(OccurredAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.Auditing_RequestLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Auditing_RequestLogs
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Auditing_RequestLogs PRIMARY KEY,
        ActorType nvarchar(20) NOT NULL,
        ActorId nvarchar(100) NOT NULL,
        ActorName nvarchar(100) NOT NULL,
        AuthenticationType nvarchar(50) NOT NULL,
        IpAddress nvarchar(64) NULL,
        UserAgent nvarchar(512) NULL,
        CorrelationId nvarchar(100) NULL,
        HttpMethod nvarchar(10) NOT NULL,
        Path nvarchar(1000) NOT NULL,
        QueryString nvarchar(1000) NULL,
        ControllerName nvarchar(100) NULL,
        ActionName nvarchar(100) NULL,
        StatusCode int NOT NULL,
        DurationMs bigint NOT NULL,
        RequestBody nvarchar(max) NULL,
        IsRequestBodyTruncated bit NOT NULL CONSTRAINT DF_Auditing_RequestLogs_ReqTruncated DEFAULT (0),
        ResponseBody nvarchar(max) NULL,
        IsResponseBodyTruncated bit NOT NULL CONSTRAINT DF_Auditing_RequestLogs_RespTruncated DEFAULT (0),
        OccurredAtUtc datetime2(3) NOT NULL
    );
    CREATE INDEX IX_Auditing_RequestLogs_Time
        ON dbo.Auditing_RequestLogs(OccurredAtUtc DESC);
    CREATE INDEX IX_Auditing_RequestLogs_Actor_Time
        ON dbo.Auditing_RequestLogs(ActorId, OccurredAtUtc DESC);
END;

COMMIT TRANSACTION;
