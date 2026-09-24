IF OBJECT_ID(N'dbo.RoomMatchEvalRun', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomMatchEvalRun
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoomMatchEvalRun PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        Status NVARCHAR(32) NOT NULL,
        TotalHotels INT NOT NULL DEFAULT 0,
        TotalSupplierRooms INT NOT NULL DEFAULT 0,
        MatchedCount INT NOT NULL DEFAULT 0,
        UnmatchedCount INT NOT NULL DEFAULT 0,
        FullMatchCount INT NOT NULL DEFAULT 0,
        TextMatchCount INT NOT NULL DEFAULT 0,
        NotMatchCount INT NOT NULL DEFAULT 0,
        DiffNewMatch INT NOT NULL DEFAULT 0,
        DiffLostMatch INT NOT NULL DEFAULT 0,
        DiffReassigned INT NOT NULL DEFAULT 0,
        DiffStatusUp INT NOT NULL DEFAULT 0,
        DiffStatusDown INT NOT NULL DEFAULT 0,
        BaselineRunId BIGINT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_RoomMatchEvalRun_CreatedAt DEFAULT SYSUTCDATETIME(),
        CompletedAt DATETIME2 NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.RoomMatchEvalResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomMatchEvalResult
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoomMatchEvalResult PRIMARY KEY,
        RunId BIGINT NOT NULL CONSTRAINT FK_RoomMatchEvalResult_Run FOREIGN KEY REFERENCES dbo.RoomMatchEvalRun(Id),
        MTHotelCode NVARCHAR(50) NOT NULL,
        SupId INT NOT NULL,
        SupRoomId INT NOT NULL,
        OTARoomId INT NULL,
        MatchType INT NOT NULL,
        MatchScore DECIMAL(5,4) NULL,
        SupRoomNameEN NVARCHAR(200) NULL,
        SupRoomNameCN NVARCHAR(200) NULL,
        OTARoomNameEN NVARCHAR(200) NULL,
        OTARoomNameCN NVARCHAR(200) NULL
    );
    CREATE INDEX IX_RoomMatchEvalResult_RunId ON dbo.RoomMatchEvalResult(RunId);
    CREATE INDEX IX_RoomMatchEvalResult_Run_SupRoom ON dbo.RoomMatchEvalResult(RunId, SupRoomId);
END;
GO

IF OBJECT_ID(N'dbo.RoomMatchEvalDiff', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomMatchEvalDiff
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RoomMatchEvalDiff PRIMARY KEY,
        RunId BIGINT NOT NULL CONSTRAINT FK_RoomMatchEvalDiff_Run FOREIGN KEY REFERENCES dbo.RoomMatchEvalRun(Id),
        SupRoomId INT NOT NULL,
        MTHotelCode NVARCHAR(50) NOT NULL,
        SupId INT NOT NULL,
        DiffType NVARCHAR(32) NOT NULL,
        Category NVARCHAR(50) NULL,
        OldOTARoomId INT NULL,
        NewOTARoomId INT NULL,
        OldMatchType INT NULL,
        NewMatchType INT NULL,
        OldMatchScore DECIMAL(5,4) NULL,
        NewMatchScore DECIMAL(5,4) NULL,
        SupRoomNameEN NVARCHAR(200) NULL,
        SupRoomNameCN NVARCHAR(200) NULL,
        OldOTARoomNameEN NVARCHAR(200) NULL,
        OldOTARoomNameCN NVARCHAR(200) NULL,
        NewOTARoomNameEN NVARCHAR(200) NULL,
        NewOTARoomNameCN NVARCHAR(200) NULL,
        Detail NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_RoomMatchEvalDiff_RunId ON dbo.RoomMatchEvalDiff(RunId);
    CREATE INDEX IX_RoomMatchEvalDiff_Run_DiffType ON dbo.RoomMatchEvalDiff(RunId, DiffType);
    CREATE INDEX IX_RoomMatchEvalDiff_Run_Category ON dbo.RoomMatchEvalDiff(RunId, Category);
END;
GO
