/*
  Hotel MatchingPipeline schema package.
  This script is intentionally not executed by the application.
  Review and apply it manually in the target RoomMapping database.
*/

IF COL_LENGTH('dbo.OTAHotelInfoRaw', 'DataVersion') IS NULL
    ALTER TABLE dbo.OTAHotelInfoRaw ADD DataVersion bigint NOT NULL CONSTRAINT DF_OTAHotelInfoRaw_DataVersion DEFAULT (1);
IF COL_LENGTH('dbo.OTAHotelInfoRaw', 'DataHash') IS NULL
    ALTER TABLE dbo.OTAHotelInfoRaw ADD DataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.OTAHotelInfoRaw', 'DataChangeTime') IS NULL
    ALTER TABLE dbo.OTAHotelInfoRaw ADD DataChangeTime datetime2(3) NOT NULL CONSTRAINT DF_OTAHotelInfoRaw_DataChangeTime DEFAULT (sysdatetime());

IF COL_LENGTH('dbo.SupHotelInfoRaw', 'DataVersion') IS NULL
    ALTER TABLE dbo.SupHotelInfoRaw ADD DataVersion bigint NOT NULL CONSTRAINT DF_SupHotelInfoRaw_DataVersion DEFAULT (1);
IF COL_LENGTH('dbo.SupHotelInfoRaw', 'DataHash') IS NULL
    ALTER TABLE dbo.SupHotelInfoRaw ADD DataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.SupHotelInfoRaw', 'DataChangeTime') IS NULL
    ALTER TABLE dbo.SupHotelInfoRaw ADD DataChangeTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelInfoRaw_DataChangeTime DEFAULT (sysdatetime());

IF OBJECT_ID('dbo.HotelStarLevelIssue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelStarLevelIssue
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelStarLevelIssue PRIMARY KEY,
        SourceType tinyint NOT NULL,
        SupplierId int NOT NULL,
        SourceId int NOT NULL,
        RawStarLevel nvarchar(100) COLLATE Chinese_PRC_BIN2 NOT NULL,
        FailureReason varchar(100) NOT NULL,
        FirstSeenTime datetime2(3) NOT NULL CONSTRAINT DF_HotelStarLevelIssue_FirstSeenTime DEFAULT (sysdatetime()),
        CONSTRAINT UQ_HotelStarLevelIssue_SourceSupplierRaw UNIQUE (SourceType, SupplierId, RawStarLevel)
    );
END;

IF COL_LENGTH('dbo.OTAHotelInfo', 'SourceDataVersion') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD SourceDataVersion bigint NOT NULL CONSTRAINT DF_OTAHotelInfo_SourceDataVersion DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'SourceDataHash') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD SourceDataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'NormalizedDataHash') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD NormalizedDataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel1Code') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel1Code varchar(50) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel1Name') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel1Name nvarchar(100) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel2Code') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel2Code varchar(50) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel2Name') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel2Name nvarchar(100) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel3Code') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel3Code varchar(50) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel3Name') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel3Name nvarchar(100) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionResolveStatus') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionResolveStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_RegionResolveStatus DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionFenceVersion') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionFenceVersion int NOT NULL CONSTRAINT DF_OTAHotelInfo_RegionFenceVersion DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'CleanStatus') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD CleanStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_CleanStatus DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'CleanError') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD CleanError nvarchar(1000) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'DataQualityStatus') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD DataQualityStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_DataQualityStatus DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'DataQualityFlags') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD DataQualityFlags bigint NOT NULL CONSTRAINT DF_OTAHotelInfo_DataQualityFlags DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'DataQualityDetailsJson') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD DataQualityDetailsJson nvarchar(max) NULL;

