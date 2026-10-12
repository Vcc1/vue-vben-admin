IF OBJECT_ID(N'dbo.ExpediaTaapAccountLevels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpediaTaapAccountLevels
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpediaTaapAccountLevels PRIMARY KEY,
        LevelCode NVARCHAR(50) NOT NULL,
        LevelName NVARCHAR(100) NOT NULL,
        MinAnnualGbv DECIMAL(18,2) NULL,
        GbvRequirementText NVARCHAR(100) NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevels_GbvRequirementText DEFAULT(N''),
        SortOrder INT NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevels_SortOrder DEFAULT(0),
        IsEnabled BIT NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevels_IsEnabled DEFAULT(1),
        Remark NVARCHAR(500) NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevels_Remark DEFAULT(N''),
        CreateTime DATETIME2 NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevels_CreateTime DEFAULT(SYSUTCDATETIME()),
        UpdateTime DATETIME2 NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevels_UpdateTime DEFAULT(SYSUTCDATETIME())
    );
END;
GO

IF COL_LENGTH(N'dbo.ExpediaTaapAccountLevels', N'MinAnnualGbv') IS NULL
BEGIN
    ALTER TABLE dbo.ExpediaTaapAccountLevels ADD MinAnnualGbv DECIMAL(18,2) NULL;
END;
GO

IF COL_LENGTH(N'dbo.ExpediaTaapAccountLevels', N'GbvRequirementText') IS NULL
BEGIN
    ALTER TABLE dbo.ExpediaTaapAccountLevels ADD GbvRequirementText NVARCHAR(100) NOT NULL
        CONSTRAINT DF_ExpediaTaapAccountLevels_GbvRequirementText DEFAULT(N'');
END;
GO

IF COL_LENGTH(N'dbo.ExpediaTaapAccountLevels', N'SortOrder') IS NULL
BEGIN
    ALTER TABLE dbo.ExpediaTaapAccountLevels ADD SortOrder INT NOT NULL
        CONSTRAINT DF_ExpediaTaapAccountLevels_SortOrder DEFAULT(0);
END;
GO

IF COL_LENGTH(N'dbo.ExpediaTaapAccountLevels', N'DiscountRate') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ExpediaTaapAccountLevels DROP COLUMN DiscountRate;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccountLevels_LevelCode'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccountLevels')
)
BEGIN
    CREATE UNIQUE INDEX IX_ExpediaTaapAccountLevels_LevelCode
        ON dbo.ExpediaTaapAccountLevels(LevelCode);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccountLevels_LevelName'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccountLevels')
)
BEGIN
    CREATE UNIQUE INDEX IX_ExpediaTaapAccountLevels_LevelName
        ON dbo.ExpediaTaapAccountLevels(LevelName);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccountLevels_Sort_Enabled'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccountLevels')
)
BEGIN
    CREATE INDEX IX_ExpediaTaapAccountLevels_Sort_Enabled
        ON dbo.ExpediaTaapAccountLevels(SortOrder, IsEnabled);
END;
GO

IF OBJECT_ID(N'dbo.ExpediaTaapAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpediaTaapAccounts
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpediaTaapAccounts PRIMARY KEY,
        AccountLevelId INT NOT NULL,
        AccountName NVARCHAR(100) NOT NULL,
        SigninEmailAddress NVARCHAR(200) NOT NULL,
        SigninPassword NVARCHAR(200) NOT NULL,
        CookieHeader NVARCHAR(MAX) NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_CookieHeader DEFAULT(N''),
        Sha256Hash NVARCHAR(128) NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_Sha256Hash DEFAULT(N''),
        ClientInfoToken NVARCHAR(200) NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_ClientInfoToken DEFAULT(N''),
        Duaid NVARCHAR(100) NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_Duaid DEFAULT(N''),
        Eapid INT NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_Eapid DEFAULT(0),
        LoginStateExpiresAt DATETIMEOFFSET NULL,
        LastLoginAt DATETIMEOFFSET NULL,
        WorkerConcurrency INT NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_WorkerConcurrency DEFAULT(1),
        CanRefreshSharedSha256Hash BIT NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_CanRefreshSharedSha256Hash DEFAULT(0),
        IsEnabled BIT NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_IsEnabled DEFAULT(1),
        Remark NVARCHAR(500) NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_Remark DEFAULT(N''),
        CreateTime DATETIME2 NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_CreateTime DEFAULT(SYSUTCDATETIME()),
        UpdateTime DATETIME2 NOT NULL CONSTRAINT DF_ExpediaTaapAccounts_UpdateTime DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_ExpediaTaapAccounts_AccountLevelId
            FOREIGN KEY (AccountLevelId) REFERENCES dbo.ExpediaTaapAccountLevels(Id)
    );
