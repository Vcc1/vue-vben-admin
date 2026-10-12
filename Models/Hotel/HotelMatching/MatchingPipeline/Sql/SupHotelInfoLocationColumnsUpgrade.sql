SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SupHotelInfo', N'U') IS NULL
    THROW 50001, 'dbo.SupHotelInfo does not exist.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.SupHotelInfo', N'CountryCode') IS NULL
    ALTER TABLE dbo.SupHotelInfo ADD CountryCode nvarchar(50) NULL;

IF COL_LENGTH(N'dbo.SupHotelInfo', N'CountryName') IS NULL
    ALTER TABLE dbo.SupHotelInfo ADD CountryName nvarchar(50) NULL;

IF COL_LENGTH(N'dbo.SupHotelInfo', N'CityCode') IS NULL
    ALTER TABLE dbo.SupHotelInfo ADD CityCode nvarchar(50) NULL;

IF COL_LENGTH(N'dbo.SupHotelInfo', N'CityName') IS NULL
    ALTER TABLE dbo.SupHotelInfo ADD CityName nvarchar(100) NULL;

COMMIT TRANSACTION;

SELECT
    column_name = columns.name,
    data_type = types.name,
    max_length = columns.max_length,
    is_nullable = columns.is_nullable
FROM sys.columns AS columns
INNER JOIN sys.types AS types ON types.user_type_id = columns.user_type_id
WHERE columns.object_id = OBJECT_ID(N'dbo.SupHotelInfo')
  AND columns.name IN (N'CountryCode', N'CountryName', N'CityCode', N'CityName')
ORDER BY columns.column_id;