IF OBJECT_ID('dbo.SupHotelInfo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupHotelInfo
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupHotelInfo PRIMARY KEY,
        RawId bigint NOT NULL,
        SourceId int NOT NULL,
        SupId int NOT NULL,
        SHotelCode nvarchar(50) NOT NULL,
        ExistingMTHotelCode varchar(100) NOT NULL CONSTRAINT DF_SupHotelInfo_ExistingMTHotelCode DEFAULT (''),
        State int NOT NULL CONSTRAINT DF_SupHotelInfo_State DEFAULT (0),
        SSaleTopLevel int NULL,
        IsHandMatch int NULL,
        ReMatchState int NULL,
        HotelName nvarchar(200) NULL,
        HotelNameEn nvarchar(200) NULL,
        Tel nvarchar(100) NULL,
        Fax nvarchar(100) NULL,
        Address nvarchar(500) NULL,
        PostCode varchar(50) NULL,
        Email varchar(100) NULL,
        StarLevel decimal(3,1) NULL,
        CoordType tinyint NOT NULL CONSTRAINT DF_SupHotelInfo_CoordType DEFAULT (0),
        Longitude decimal(10,7) NULL,
        Latitude decimal(10,7) NULL,
        GeoPoint geography NULL,
        RegionLevel1Code varchar(50) NULL,
        RegionLevel1Name nvarchar(100) NULL,
        RegionLevel2Code varchar(50) NULL,
        RegionLevel2Name nvarchar(100) NULL,
        RegionLevel3Code varchar(50) NULL,
        RegionLevel3Name nvarchar(100) NULL,
        RegionResolveStatus tinyint NOT NULL CONSTRAINT DF_SupHotelInfo_RegionResolveStatus DEFAULT (0),
        RegionFenceVersion int NOT NULL CONSTRAINT DF_SupHotelInfo_RegionFenceVersion DEFAULT (0),
        SourceDataVersion bigint NOT NULL CONSTRAINT DF_SupHotelInfo_SourceDataVersion DEFAULT (0),
        SourceDataHash varbinary(32) NULL,
        NormalizedDataHash varbinary(32) NULL,
        CleanVersion int NOT NULL CONSTRAINT DF_SupHotelInfo_CleanVersion DEFAULT (0),
        CleanStatus tinyint NOT NULL CONSTRAINT DF_SupHotelInfo_CleanStatus DEFAULT (0),
        CleanError nvarchar(1000) NULL,
        CleanTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelInfo_CleanTime DEFAULT (sysdatetime()),
        DataQualityStatus tinyint NOT NULL CONSTRAINT DF_SupHotelInfo_DataQualityStatus DEFAULT (0),
        DataQualityFlags bigint NOT NULL CONSTRAINT DF_SupHotelInfo_DataQualityFlags DEFAULT (0),
        DataQualityDetailsJson nvarchar(max) NULL,
        MatchStatus tinyint NOT NULL CONSTRAINT DF_SupHotelInfo_MatchStatus DEFAULT (0),
        LastMatchVersion int NULL,
        LastMatchedDataHash varbinary(32) NULL,
        LastMatchTime datetime2(3) NULL,
        NextMatchTime datetime2(3) NULL,
        MatchAttemptCount int NOT NULL CONSTRAINT DF_SupHotelInfo_MatchAttemptCount DEFAULT (0),
        MatchError nvarchar(1000) NULL,
        CreateTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelInfo_CreateTime DEFAULT (sysdatetime()),
        UpdateTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelInfo_UpdateTime DEFAULT (sysdatetime()),
        CONSTRAINT FK_SupHotelInfo_SupHotelInfoRaw FOREIGN KEY (RawId) REFERENCES dbo.SupHotelInfoRaw(Id),
        CONSTRAINT CK_SupHotelInfo_CoordType CHECK (CoordType IN (0,1,2,3)),
        CONSTRAINT CK_SupHotelInfo_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_SupHotelInfo_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_SupHotelInfo_CoordinatePair CHECK ((Longitude IS NULL AND Latitude IS NULL) OR (Longitude IS NOT NULL AND Latitude IS NOT NULL))
    ) ON [CACHE];
END;

IF OBJECT_ID('dbo.OTAHotelPhone', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OTAHotelPhone
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OTAHotelPhone PRIMARY KEY,
        OTAHotelInfoId bigint NOT NULL,
        Ordinal smallint NOT NULL,
        Raw nvarchar(100) NOT NULL,
        E164 varchar(32) NULL,
        NationalNumber varchar(32) NULL,
        CountryCallingCode varchar(8) NULL,
        RegionCode char(2) NULL,
        NumberType varchar(32) NULL,
        AreaCode varchar(16) NULL,
        Extension varchar(20) NULL,
        IsValid bit NOT NULL,
        Error varchar(100) NULL,
        CreateTime datetime2(3) NOT NULL CONSTRAINT DF_OTAHotelPhone_CreateTime DEFAULT (sysdatetime()),
        UpdateTime datetime2(3) NOT NULL CONSTRAINT DF_OTAHotelPhone_UpdateTime DEFAULT (sysdatetime()),
        CONSTRAINT FK_OTAHotelPhone_OTAHotelInfo FOREIGN KEY (OTAHotelInfoId) REFERENCES dbo.OTAHotelInfo(Id) ON DELETE CASCADE
    ) ON [CACHE];
END;

