/*
  Hotel MatchingPipeline - permanent pair-level vetoes and Redis sync state.
  Run manually in the RoomMapping database after SupHotelIdMigration.sql.
  This script is idempotent; the application never executes it.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.HotelMatchVeto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelMatchVeto
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelMatchVeto PRIMARY KEY,
        SupHotelId int NOT NULL,
        MTHotelCode varchar(100) NOT NULL,
        Operator nvarchar(100) NOT NULL,
        Reason nvarchar(500) NOT NULL,
        RedisSyncAttemptCount int NOT NULL
            CONSTRAINT DF_HotelMatchVeto_RedisSyncAttemptCount DEFAULT (0),
        FirstRedisSyncFailureTime datetime2(3) NULL,
        LastRedisSyncAttemptTime datetime2(3) NULL,
        LastRedisSyncError nvarchar(4000) NULL,
        FailureAlertSentTime datetime2(3) NULL,
        RedisSyncedTime datetime2(3) NULL,
        RecoveryAlertSentTime datetime2(3) NULL,
        CreateTime datetime2(3) NOT NULL
            CONSTRAINT DF_HotelMatchVeto_CreateTime DEFAULT (sysdatetime()),
        UpdateTime datetime2(3) NOT NULL
            CONSTRAINT DF_HotelMatchVeto_UpdateTime DEFAULT (sysdatetime()),
        CONSTRAINT FK_HotelMatchVeto_SupHotelInfo
            FOREIGN KEY (SupHotelId)
            REFERENCES dbo.SupHotelInfo(SupHotelId)
            ON DELETE CASCADE
    ) ON [CACHE];
END;

IF EXISTS
(
    SELECT 1
    FROM sys.extended_properties
    WHERE class = 1
      AND major_id = OBJECT_ID(N'dbo.HotelMatchVeto')
      AND minor_id = 0
      AND name = N'MS_Description'
)
    EXEC sys.sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'酒店匹配永久否决记录及 Redis 同步状态；同一供应商酒店与主酒店配对只能否决一次',
        @level0type = N'SCHEMA',
        @level0name = N'dbo',
        @level1type = N'TABLE',
        @level1name = N'HotelMatchVeto';
ELSE
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'酒店匹配永久否决记录及 Redis 同步状态；同一供应商酒店与主酒店配对只能否决一次',
        @level0type = N'SCHEMA',
        @level0name = N'dbo',
        @level1type = N'TABLE',
        @level1name = N'HotelMatchVeto';

DECLARE @VetoColumnDescriptions TABLE
(
    ColumnName sysname NOT NULL PRIMARY KEY,
    [Description] nvarchar(400) NOT NULL
);

INSERT INTO @VetoColumnDescriptions(ColumnName, [Description])
VALUES
    (N'Id', N'酒店匹配否决记录主键'),
    (N'SupHotelId', N'供应商酒店内部ID，对应 SupHotelInfo.SupHotelId'),
    (N'MTHotelCode', N'被永久否决匹配的梦途主酒店编码'),
    (N'Operator', N'执行人工否决的操作人'),
    (N'Reason', N'人工否决原因'),
    (N'RedisSyncAttemptCount', N'Redis 同步累计尝试次数，失败和成功尝试均计数'),
    (N'FirstRedisSyncFailureTime', N'Redis 首次同步失败时间；从未失败时为空'),
    (N'LastRedisSyncAttemptTime', N'Redis 最近一次同步尝试时间'),
    (N'LastRedisSyncError', N'Redis 最近一次同步失败的异常信息；同步成功后清空'),
    (N'FailureAlertSentTime', N'Redis 首次失败告警成功发送时间；未发送成功时为空'),
    (N'RedisSyncedTime', N'Redis 中该供应商酒店配对删除成功的时间；未完成时为空'),
    (N'RecoveryAlertSentTime', N'Redis 恢复通知成功发送时间；无需通知或未发送成功时为空'),
    (N'CreateTime', N'否决记录创建及数据库否决事务完成时间'),
    (N'UpdateTime', N'否决记录最后更新时间，包括 Redis 同步和告警状态变更');

DECLARE @VetoColumnName sysname;
DECLARE @VetoColumnDescription nvarchar(400);

WHILE EXISTS (SELECT 1 FROM @VetoColumnDescriptions)
BEGIN
    SELECT TOP (1)
        @VetoColumnName = ColumnName,
        @VetoColumnDescription = [Description]
    FROM @VetoColumnDescriptions
    ORDER BY ColumnName;

    IF EXISTS
    (
        SELECT 1
        FROM sys.extended_properties
        WHERE class = 1
          AND major_id = OBJECT_ID(N'dbo.HotelMatchVeto')
          AND minor_id = COLUMNPROPERTY(
              OBJECT_ID(N'dbo.HotelMatchVeto'),
              @VetoColumnName,
              N'ColumnId')
          AND name = N'MS_Description'
    )
        EXEC sys.sp_updateextendedproperty
            @name = N'MS_Description',
            @value = @VetoColumnDescription,
            @level0type = N'SCHEMA',
            @level0name = N'dbo',
            @level1type = N'TABLE',
            @level1name = N'HotelMatchVeto',
            @level2type = N'COLUMN',
            @level2name = @VetoColumnName;
    ELSE
        EXEC sys.sp_addextendedproperty
            @name = N'MS_Description',
            @value = @VetoColumnDescription,
            @level0type = N'SCHEMA',
            @level0name = N'dbo',
            @level1type = N'TABLE',
            @level1name = N'HotelMatchVeto',
            @level2type = N'COLUMN',
            @level2name = @VetoColumnName;

    DELETE FROM @VetoColumnDescriptions
    WHERE ColumnName = @VetoColumnName;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.HotelMatchVeto')
      AND name = N'UX_HotelMatchVeto_Pair'
)
    CREATE UNIQUE INDEX UX_HotelMatchVeto_Pair
        ON dbo.HotelMatchVeto(SupHotelId, MTHotelCode)
        ON [CACHE];

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.HotelMatchVeto')
      AND name = N'IX_HotelMatchVeto_PendingSync'
)
    CREATE INDEX IX_HotelMatchVeto_PendingSync
        ON dbo.HotelMatchVeto(RedisSyncedTime, RecoveryAlertSentTime, Id)
        INCLUDE
        (
            LastRedisSyncAttemptTime, FailureAlertSentTime,
            RedisSyncAttemptCount, MTHotelCode
        )
        ON [CACHE];

COMMIT TRANSACTION;
GO
