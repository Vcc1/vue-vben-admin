/*
  Run manually in the RoomMapping database after deploying the matching code
  that reads HotelName/HotelNameEn directly.
*/

IF COL_LENGTH(N'dbo.OTAHotelInfo', N'NormalizedHotelName') IS NOT NULL
    ALTER TABLE dbo.OTAHotelInfo DROP COLUMN NormalizedHotelName;
IF COL_LENGTH(N'dbo.OTAHotelInfo', N'NormalizedHotelEnName') IS NOT NULL
    ALTER TABLE dbo.OTAHotelInfo DROP COLUMN NormalizedHotelEnName;
IF COL_LENGTH(N'dbo.OTAHotelInfo', N'NameTokensJson') IS NOT NULL
    ALTER TABLE dbo.OTAHotelInfo DROP COLUMN NameTokensJson;
GO

IF COL_LENGTH(N'dbo.SupHotelInfo', N'NormalizedHotelName') IS NOT NULL
    ALTER TABLE dbo.SupHotelInfo DROP COLUMN NormalizedHotelName;
IF COL_LENGTH(N'dbo.SupHotelInfo', N'NormalizedHotelEnName') IS NOT NULL
    ALTER TABLE dbo.SupHotelInfo DROP COLUMN NormalizedHotelEnName;
IF COL_LENGTH(N'dbo.SupHotelInfo', N'NameTokensJson') IS NOT NULL
    ALTER TABLE dbo.SupHotelInfo DROP COLUMN NameTokensJson;
GO
