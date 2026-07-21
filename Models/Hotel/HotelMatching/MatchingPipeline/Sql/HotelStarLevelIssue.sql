/* Idempotent schema for unknown or invalid hotel star-level values. */
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
