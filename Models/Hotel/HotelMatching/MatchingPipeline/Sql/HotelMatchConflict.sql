CREATE TABLE dbo.HotelMatchConflict
(
    Id bigint IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_HotelMatchConflict PRIMARY KEY,
    SupHotelId int NOT NULL,
    SupId int NOT NULL,
    SupHotelCode nvarchar(50) NOT NULL,
    MatchingVersion int NOT NULL,
    CandidateCount int NOT NULL,
    MTHotelCodesJson nvarchar(max) NOT NULL,
    CandidatesJson nvarchar(max) NOT NULL,
    IsActive bit NOT NULL,
    FirstConflictTime datetime2(3) NOT NULL,
    LastConflictTime datetime2(3) NOT NULL,
    ResolvedTime datetime2(3) NULL,
    CreateTime datetime2(3) NOT NULL,
    UpdateTime datetime2(3) NOT NULL,
    CONSTRAINT FK_HotelMatchConflict_SupHotelInfo
        FOREIGN KEY (SupHotelId)
        REFERENCES dbo.SupHotelInfo(SupHotelId)
        ON DELETE CASCADE
) ON [CACHE] TEXTIMAGE_ON [CACHE];
GO

CREATE UNIQUE INDEX UX_HotelMatchConflict_SupHotelId
    ON dbo.HotelMatchConflict(SupHotelId)
    ON [CACHE];
GO

CREATE INDEX IX_HotelMatchConflict_Active
    ON dbo.HotelMatchConflict(IsActive, UpdateTime)
    INCLUDE (SupHotelId, SupId, SupHotelCode, CandidateCount, MatchingVersion)
    ON [CACHE];
GO
