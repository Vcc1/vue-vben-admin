/*
  Session management and graded login tables for Admin.Host.
  Run manually against the RoomMapping database. This script is idempotent and
  intentionally does not alter or reference existing business tables.

  Adds:
    dbo.Identity_Sessions        - one row per login credential (sid)
    dbo.Identity_TrustedDevices  - (user, device) trust relation for graded login
  Alters:
    dbo.Identity_RefreshTokens     - + Sid, DeviceId, UserAgent
    dbo.IdentityOIDC_Applications  - + BackChannelLogoutUri, RequiredSessionLevel
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Identity_Sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_Sessions
    (
        Sid varchar(64) NOT NULL CONSTRAINT PK_Identity_Sessions PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        DeviceId nvarchar(64) NULL,
        DeviceInfoJson nvarchar(max) NULL,
        LoginMethod varchar(20) NOT NULL CONSTRAINT DF_Identity_Sessions_LoginMethod DEFAULT('pwd'),
        PasswordVerifiedAtUtc datetime2(3) NULL,
        StrongAuthAtUtc datetime2(3) NULL,
        StrongAuthMethod varchar(20) NULL,
        ClientIdsJson nvarchar(1000) NULL,
        CreatedByIp nvarchar(64) NULL,
        UserAgent nvarchar(512) NULL,
        CreatedAtUtc datetime2(3) NOT NULL,
        LastActivityAtUtc datetime2(3) NOT NULL,
        ExpiresAtUtc datetime2(3) NOT NULL,
        RevokedAtUtc datetime2(3) NULL,
        RevokedReason nvarchar(200) NULL,
        RevokedByActorId nvarchar(100) NULL,
        RevokedByActorName nvarchar(100) NULL
    );
    CREATE INDEX IX_Identity_Sessions_User_Revoked
        ON dbo.Identity_Sessions(UserId, RevokedAtUtc);
    CREATE INDEX IX_Identity_Sessions_DeviceId
        ON dbo.Identity_Sessions(DeviceId) WHERE DeviceId IS NOT NULL;
    CREATE INDEX IX_Identity_Sessions_LastActivity
        ON dbo.Identity_Sessions(LastActivityAtUtc);
END;

IF OBJECT_ID(N'dbo.Identity_TrustedDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Identity_TrustedDevices
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_TrustedDevices PRIMARY KEY,
        UserId uniqueidentifier NOT NULL,
        DeviceId nvarchar(64) NOT NULL,
        DeviceInfoJson nvarchar(max) NULL,
        FirstSeenUtc datetime2(3) NOT NULL,
        LastSeenUtc datetime2(3) NOT NULL,
        LastLoginSid varchar(64) NULL,
        Status varchar(20) NOT NULL CONSTRAINT DF_Identity_TrustedDevices_Status DEFAULT('trusted'),
        RevokedReason nvarchar(200) NULL,
        RevokedAtUtc datetime2(3) NULL,
        RevokedByActorId nvarchar(100) NULL,
        CONSTRAINT FK_Identity_TrustedDevices_Users FOREIGN KEY(UserId)
            REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_Identity_TrustedDevices_User_Device
        ON dbo.Identity_TrustedDevices(UserId, DeviceId);
END;

IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'Sid') IS NULL
    ALTER TABLE dbo.Identity_RefreshTokens ADD Sid varchar(64) NULL;

IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'DeviceId') IS NULL
    ALTER TABLE dbo.Identity_RefreshTokens ADD DeviceId nvarchar(64) NULL;

IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'UserAgent') IS NULL
    ALTER TABLE dbo.Identity_RefreshTokens ADD UserAgent nvarchar(512) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Identity_RefreshTokens_Sid'
               AND object_id = OBJECT_ID(N'dbo.Identity_RefreshTokens'))
    CREATE INDEX IX_Identity_RefreshTokens_Sid
        ON dbo.Identity_RefreshTokens(Sid) WHERE Sid IS NOT NULL;

IF OBJECT_ID(N'dbo.IdentityOIDC_Applications', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'BackChannelLogoutUri') IS NULL
        ALTER TABLE dbo.IdentityOIDC_Applications ADD BackChannelLogoutUri nvarchar(400) NULL;

    IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'RequiredSessionLevel') IS NULL
        ALTER TABLE dbo.IdentityOIDC_Applications
            ADD RequiredSessionLevel int NOT NULL CONSTRAINT DF_IdentityOIDC_Applications_Level DEFAULT(1);
END;

COMMIT TRANSACTION;
