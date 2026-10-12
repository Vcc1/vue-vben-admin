SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
DECLARE @sql nvarchar(max);
DECLARE @hasDuplicate bit = 0;
DECLARE @hasOrphan bit = 0;
DECLARE @hasUnmappedPhone bit = 0;

IF OBJECT_ID(N'dbo.SupHotelInfoRaw', N'U') IS NULL
    THROW 50001, 'dbo.SupHotelInfoRaw does not exist.', 1;

IF OBJECT_ID(N'dbo.SupHotelInfo', N'U') IS NULL
    THROW 50002, 'dbo.SupHotelInfo does not exist.', 1;

IF COL_LENGTH(N'dbo.SupHotelInfoRaw', N'SupHotelId') IS NULL
   AND COL_LENGTH(N'dbo.SupHotelInfoRaw', N'SourceId') IS NULL
    THROW 50003, 'SupHotelInfoRaw has neither SourceId nor SupHotelId.', 1;

IF COL_LENGTH(N'dbo.SupHotelInfo', N'SupHotelId') IS NULL
   AND COL_LENGTH(N'dbo.SupHotelInfo', N'SourceId') IS NULL
    THROW 50004, 'SupHotelInfo has neither SourceId nor SupHotelId.', 1;

IF COL_LENGTH(N'dbo.SupHotelInfoRaw', N'SourceId') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql
        N'SELECT @v = CASE WHEN EXISTS
          (SELECT SourceId FROM dbo.SupHotelInfoRaw GROUP BY SourceId HAVING COUNT_BIG(*) > 1)
          THEN 1 ELSE 0 END;',
        N'@v bit OUTPUT', @v = @hasDuplicate OUTPUT;
    IF @hasDuplicate = 1
        THROW 50005, 'SupHotelInfoRaw.SourceId contains duplicates and cannot become the primary key.', 1;
END;

SET @hasDuplicate = 0;
IF COL_LENGTH(N'dbo.SupHotelInfo', N'SourceId') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql
        N'SELECT @v = CASE WHEN EXISTS
          (SELECT SourceId FROM dbo.SupHotelInfo GROUP BY SourceId HAVING COUNT_BIG(*) > 1)
          THEN 1 ELSE 0 END;',
        N'@v bit OUTPUT', @v = @hasDuplicate OUTPUT;
    IF @hasDuplicate = 1
        THROW 50006, 'SupHotelInfo.SourceId contains duplicates and cannot become the primary key.', 1;
END;

IF COL_LENGTH(N'dbo.SupHotelInfoRaw', N'SourceId') IS NOT NULL
   AND COL_LENGTH(N'dbo.SupHotelInfo', N'SourceId') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql
        N'SELECT @v = CASE WHEN EXISTS
          (SELECT 1 FROM dbo.SupHotelInfo AS clean
           LEFT JOIN dbo.SupHotelInfoRaw AS raw ON raw.SourceId = clean.SourceId
           WHERE raw.SourceId IS NULL)
          THEN 1 ELSE 0 END;',
        N'@v bit OUTPUT', @v = @hasOrphan OUTPUT;
    IF @hasOrphan = 1
        THROW 50007, 'SupHotelInfo contains SourceId values that do not exist in SupHotelInfoRaw.', 1;
END;