IF OBJECT_ID('dbo.SupHotelPhone', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupHotelPhone
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupHotelPhone PRIMARY KEY,
        SupHotelInfoId bigint NOT NULL,
        Ordinal smallint NOT NULL,
        Raw nvarchar(100) NOT NULL,
        E164 varchar(32) NULL,
        NationalNumber varchar(32) NULL,
        CountryCallingCode varchar(8) NULL,
        RegionCode char(2) NULL,
        NumberType varchar(32) NULL,
        AreaCode varchar(16) NULL,
        Extension varchar(20) NULL,
        IsValid bit NOT NULL,
        Error varchar(100) NULL,
        CreateTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelPhone_CreateTime DEFAULT (sysdatetime()),
        UpdateTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelPhone_UpdateTime DEFAULT (sysdatetime()),
        CONSTRAINT FK_SupHotelPhone_SupHotelInfo FOREIGN KEY (SupHotelInfoId) REFERENCES dbo.SupHotelInfo(Id) ON DELETE CASCADE
    ) ON [CACHE];
END;

-- Phone details are relational. Remove the superseded JSON columns if an earlier draft was applied.
IF COL_LENGTH('dbo.OTAHotelInfo', 'NormalizedPhonesJson') IS NOT NULL
    ALTER TABLE dbo.OTAHotelInfo DROP COLUMN NormalizedPhonesJson;
IF COL_LENGTH('dbo.SupHotelInfo', 'NormalizedPhonesJson') IS NOT NULL
    ALTER TABLE dbo.SupHotelInfo DROP COLUMN NormalizedPhonesJson;

