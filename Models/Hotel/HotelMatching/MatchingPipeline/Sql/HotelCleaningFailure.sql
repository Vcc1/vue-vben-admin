IF OBJECT_ID(N'dbo.HotelCleaningFailure', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelCleaningFailure
    (
        Id bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HotelCleaningFailure PRIMARY KEY CLUSTERED,
        SourceType tinyint NOT NULL,
        DataId bigint NOT NULL,
        SupplierId int NULL,
        SourceId int NULL,
        HotelCode nvarchar(200) NOT NULL,
        ExceptionType varchar(500) NOT NULL,
        ExceptionMessage nvarchar(4000) NOT NULL,
        StackTrace nvarchar(max) NULL,
        CleanVersion int NOT NULL,
        RegionBoundaryVersion int NOT NULL,
        FailureCount int NOT NULL
            CONSTRAINT DF_HotelCleaningFailure_FailureCount DEFAULT (1),
        FirstFailureTime datetime2(3) NOT NULL
            CONSTRAINT DF_HotelCleaningFailure_FirstFailureTime DEFAULT (sysdatetime()),
        LastFailureTime datetime2(3) NOT NULL
            CONSTRAINT DF_HotelCleaningFailure_LastFailureTime DEFAULT (sysdatetime()),
        CONSTRAINT UX_HotelCleaningFailure_SourceType_DataId
            UNIQUE (SourceType, DataId)
    ) ON [CACHE] TEXTIMAGE_ON [CACHE];

    CREATE INDEX IX_HotelCleaningFailure_LastFailureTime
        ON dbo.HotelCleaningFailure(LastFailureTime DESC)
        INCLUDE (SourceType, DataId, HotelCode, ExceptionType, FailureCount)
        ON [CACHE];
END;
GO

-- Run this after fixing failures. Cleared source rows are retried by the next cleaning pass.
-- TRUNCATE TABLE dbo.HotelCleaningFailure;