/* Convert the phone foreign key values before removing SupHotelInfo.Id. */
IF OBJECT_ID(N'dbo.SupHotelPhone', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SupHotelPhone', N'SupHotelInfoId') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql
        N'SELECT @hasUnmapped = CASE WHEN EXISTS
          (SELECT 1
           FROM dbo.SupHotelPhone AS phone
           LEFT JOIN dbo.SupHotelInfo AS clean ON clean.Id = phone.SupHotelInfoId
           WHERE clean.Id IS NULL)
          THEN 1 ELSE 0 END;',
        N'@hasUnmapped bit OUTPUT', @hasUnmapped = @hasUnmappedPhone OUTPUT;
    IF @hasUnmappedPhone = 1
        THROW 50008, 'Some SupHotelPhone rows cannot be mapped to SupHotelId.', 1;

    SET @sql = NULL;
    SELECT @sql = STRING_AGG(
        CAST(N'ALTER TABLE dbo.SupHotelPhone DROP CONSTRAINT ' + QUOTENAME(fk.name) AS nvarchar(max)),
        N';')
    FROM sys.foreign_keys AS fk
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.SupHotelPhone')
      AND fk.referenced_object_id = OBJECT_ID(N'dbo.SupHotelInfo');
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    SET @sql = NULL;
    SELECT @sql = STRING_AGG(
        CAST(N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.SupHotelPhone' AS nvarchar(max)),
        N';')
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.SupHotelPhone')
      AND i.is_primary_key = 0
      AND EXISTS
      (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND c.name = N'SupHotelInfoId'
    );
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    IF COL_LENGTH(N'dbo.SupHotelInfo', N'SourceId') IS NOT NULL
        EXEC(N'
            UPDATE phone
            SET SupHotelInfoId = clean.SourceId
            FROM dbo.SupHotelPhone AS phone
            INNER JOIN dbo.SupHotelInfo AS clean ON clean.Id = phone.SupHotelInfoId;');
    ELSE
        EXEC(N'
            UPDATE phone
            SET SupHotelInfoId = clean.SupHotelId
            FROM dbo.SupHotelPhone AS phone
            INNER JOIN dbo.SupHotelInfo AS clean ON clean.Id = phone.SupHotelInfoId;');

    EXEC sys.sp_rename N'dbo.SupHotelPhone.SupHotelInfoId', N'SupHotelId', N'COLUMN';
    EXEC(N'ALTER TABLE dbo.SupHotelPhone ALTER COLUMN SupHotelId int NOT NULL;');
END;

/* Remove the old clean-to-raw foreign key before changing either primary key. */
SET @sql = NULL;
SELECT @sql = STRING_AGG(
    CAST(N'ALTER TABLE dbo.SupHotelInfo DROP CONSTRAINT ' + QUOTENAME(fk.name) AS nvarchar(max)),
    N';')
FROM sys.foreign_keys AS fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.SupHotelInfo')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.SupHotelInfoRaw');
IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

IF OBJECT_ID(N'dbo.TR_SupHotelInfo_SetGeoPoint', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_SupHotelInfo_SetGeoPoint;

/* Migrate SupHotelInfo to SupHotelId and MTHotelCode. */
IF COL_LENGTH(N'dbo.SupHotelInfo', N'SourceId') IS NOT NULL
BEGIN
    SET @sql = NULL;
    SELECT @sql = STRING_AGG(
        CAST(N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.SupHotelInfo' AS nvarchar(max)),
        N';')
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.SupHotelInfo')
      AND i.is_primary_key = 0
      AND EXISTS
      (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND c.name IN (N'Id', N'RawId', N'ExistingMTHotelCode')
      );
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    SET @sql = NULL;
    SELECT @sql = STRING_AGG(
        CAST(N'DROP INDEX ' + QUOTENAME(si.name) + N' ON dbo.SupHotelInfo' AS nvarchar(max)),
        N';')
    FROM sys.spatial_indexes AS si
    WHERE si.object_id = OBJECT_ID(N'dbo.SupHotelInfo');
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    SET @sql = NULL;
    SELECT @sql = N'ALTER TABLE dbo.SupHotelInfo DROP CONSTRAINT ' + QUOTENAME(k.name)
    FROM sys.key_constraints AS k
    WHERE k.parent_object_id = OBJECT_ID(N'dbo.SupHotelInfo') AND k.type = N'PK';
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    EXEC sys.sp_rename N'dbo.SupHotelInfo.SourceId', N'SupHotelId', N'COLUMN';
    IF COL_LENGTH(N'dbo.SupHotelInfo', N'ExistingMTHotelCode') IS NOT NULL
        EXEC sys.sp_rename N'dbo.SupHotelInfo.ExistingMTHotelCode', N'MTHotelCode', N'COLUMN';

    IF COL_LENGTH(N'dbo.SupHotelInfo', N'RawId') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.SupHotelInfo DROP COLUMN RawId;');
    IF COL_LENGTH(N'dbo.SupHotelInfo', N'Id') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.SupHotelInfo DROP COLUMN Id;');

    EXEC(N'ALTER TABLE dbo.SupHotelInfo
        ADD CONSTRAINT PK_SupHotelInfo PRIMARY KEY CLUSTERED (SupHotelId) ON [CACHE];');

END;

/* Migrate SupHotelInfoRaw to SupHotelId. */
IF COL_LENGTH(N'dbo.SupHotelInfoRaw', N'SourceId') IS NOT NULL
BEGIN
    SET @sql = NULL;
    SELECT @sql = STRING_AGG(
        CAST(N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.SupHotelInfoRaw' AS nvarchar(max)),
        N';')
    FROM sys.indexes AS i
    WHERE i.object_id = OBJECT_ID(N'dbo.SupHotelInfoRaw')
      AND i.is_primary_key = 0
      AND EXISTS
      (
          SELECT 1
          FROM sys.index_columns AS ic
          INNER JOIN sys.columns AS c
              ON c.object_id = ic.object_id AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id
            AND ic.index_id = i.index_id
            AND c.name = N'Id'
      );
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    SET @sql = NULL;
    SELECT @sql = N'ALTER TABLE dbo.SupHotelInfoRaw DROP CONSTRAINT ' + QUOTENAME(k.name)
    FROM sys.key_constraints AS k
    WHERE k.parent_object_id = OBJECT_ID(N'dbo.SupHotelInfoRaw') AND k.type = N'PK';
    IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;

    EXEC sys.sp_rename N'dbo.SupHotelInfoRaw.SourceId', N'SupHotelId', N'COLUMN';
    EXEC(N'ALTER TABLE dbo.SupHotelInfoRaw DROP COLUMN Id;');
    EXEC(N'ALTER TABLE dbo.SupHotelInfoRaw
        ADD CONSTRAINT PK_SupHotelInfoRaw PRIMARY KEY CLUSTERED (SupHotelId) ON [CACHE];');
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_SupHotelInfo_SupHotelInfoRaw'
      AND parent_object_id = OBJECT_ID(N'dbo.SupHotelInfo')
)
    EXEC(N'ALTER TABLE dbo.SupHotelInfo WITH CHECK
        ADD CONSTRAINT FK_SupHotelInfo_SupHotelInfoRaw
        FOREIGN KEY (SupHotelId) REFERENCES dbo.SupHotelInfoRaw(SupHotelId);');

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfoRaw')
      AND name = N'IX_SupHotelInfoRaw_DataChangeTime'
)
    EXEC(N'CREATE INDEX IX_SupHotelInfoRaw_DataChangeTime
        ON dbo.SupHotelInfoRaw(DataChangeTime, SupHotelId) ON [CACHE];');

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfoRaw')
      AND name = N'IX_SupHotelInfoRaw_DataVersion'
)
    EXEC(N'CREATE INDEX IX_SupHotelInfoRaw_DataVersion
        ON dbo.SupHotelInfoRaw(DataVersion, SupHotelId) ON [CACHE];');

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
      AND name = N'IX_SupHotelInfo_MTHotelCode'
)
    EXEC(N'CREATE INDEX IX_SupHotelInfo_MTHotelCode
        ON dbo.SupHotelInfo(MTHotelCode) ON [CACHE];');

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
      AND name = N'IX_SupHotelInfo_Mapping'
)
    EXEC(N'CREATE INDEX IX_SupHotelInfo_Mapping
        ON dbo.SupHotelInfo(SupId, SHotelCode, MTHotelCode) ON [CACHE];');

IF COL_LENGTH(N'dbo.SupHotelInfo', N'GeoPoint') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.spatial_indexes
       WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
   )
    EXEC(N'CREATE SPATIAL INDEX SIX_SupHotelInfo_GeoPoint
        ON dbo.SupHotelInfo(GeoPoint) USING GEOGRAPHY_AUTO_GRID;');

IF OBJECT_ID(N'dbo.SupHotelPhone', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_SupHotelPhone_SupHotelInfo'
          AND parent_object_id = OBJECT_ID(N'dbo.SupHotelPhone')
    )
        EXEC(N'ALTER TABLE dbo.SupHotelPhone WITH CHECK
            ADD CONSTRAINT FK_SupHotelPhone_SupHotelInfo
            FOREIGN KEY (SupHotelId) REFERENCES dbo.SupHotelInfo(SupHotelId)
            ON DELETE CASCADE;');

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupHotelPhone')
          AND name = N'UX_SupHotelPhone_HotelOrdinal'
    )
        EXEC(N'CREATE UNIQUE INDEX UX_SupHotelPhone_HotelOrdinal
            ON dbo.SupHotelPhone(SupHotelId, Ordinal) ON [CACHE];');

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupHotelPhone')
          AND name = N'IX_SupHotelPhone_E164'
    )
        EXEC(N'CREATE INDEX IX_SupHotelPhone_E164
            ON dbo.SupHotelPhone(E164)
            INCLUDE (SupHotelId, NumberType)
            WHERE IsValid = 1 AND E164 IS NOT NULL
            ON [CACHE];');

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupHotelPhone')
          AND name = N'IX_SupHotelPhone_National'
    )
        EXEC(N'CREATE INDEX IX_SupHotelPhone_National
            ON dbo.SupHotelPhone(CountryCallingCode, NationalNumber)
            INCLUDE (SupHotelId)
            WHERE IsValid = 1
              AND CountryCallingCode IS NOT NULL
              AND NationalNumber IS NOT NULL
            ON [CACHE];');
END;

EXEC(N'
CREATE TRIGGER dbo.TR_SupHotelInfo_SetGeoPoint
ON dbo.SupHotelInfo
WITH EXECUTE AS CALLER
FOR INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE target
    SET GeoPoint = CASE
        WHEN target.Latitude IS NULL OR target.Longitude IS NULL THEN NULL
        ELSE geography::Point(target.Latitude, target.Longitude, 4326)
    END
    FROM dbo.SupHotelInfo AS target
    INNER JOIN inserted AS item ON item.SupHotelId = target.SupHotelId;
END;');

COMMIT TRANSACTION;
GO
