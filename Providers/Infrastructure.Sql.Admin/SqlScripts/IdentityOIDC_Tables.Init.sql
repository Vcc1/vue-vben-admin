/*
  OpenIddict OIDC tables for Admin.Host.
  Run manually against the RoomMapping database after AdminTables.Init.sql.
  This script is idempotent.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- IdentityOIDC_Applications: OIDC client applications
IF OBJECT_ID(N'dbo.IdentityOIDC_Applications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdentityOIDC_Applications
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_IdentityOIDC_Applications PRIMARY KEY,
        ClientId nvarchar(100) NOT NULL,
        ClientSecret nvarchar(400) NULL,
        ClientType nvarchar(50) NOT NULL,
        ApplicationType nvarchar(50) NULL,
        ConsentType nvarchar(50) NOT NULL,
        DisplayName nvarchar(200) NULL,
        RedirectUris nvarchar(max) NULL,
        PostLogoutRedirectUris nvarchar(max) NULL,
        Permissions nvarchar(max) NULL,
        Requirements nvarchar(max) NULL,
        Properties nvarchar(max) NULL,
        ConcurrencyToken nvarchar(50) NULL,
        CreationDate datetime2(3) NULL,
        UpdatedDate datetime2(3) NULL
    );
    CREATE UNIQUE INDEX UX_IdentityOIDC_Applications_ClientId
        ON dbo.IdentityOIDC_Applications(ClientId);
END;

-- IdentityOIDC_Scopes: Available scopes for OIDC clients
IF OBJECT_ID(N'dbo.IdentityOIDC_Scopes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdentityOIDC_Scopes
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_IdentityOIDC_Scopes PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        DisplayName nvarchar(200) NULL,
        Description nvarchar(1000) NULL,
        Resources nvarchar(max) NULL,
        Properties nvarchar(max) NULL,
        ConcurrencyToken nvarchar(50) NULL
    );
    CREATE UNIQUE INDEX UX_IdentityOIDC_Scopes_Name
        ON dbo.IdentityOIDC_Scopes(Name);
END;

-- IdentityOIDC_Authorizations: User authorization records
IF OBJECT_ID(N'dbo.IdentityOIDC_Authorizations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdentityOIDC_Authorizations
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_IdentityOIDC_Authorizations PRIMARY KEY,
        ApplicationId uniqueidentifier NOT NULL,
        Scopes nvarchar(max) NULL,
        Subject nvarchar(200) NOT NULL,
        Status nvarchar(50) NOT NULL,
        Properties nvarchar(max) NULL,
        CreationDate datetime2(3) NULL,
        CONSTRAINT FK_IdentityOIDC_Authorizations_Applications
            FOREIGN KEY (ApplicationId) REFERENCES dbo.IdentityOIDC_Applications(Id)
            ON DELETE NO ACTION
    );
    CREATE INDEX IX_IdentityOIDC_Authorizations_ApplicationId
        ON dbo.IdentityOIDC_Authorizations(ApplicationId);
    CREATE INDEX IX_IdentityOIDC_Authorizations_Subject
        ON dbo.IdentityOIDC_Authorizations(Subject);
END;

-- IdentityOIDC_Tokens: Issued tokens (access, refresh, etc.)
IF OBJECT_ID(N'dbo.IdentityOIDC_Tokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdentityOIDC_Tokens
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_IdentityOIDC_Tokens PRIMARY KEY,
        ApplicationId uniqueidentifier NOT NULL,
        AuthorizationId uniqueidentifier NULL,
        Type nvarchar(50) NOT NULL,
        Subject nvarchar(200) NULL,
        Status nvarchar(50) NOT NULL,
        ReferenceId nvarchar(100) NULL,
        Payload nvarchar(max) NULL,
        Properties nvarchar(max) NULL,
        CreationDate datetime2(3) NULL,
        ExpirationDate datetime2(3) NULL,
        RedemptionDate datetime2(3) NULL,
        ConcurrencyToken nvarchar(50) NULL,
        CONSTRAINT FK_IdentityOIDC_Tokens_Applications
            FOREIGN KEY (ApplicationId) REFERENCES dbo.IdentityOIDC_Applications(Id)
            ON DELETE NO ACTION,
        CONSTRAINT FK_IdentityOIDC_Tokens_Authorizations
            FOREIGN KEY (AuthorizationId) REFERENCES dbo.IdentityOIDC_Authorizations(Id)
            ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_IdentityOIDC_Tokens_ReferenceId
        ON dbo.IdentityOIDC_Tokens(ReferenceId) WHERE ReferenceId IS NOT NULL;
    CREATE INDEX IX_IdentityOIDC_Tokens_ApplicationId
        ON dbo.IdentityOIDC_Tokens(ApplicationId);
    CREATE INDEX IX_IdentityOIDC_Tokens_AuthorizationId
        ON dbo.IdentityOIDC_Tokens(AuthorizationId);
    CREATE INDEX IX_IdentityOIDC_Tokens_Subject
        ON dbo.IdentityOIDC_Tokens(Subject);
END;

-- Insert default scopes if not exists
IF NOT EXISTS (SELECT 1 FROM dbo.IdentityOIDC_Scopes WHERE Name = 'openid')
BEGIN
    INSERT INTO dbo.IdentityOIDC_Scopes (Id, Name, DisplayName, Description, Resources, Properties, ConcurrencyToken)
    VALUES (NEWID(), 'openid', 'OpenID', 'OpenID Connect scope', '[]', '{}', NEWID());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.IdentityOIDC_Scopes WHERE Name = 'profile')
BEGIN
    INSERT INTO dbo.IdentityOIDC_Scopes (Id, Name, DisplayName, Description, Resources, Properties, ConcurrencyToken)
    VALUES (NEWID(), 'profile', 'Profile', 'User profile information', '[]', '{}', NEWID());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.IdentityOIDC_Scopes WHERE Name = 'email')
BEGIN
    INSERT INTO dbo.IdentityOIDC_Scopes (Id, Name, DisplayName, Description, Resources, Properties, ConcurrencyToken)
    VALUES (NEWID(), 'email', 'Email', 'User email address', '[]', '{}', NEWID());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.IdentityOIDC_Scopes WHERE Name = 'roles')
BEGIN
    INSERT INTO dbo.IdentityOIDC_Scopes (Id, Name, DisplayName, Description, Resources, Properties, ConcurrencyToken)
    VALUES (NEWID(), 'roles', 'Roles', 'User roles', '[]', '{}', NEWID());
END;

COMMIT TRANSACTION;
PRINT 'OIDC tables created successfully.';