END;
GO

IF COL_LENGTH(N'dbo.ExpediaTaapAccounts', N'WorkerConcurrency') IS NULL
BEGIN
    ALTER TABLE dbo.ExpediaTaapAccounts ADD WorkerConcurrency INT NOT NULL
        CONSTRAINT DF_ExpediaTaapAccounts_WorkerConcurrency DEFAULT(1);
END;
GO

IF COL_LENGTH(N'dbo.ExpediaTaapAccounts', N'CanRefreshSharedSha256Hash') IS NULL
BEGIN
    ALTER TABLE dbo.ExpediaTaapAccounts ADD CanRefreshSharedSha256Hash BIT NOT NULL
        CONSTRAINT DF_ExpediaTaapAccounts_CanRefreshSharedSha256Hash DEFAULT(0);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccounts_SigninEmailAddress'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccounts')
)
BEGIN
    CREATE UNIQUE INDEX IX_ExpediaTaapAccounts_SigninEmailAddress
        ON dbo.ExpediaTaapAccounts(SigninEmailAddress);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccounts_Level_Enabled'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccounts')
)
BEGIN
    CREATE INDEX IX_ExpediaTaapAccounts_Level_Enabled
        ON dbo.ExpediaTaapAccounts(AccountLevelId, IsEnabled);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccounts_SharedSha_Enabled'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccounts')
)
BEGIN
    CREATE INDEX IX_ExpediaTaapAccounts_SharedSha_Enabled
        ON dbo.ExpediaTaapAccounts(CanRefreshSharedSha256Hash, IsEnabled);
END;
GO

IF OBJECT_ID(N'dbo.ExpediaTaapAccountLevelCommissionRates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpediaTaapAccountLevelCommissionRates
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpediaTaapAccountLevelCommissionRates PRIMARY KEY,
        AccountLevelId INT NOT NULL,
        ProductCode NVARCHAR(50) NOT NULL,
        ProductName NVARCHAR(100) NOT NULL,
        CommissionRate DECIMAL(10,4) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevelCommissionRates_SortOrder DEFAULT(0),
        IsEnabled BIT NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevelCommissionRates_IsEnabled DEFAULT(1),
        Remark NVARCHAR(500) NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevelCommissionRates_Remark DEFAULT(N''),
        CreateTime DATETIME2 NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevelCommissionRates_CreateTime DEFAULT(SYSUTCDATETIME()),
        UpdateTime DATETIME2 NOT NULL CONSTRAINT DF_ExpediaTaapAccountLevelCommissionRates_UpdateTime DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_ExpediaTaapAccountLevelCommissionRates_AccountLevelId
            FOREIGN KEY (AccountLevelId) REFERENCES dbo.ExpediaTaapAccountLevels(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccountLevelCommissionRates_Level_Product'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccountLevelCommissionRates')
)
BEGIN
    CREATE UNIQUE INDEX IX_ExpediaTaapAccountLevelCommissionRates_Level_Product
        ON dbo.ExpediaTaapAccountLevelCommissionRates(AccountLevelId, ProductCode);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ExpediaTaapAccountLevelCommissionRates_Level_Sort_Enabled'
      AND object_id = OBJECT_ID(N'dbo.ExpediaTaapAccountLevelCommissionRates')
)
BEGIN
    CREATE INDEX IX_ExpediaTaapAccountLevelCommissionRates_Level_Sort_Enabled
        ON dbo.ExpediaTaapAccountLevelCommissionRates(AccountLevelId, SortOrder, IsEnabled);
END;
GO

