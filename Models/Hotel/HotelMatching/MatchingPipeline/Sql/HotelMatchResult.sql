/*
  Matching pipeline evidence result table and queue index.
  Run before deploying the matching pipeline.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SupHotelInfo', N'U') IS NULL
    THROW 50001, 'dbo.SupHotelInfo does not exist.', 1;

IF OBJECT_ID(N'dbo.HotelMatchResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelMatchResult
    (
        Id bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HotelMatchResult PRIMARY KEY,
        SupHotelId int NOT NULL,
        MTHotelCode varchar(100) NOT NULL,
        NameEvidenceSourceType tinyint NOT NULL,
        NameEvidenceHotelId int NOT NULL,
        NameMatchKind tinyint NOT NULL,
        NamePendingValue nvarchar(300) NULL,
        NameEvidenceValue nvarchar(300) NULL,
        CoordinateEvidenceSourceType tinyint NOT NULL,
        CoordinateEvidenceHotelId int NOT NULL,
        DistanceMeters decimal(12,3) NOT NULL,
        StarEvidenceSourceType tinyint NULL,
        StarEvidenceHotelId int NULL,
        StarPendingValue varchar(20) NULL,
        StarEvidenceValue varchar(20) NULL,
        AddressEvidenceSourceType tinyint NULL,
        AddressEvidenceHotelId int NULL,
        AddressPendingValue nvarchar(500) NULL,
        AddressEvidenceValue nvarchar(500) NULL,
        PhoneEvidenceSourceType tinyint NULL,
        PhoneEvidenceHotelId int NULL,
        PhonePendingValue varchar(32) NULL,
        PhoneEvidenceValue varchar(32) NULL,
        OtaFactCount int NOT NULL,
        SupplierFactCount int NOT NULL,
        EvidenceJson nvarchar(max) NULL,
        CreateTime datetime2(3) NOT NULL,
        UpdateTime datetime2(3) NOT NULL,
        CONSTRAINT FK_HotelMatchResult_SupHotelInfo
            FOREIGN KEY (SupHotelId)
            REFERENCES dbo.SupHotelInfo(SupHotelId)
            ON DELETE CASCADE
    ) ON [CACHE];
END;

IF COL_LENGTH(N'dbo.HotelMatchResult', N'MatchingVersion') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.HotelMatchResult')
          AND name = N'UX_HotelMatchResult_BusinessKey'
    )
        DROP INDEX UX_HotelMatchResult_BusinessKey ON dbo.HotelMatchResult;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.HotelMatchResult')
          AND name = N'IX_HotelMatchResult_MTHotelCode'
    )
        DROP INDEX IX_HotelMatchResult_MTHotelCode ON dbo.HotelMatchResult;

    ;WITH RankedResults AS
    (
        SELECT
            Id,
            ROW_NUMBER() OVER
            (
                PARTITION BY SupHotelId
                ORDER BY MatchingVersion DESC, UpdateTime DESC, Id DESC
            ) AS RowNumber
        FROM dbo.HotelMatchResult
    )
    DELETE FROM RankedResults
    WHERE RowNumber > 1;

    DECLARE @MatchingVersionDefaultConstraint sysname;
    SELECT @MatchingVersionDefaultConstraint = defaultConstraints.name
    FROM sys.default_constraints AS defaultConstraints
    INNER JOIN sys.columns AS columnsInfo
        ON columnsInfo.default_object_id = defaultConstraints.object_id
    WHERE defaultConstraints.parent_object_id = OBJECT_ID(N'dbo.HotelMatchResult')
      AND columnsInfo.name = N'MatchingVersion';

    IF @MatchingVersionDefaultConstraint IS NOT NULL
        EXEC(N'ALTER TABLE dbo.HotelMatchResult DROP CONSTRAINT '
            + QUOTENAME(@MatchingVersionDefaultConstraint) + N';');

    EXEC(N'ALTER TABLE dbo.HotelMatchResult DROP COLUMN MatchingVersion;');
END;

IF COL_LENGTH(N'dbo.HotelMatchResult', N'PendingSourceDataVersion') IS NOT NULL
    EXEC(N'ALTER TABLE dbo.HotelMatchResult DROP COLUMN PendingSourceDataVersion;');

IF COL_LENGTH(N'dbo.HotelMatchResult', N'PendingNormalizedDataHash') IS NOT NULL
    EXEC(N'ALTER TABLE dbo.HotelMatchResult DROP COLUMN PendingNormalizedDataHash;');

DECLARE @ColumnDescriptions TABLE
(
    ColumnName sysname NOT NULL PRIMARY KEY,
    [Description] nvarchar(400) NOT NULL
);

INSERT INTO @ColumnDescriptions(ColumnName, [Description])
VALUES
    (N'Id', N'酒店匹配结果主键'),
    (N'SupHotelId', N'当前待匹配的供应商酒店ID，对应SupHotelInfo.SupHotelId'),
    (N'MTHotelCode', N'候选匹配的主酒店编码'),
    (N'NameEvidenceSourceType', N'名称证据来源类型：1=OTA酒店，2=供应商酒店'),
    (N'NameEvidenceHotelId', N'提供名称匹配证据的具体OTA或供应商酒店ID'),
    (N'NameMatchKind', N'名称匹配方式：1=词和顺序一致，2=词一致但顺序不同，3=移除忽略词后匹配'),
    (N'NamePendingValue', N'待匹配供应商酒店参与比较的名称'),
    (N'NameEvidenceValue', N'事实酒店中与待匹配名称命中的名称'),
    (N'CoordinateEvidenceSourceType', N'经纬度证据来源类型：1=OTA酒店，2=供应商酒店'),
    (N'CoordinateEvidenceHotelId', N'与待匹配酒店距离满足要求的具体OTA或供应商酒店ID'),
    (N'DistanceMeters', N'待匹配酒店与坐标证据酒店之间的距离，单位为米'),
    (N'StarEvidenceSourceType', N'星级证据来源类型：1=OTA酒店，2=供应商酒店'),
    (N'StarEvidenceHotelId', N'提供星级匹配证据的具体OTA或供应商酒店ID'),
    (N'StarPendingValue', N'待匹配供应商酒店的星级值'),
    (N'StarEvidenceValue', N'事实酒店中命中的星级值'),
    (N'AddressEvidenceSourceType', N'地址证据来源类型：1=OTA酒店，2=供应商酒店'),
    (N'AddressEvidenceHotelId', N'提供地址匹配证据的具体OTA或供应商酒店ID'),
    (N'AddressPendingValue', N'待匹配供应商酒店的地址'),
    (N'AddressEvidenceValue', N'事实酒店中命中的地址'),
    (N'PhoneEvidenceSourceType', N'电话证据来源类型：1=OTA酒店，2=供应商酒店'),
    (N'PhoneEvidenceHotelId', N'提供电话匹配证据的具体OTA或供应商酒店ID'),
    (N'PhonePendingValue', N'待匹配供应商酒店的标准化电话号码'),
    (N'PhoneEvidenceValue', N'事实酒店中命中的标准化电话号码'),
    (N'OtaFactCount', N'该候选主酒店编码参与匹配的OTA事实酒店数量'),
    (N'SupplierFactCount', N'该候选主酒店编码参与匹配的供应商事实酒店数量'),
    (N'EvidenceJson', N'该候选关系的完整匹配证据JSON'),
    (N'CreateTime', N'匹配结果创建时间'),
    (N'UpdateTime', N'匹配结果最后更新时间');

DECLARE @ColumnName sysname;
DECLARE @ColumnDescription nvarchar(400);

WHILE EXISTS (SELECT 1 FROM @ColumnDescriptions)
BEGIN
    SELECT TOP (1)
        @ColumnName = ColumnName,
        @ColumnDescription = [Description]
    FROM @ColumnDescriptions
    ORDER BY ColumnName;

    IF EXISTS
    (
        SELECT 1
        FROM sys.extended_properties
        WHERE class = 1
          AND major_id = OBJECT_ID(N'dbo.HotelMatchResult')
          AND minor_id = COLUMNPROPERTY(
              OBJECT_ID(N'dbo.HotelMatchResult'),
              @ColumnName,
              N'ColumnId')
          AND name = N'MS_Description'
    )
        EXEC sys.sp_updateextendedproperty
            @name = N'MS_Description',
            @value = @ColumnDescription,
            @level0type = N'SCHEMA',
            @level0name = N'dbo',
            @level1type = N'TABLE',
            @level1name = N'HotelMatchResult',
            @level2type = N'COLUMN',
            @level2name = @ColumnName;
    ELSE
        EXEC sys.sp_addextendedproperty
            @name = N'MS_Description',
            @value = @ColumnDescription,
            @level0type = N'SCHEMA',
            @level0name = N'dbo',
            @level1type = N'TABLE',
            @level1name = N'HotelMatchResult',
            @level2type = N'COLUMN',
            @level2name = @ColumnName;

    DELETE FROM @ColumnDescriptions
    WHERE ColumnName = @ColumnName;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.HotelMatchResult')
      AND name = N'UX_HotelMatchResult_BusinessKey'
)
    DROP INDEX UX_HotelMatchResult_BusinessKey ON dbo.HotelMatchResult;

;WITH RankedResults AS
(
    SELECT
        Id,
        ROW_NUMBER() OVER
        (
            PARTITION BY SupHotelId
            ORDER BY UpdateTime DESC, Id DESC
        ) AS RowNumber
    FROM dbo.HotelMatchResult
)
DELETE FROM RankedResults
WHERE RowNumber > 1;

CREATE UNIQUE INDEX UX_HotelMatchResult_BusinessKey
    ON dbo.HotelMatchResult(SupHotelId)
    ON [CACHE];

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.HotelMatchResult')
      AND name = N'IX_HotelMatchResult_MTHotelCode'
    )
    CREATE INDEX IX_HotelMatchResult_MTHotelCode
        ON dbo.HotelMatchResult(MTHotelCode)
        INCLUDE (SupHotelId, DistanceMeters, NameEvidenceHotelId, CoordinateEvidenceHotelId)
        ON [CACHE];

IF EXISTS
(
    SELECT 1
    FROM sys.indexes AS indexInfo
    INNER JOIN sys.index_columns AS indexColumn
        ON indexColumn.object_id = indexInfo.object_id
        AND indexColumn.index_id = indexInfo.index_id
    INNER JOIN sys.columns AS columnInfo
        ON columnInfo.object_id = indexColumn.object_id
        AND columnInfo.column_id = indexColumn.column_id
    WHERE indexInfo.object_id = OBJECT_ID(N'dbo.SupHotelInfo')
      AND indexInfo.name = N'IX_SupHotelInfo_MatchingQueue'
      AND columnInfo.name = N'LastMatchVersion'
)
    DROP INDEX IX_SupHotelInfo_MatchingQueue ON dbo.SupHotelInfo;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
      AND name = N'IX_SupHotelInfo_MatchingQueue'
    )
    CREATE INDEX IX_SupHotelInfo_MatchingQueue
        ON dbo.SupHotelInfo(State, MTHotelCode, SupHotelId)
        ON [CACHE];

COMMIT TRANSACTION;
GO
