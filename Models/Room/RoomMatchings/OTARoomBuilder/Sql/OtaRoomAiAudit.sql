IF OBJECT_ID(N'dbo.OTARoomAiDecision', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OTARoomAiDecision
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OTARoomAiDecision PRIMARY KEY,
        SupId INT NOT NULL,
        SHotelCode NVARCHAR(100) NOT NULL,
        SRoomCode NVARCHAR(100) NOT NULL,
        MTHotelCode NVARCHAR(50) NOT NULL,
        SourceRoomNameChinese NVARCHAR(500) NULL,
        SourceRoomNameEnglish NVARCHAR(500) NULL,
        SearchResultVersion INT NULL,
        ProcessorVersion NVARCHAR(32) NOT NULL,
        PromptVersion NVARCHAR(32) NOT NULL,
        SuitableForCreation BIT NOT NULL,
        CleanedRoomNameChinese NVARCHAR(500) NULL,
        CleanedRoomNameEnglish NVARCHAR(500) NULL,
        CleanedRoomNameLocal NVARCHAR(500) NULL,
        DecisionJson NVARCHAR(MAX) NULL,
        ReasonChinese NVARCHAR(2000) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_OTARoomAiDecision_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_OTARoomAiDecision_Key
        ON dbo.OTARoomAiDecision(SupId, SHotelCode, SRoomCode, MTHotelCode);
END;

IF OBJECT_ID(N'dbo.OTARoomAiCreateFailure', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OTARoomAiCreateFailure
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OTARoomAiCreateFailure PRIMARY KEY,
        SupId INT NOT NULL,
        SHotelCode NVARCHAR(100) NOT NULL,
        SRoomCode NVARCHAR(100) NOT NULL,
        MTHotelCode NVARCHAR(50) NOT NULL,
        Attempts INT NOT NULL,
        ErrorMessage NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        SearchResultVersion INT NULL,
        ProcessorVersion NVARCHAR(32) NOT NULL,
        PromptVersion NVARCHAR(32) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_OTARoomAiCreateFailure_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_OTARoomAiCreateFailure_Key
        ON dbo.OTARoomAiCreateFailure(SupId, SHotelCode, SRoomCode, MTHotelCode);
END;