MERGE dbo.ExpediaTaapAccountLevels AS target
USING (
    VALUES
        (N'STANDARD', N'Standard', CAST(NULL AS DECIMAL(18,2)), N'No GBV requirement', 10, 1, N''),
        (N'SILVER', N'Silver', 120000.00, N'GBV CNY 120,000 / year', 20, 1, N''),
        (N'GOLD', N'Gold', 600000.00, N'GBV CNY 600,000 / year', 30, 1, N''),
        (N'PLATINUM', N'Platinum', 3300000.00, N'GBV CNY 3,300,000 / year', 40, 1, N'')
) AS source(LevelCode, LevelName, MinAnnualGbv, GbvRequirementText, SortOrder, IsEnabled, Remark)
ON target.LevelCode = source.LevelCode
WHEN MATCHED THEN
    UPDATE SET
        target.LevelName = source.LevelName,
        target.MinAnnualGbv = source.MinAnnualGbv,
        target.GbvRequirementText = source.GbvRequirementText,
        target.SortOrder = source.SortOrder,
        target.IsEnabled = source.IsEnabled,
        target.Remark = source.Remark,
        target.UpdateTime = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (LevelCode, LevelName, MinAnnualGbv, GbvRequirementText, SortOrder, IsEnabled, Remark, CreateTime, UpdateTime)
    VALUES (source.LevelCode, source.LevelName, source.MinAnnualGbv, source.GbvRequirementText, source.SortOrder, source.IsEnabled, source.Remark, SYSUTCDATETIME(), SYSUTCDATETIME());
GO

MERGE dbo.ExpediaTaapAccountLevelCommissionRates AS target
USING (
    SELECT
        l.Id AS AccountLevelId,
        v.ProductCode,
        v.ProductName,
        v.CommissionRate,
        v.SortOrder,
        CAST(1 AS BIT) AS IsEnabled,
        N'' AS Remark
    FROM dbo.ExpediaTaapAccountLevels l
    JOIN (
        VALUES
            (N'STANDARD', N'PREMIUM_PLUS', N'Premium Plus', 10.0000, 10),
            (N'STANDARD', N'PREMIUM', N'Premium', 8.0000, 20),
            (N'STANDARD', N'BASIC_PLUS', N'Basic Plus', 6.0000, 30),
            (N'STANDARD', N'BASIC', N'Basic', 3.0000, 40),
            (N'SILVER', N'PREMIUM_PLUS', N'Premium Plus', 11.0000, 10),
            (N'SILVER', N'PREMIUM', N'Premium', 9.0000, 20),
            (N'SILVER', N'BASIC_PLUS', N'Basic Plus', 7.0000, 30),
            (N'SILVER', N'BASIC', N'Basic', 3.5000, 40),
            (N'GOLD', N'PREMIUM_PLUS', N'Premium Plus', 11.5000, 10),
            (N'GOLD', N'PREMIUM', N'Premium', 9.5000, 20),
            (N'GOLD', N'BASIC_PLUS', N'Basic Plus', 7.5000, 30),
            (N'GOLD', N'BASIC', N'Basic', 4.0000, 40),
            (N'PLATINUM', N'PREMIUM_PLUS', N'Premium Plus', 12.0000, 10),
            (N'PLATINUM', N'PREMIUM', N'Premium', 10.0000, 20),
            (N'PLATINUM', N'BASIC_PLUS', N'Basic Plus', 8.0000, 30),
            (N'PLATINUM', N'BASIC', N'Basic', 4.5000, 40)
    ) AS v(LevelCode, ProductCode, ProductName, CommissionRate, SortOrder)
        ON v.LevelCode = l.LevelCode
) AS source
ON target.AccountLevelId = source.AccountLevelId
   AND target.ProductCode = source.ProductCode
WHEN MATCHED THEN
    UPDATE SET
        target.ProductName = source.ProductName,
        target.CommissionRate = source.CommissionRate,
        target.SortOrder = source.SortOrder,
        target.IsEnabled = source.IsEnabled,
        target.Remark = source.Remark,
        target.UpdateTime = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (AccountLevelId, ProductCode, ProductName, CommissionRate, SortOrder, IsEnabled, Remark, CreateTime, UpdateTime)
    VALUES (source.AccountLevelId, source.ProductCode, source.ProductName, source.CommissionRate, source.SortOrder, source.IsEnabled, source.Remark, SYSUTCDATETIME(), SYSUTCDATETIME());
GO