IF OBJECT_ID('dbo.HotelMatchDelivery', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelMatchDelivery
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelMatchDelivery PRIMARY KEY,
        SupId int NOT NULL,
        SHotelCode nvarchar(50) NOT NULL,
        MTHotelCode varchar(100) NOT NULL,
        SourceDataVersion bigint NOT NULL,
        NormalizedDataHash varbinary(32) NOT NULL,
        CleanVersion int NOT NULL,
        MatchingVersion int NOT NULL,
        IdempotencyKey varchar(64) NOT NULL,
        DeliveryStatus tinyint NOT NULL CONSTRAINT DF_HotelMatchDelivery_DeliveryStatus DEFAULT (0),
        RetryCount int NOT NULL CONSTRAINT DF_HotelMatchDelivery_RetryCount DEFAULT (0),
        NextRetryTime datetime2(3) NULL,
        RequestPayload nvarchar(max) NULL,
        ResponsePayload nvarchar(max) NULL,
        LastError nvarchar(1000) NULL,
        CreateTime datetime2(3) NOT NULL CONSTRAINT DF_HotelMatchDelivery_CreateTime DEFAULT (sysdatetime()),
        UpdateTime datetime2(3) NOT NULL CONSTRAINT DF_HotelMatchDelivery_UpdateTime DEFAULT (sysdatetime()),
        CONSTRAINT UQ_HotelMatchDelivery_IdempotencyKey UNIQUE (IdempotencyKey)
    ) ON [CACHE];
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfoRaw_DataChangeTime' AND object_id = OBJECT_ID('dbo.OTAHotelInfoRaw'))
    CREATE INDEX IX_OTAHotelInfoRaw_DataChangeTime ON dbo.OTAHotelInfoRaw(DataChangeTime, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfoRaw_DataVersion' AND object_id = OBJECT_ID('dbo.OTAHotelInfoRaw'))
    CREATE INDEX IX_OTAHotelInfoRaw_DataVersion ON dbo.OTAHotelInfoRaw(DataVersion, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OTAHotelInfo_RawId' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE UNIQUE INDEX UX_OTAHotelInfo_RawId ON dbo.OTAHotelInfo(RawId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfo_MTHotelCode' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE INDEX IX_OTAHotelInfo_MTHotelCode ON dbo.OTAHotelInfo(MTHotelCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfo_Clean' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE INDEX IX_OTAHotelInfo_Clean ON dbo.OTAHotelInfo(CleanStatus, DataQualityStatus, CleanVersion);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfoRaw_DataChangeTime' AND object_id = OBJECT_ID('dbo.SupHotelInfoRaw'))
    CREATE INDEX IX_SupHotelInfoRaw_DataChangeTime ON dbo.SupHotelInfoRaw(DataChangeTime, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfoRaw_DataVersion' AND object_id = OBJECT_ID('dbo.SupHotelInfoRaw'))
    CREATE INDEX IX_SupHotelInfoRaw_DataVersion ON dbo.SupHotelInfoRaw(DataVersion, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfo_Match' AND object_id = OBJECT_ID('dbo.SupHotelInfo'))
    CREATE INDEX IX_SupHotelInfo_Match ON dbo.SupHotelInfo(MatchStatus, LastMatchVersion, CleanStatus, DataQualityStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfo_Mapping' AND object_id = OBJECT_ID('dbo.SupHotelInfo'))
    CREATE INDEX IX_SupHotelInfo_Mapping ON dbo.SupHotelInfo(SupId, SHotelCode, ExistingMTHotelCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupHotelInfo_SupplierHotel' AND object_id = OBJECT_ID('dbo.SupHotelInfo'))
    CREATE UNIQUE INDEX UX_SupHotelInfo_SupplierHotel ON dbo.SupHotelInfo(SupId, SHotelCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfo_ExistingMTHotelCode' AND object_id = OBJECT_ID('dbo.SupHotelInfo'))
    CREATE INDEX IX_SupHotelInfo_ExistingMTHotelCode ON dbo.SupHotelInfo(ExistingMTHotelCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupHotelInfo_RawId' AND object_id = OBJECT_ID('dbo.SupHotelInfo'))
    CREATE UNIQUE INDEX UX_SupHotelInfo_RawId ON dbo.SupHotelInfo(RawId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_HotelMatchDelivery_BusinessKey' AND object_id = OBJECT_ID('dbo.HotelMatchDelivery'))
    CREATE UNIQUE INDEX UX_HotelMatchDelivery_BusinessKey ON dbo.HotelMatchDelivery(SupId, SHotelCode, NormalizedDataHash, MatchingVersion);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OTAHotelPhone_HotelOrdinal' AND object_id = OBJECT_ID('dbo.OTAHotelPhone'))
    CREATE UNIQUE INDEX UX_OTAHotelPhone_HotelOrdinal ON dbo.OTAHotelPhone(OTAHotelInfoId, Ordinal);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelPhone_E164' AND object_id = OBJECT_ID('dbo.OTAHotelPhone'))
    CREATE INDEX IX_OTAHotelPhone_E164 ON dbo.OTAHotelPhone(E164) INCLUDE (OTAHotelInfoId, NumberType) WHERE IsValid = 1 AND E164 IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelPhone_National' AND object_id = OBJECT_ID('dbo.OTAHotelPhone'))
    CREATE INDEX IX_OTAHotelPhone_National ON dbo.OTAHotelPhone(CountryCallingCode, NationalNumber) INCLUDE (OTAHotelInfoId) WHERE IsValid = 1 AND CountryCallingCode IS NOT NULL AND NationalNumber IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupHotelPhone_HotelOrdinal' AND object_id = OBJECT_ID('dbo.SupHotelPhone'))
    CREATE UNIQUE INDEX UX_SupHotelPhone_HotelOrdinal ON dbo.SupHotelPhone(SupHotelInfoId, Ordinal);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelPhone_E164' AND object_id = OBJECT_ID('dbo.SupHotelPhone'))
    CREATE INDEX IX_SupHotelPhone_E164 ON dbo.SupHotelPhone(E164) INCLUDE (SupHotelInfoId, NumberType) WHERE IsValid = 1 AND E164 IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelPhone_National' AND object_id = OBJECT_ID('dbo.SupHotelPhone'))
    CREATE INDEX IX_SupHotelPhone_National ON dbo.SupHotelPhone(CountryCallingCode, NationalNumber) INCLUDE (SupHotelInfoId) WHERE IsValid = 1 AND CountryCallingCode IS NOT NULL AND NationalNumber IS NOT NULL;

IF OBJECT_ID('dbo.TR_OTAHotelInfo_SetGeoPoint', 'TR') IS NULL
    EXEC('CREATE TRIGGER dbo.TR_OTAHotelInfo_SetGeoPoint ON dbo.OTAHotelInfo AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; UPDATE target SET GeoPoint = CASE WHEN target.Latitude IS NULL OR target.Longitude IS NULL THEN NULL ELSE geography::Point(target.Latitude, target.Longitude, 4326) END FROM dbo.OTAHotelInfo target INNER JOIN inserted item ON item.Id = target.Id; END');
IF OBJECT_ID('dbo.TR_SupHotelInfo_SetGeoPoint', 'TR') IS NULL
    EXEC('CREATE TRIGGER dbo.TR_SupHotelInfo_SetGeoPoint ON dbo.SupHotelInfo AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; UPDATE target SET GeoPoint = CASE WHEN target.Latitude IS NULL OR target.Longitude IS NULL THEN NULL ELSE geography::Point(target.Latitude, target.Longitude, 4326) END FROM dbo.SupHotelInfo target INNER JOIN inserted item ON item.Id = target.Id; END');

IF NOT EXISTS (SELECT 1 FROM sys.spatial_indexes WHERE name = 'SIX_OTAHotelInfo_GeoPoint' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE SPATIAL INDEX SIX_OTAHotelInfo_GeoPoint ON dbo.OTAHotelInfo(GeoPoint) USING GEOGRAPHY_AUTO_GRID;
IF NOT EXISTS (SELECT 1 FROM sys.spatial_indexes WHERE name = 'SIX_SupHotelInfo_GeoPoint' AND object_id = OBJECT_ID('dbo.SupHotelInfo'))
    CREATE SPATIAL INDEX SIX_SupHotelInfo_GeoPoint ON dbo.SupHotelInfo(GeoPoint) USING GEOGRAPHY_AUTO_GRID;
