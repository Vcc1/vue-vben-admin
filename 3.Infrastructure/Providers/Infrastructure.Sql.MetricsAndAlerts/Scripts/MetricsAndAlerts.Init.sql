SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.MonitorTargetGroup', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MonitorTargetGroup
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MonitorTargetGroup PRIMARY KEY,
        GroupName NVARCHAR(256) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_MonitorTargetGroup_SortOrder DEFAULT 0,
        IsEnabled BIT NOT NULL CONSTRAINT DF_MonitorTargetGroup_IsEnabled DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MonitorTargetGroup_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MonitorTargetGroup_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_MonitorTargetGroup_GroupName ON dbo.MonitorTargetGroup(GroupName);
    CREATE INDEX IX_MonitorTargetGroup_Enabled_Sort ON dbo.MonitorTargetGroup(IsEnabled, SortOrder);
END;
GO

IF OBJECT_ID(N'dbo.MonitorTarget', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MonitorTarget
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MonitorTarget PRIMARY KEY,
        GroupId BIGINT NULL,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetName NVARCHAR(256) NULL,
        TargetType INT NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_MonitorTarget_SortOrder DEFAULT 0,
        NotifyUsers NVARCHAR(1024) NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_MonitorTarget_IsEnabled DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MonitorTarget_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MonitorTarget_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_MonitorTarget_MonitorTargetGroup FOREIGN KEY(GroupId) REFERENCES dbo.MonitorTargetGroup(Id) ON DELETE SET NULL
    );

    CREATE UNIQUE INDEX UX_MonitorTarget_TargetKey_Type ON dbo.MonitorTarget(TargetKey, TargetType);
    CREATE INDEX IX_MonitorTarget_GroupId ON dbo.MonitorTarget(GroupId);
    CREATE INDEX IX_MonitorTarget_Group_Sort ON dbo.MonitorTarget(GroupId, SortOrder);
END;
GO

IF COL_LENGTH(N'dbo.MonitorTarget', N'SortOrder') IS NULL
BEGIN
    ALTER TABLE dbo.MonitorTarget
    ADD SortOrder INT NOT NULL CONSTRAINT DF_MonitorTarget_SortOrder DEFAULT 0 WITH VALUES;
END;
GO

IF COL_LENGTH(N'dbo.MonitorTarget', N'NotifyUsers') IS NULL
BEGIN
    ALTER TABLE dbo.MonitorTarget
    ADD NotifyUsers NVARCHAR(1024) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MonitorTarget') AND name = N'IX_MonitorTarget_Group_Sort')
BEGIN
    CREATE INDEX IX_MonitorTarget_Group_Sort ON dbo.MonitorTarget(GroupId, SortOrder);
END;
GO

IF OBJECT_ID(N'dbo.TargetAliveAlertRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TargetAliveAlertRule
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TargetAliveAlertRule PRIMARY KEY,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        MinAliveCount INT NOT NULL CONSTRAINT DF_TargetAliveAlertRule_MinAliveCount DEFAULT 1,
        OfflineSeconds INT NOT NULL CONSTRAINT DF_TargetAliveAlertRule_OfflineSeconds DEFAULT 60,
        DurationMinutes INT NOT NULL CONSTRAINT DF_TargetAliveAlertRule_DurationMinutes DEFAULT 0,
        NotifyMinuteQueue NVARCHAR(256) NOT NULL CONSTRAINT DF_TargetAliveAlertRule_NotifyMinuteQueue DEFAULT '0,5,10,120',
        IsEnabled BIT NOT NULL CONSTRAINT DF_TargetAliveAlertRule_IsEnabled DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TargetAliveAlertRule_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_TargetAliveAlertRule_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_TargetAliveAlertRule_Target ON dbo.TargetAliveAlertRule(TargetKey, TargetType);
END;
GO

IF OBJECT_ID(N'dbo.TargetUrlMonitorRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TargetUrlMonitorRule
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TargetUrlMonitorRule PRIMARY KEY,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        UrlName NVARCHAR(256) NULL,
        Url NVARCHAR(1024) NOT NULL,
        HttpMethod NVARCHAR(16) NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_HttpMethod DEFAULT 'GET',
        TimeoutSeconds INT NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_TimeoutSeconds DEFAULT 10,
        CheckIntervalSeconds INT NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_CheckIntervalSeconds DEFAULT 30,
        ExpectedStatusCodes NVARCHAR(128) NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_ExpectedStatusCodes DEFAULT '200',
        DurationMinutes INT NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_DurationMinutes DEFAULT 0,
        NotifyMinuteQueue NVARCHAR(256) NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_NotifyMinuteQueue DEFAULT '0,5,10,120',
        IsEnabled BIT NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_IsEnabled DEFAULT 1,
        LastCheckAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_TargetUrlMonitorRule_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_TargetUrlMonitorRule_Target ON dbo.TargetUrlMonitorRule(TargetKey, TargetType);
    CREATE INDEX IX_TargetUrlMonitorRule_Enabled_LastCheck ON dbo.TargetUrlMonitorRule(IsEnabled, LastCheckAt);
END;
GO

IF OBJECT_ID(N'dbo.AlertState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertState
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AlertState PRIMARY KEY,
        AlertType INT NOT NULL,
        RuleId BIGINT NOT NULL,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        AlertKey NVARCHAR(512) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AlertState_IsActive DEFAULT 0,
        FirstAbnormalAt DATETIME2 NULL,
        FiringAt DATETIME2 NULL,
        RecoveredAt DATETIME2 NULL,
        LastNotifyAt DATETIME2 NULL,
        NextNotifyIndex INT NOT NULL CONSTRAINT DF_AlertState_NextNotifyIndex DEFAULT 0,
        CurrentValue NVARCHAR(256) NULL,
        ThresholdValue NVARCHAR(256) NULL,
        LastError NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_AlertState_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_AlertState_AlertKey ON dbo.AlertState(AlertKey);
    CREATE INDEX IX_AlertState_Filter ON dbo.AlertState(TargetKey, TargetType, AlertType, IsActive);
END;
GO

IF OBJECT_ID(N'dbo.AlertNotifyLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertNotifyLog
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AlertNotifyLog PRIMARY KEY,
        AlertStateId BIGINT NOT NULL,
        AlertType INT NOT NULL,
        NotifyType INT NOT NULL,
        NotifyMinute INT NULL,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        Title NVARCHAR(256) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        IsSuccess BIT NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AlertNotifyLog_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_AlertNotifyLog_Filter ON dbo.AlertNotifyLog(TargetKey, TargetType, AlertType, CreatedAt);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.MonitorTargetGroup') AND name = N'MS_Description' AND minor_id = 0)
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'监控目标分组表', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'MonitorTargetGroup';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.MonitorTarget') AND name = N'MS_Description' AND minor_id = 0)
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'监控目标表，按 TargetKey + TargetType 唯一', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'MonitorTarget';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.MonitorTarget') AND name = N'MS_Description' AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MonitorTarget'), N'SortOrder', 'ColumnId'))
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目标排序序号，分组内按此排序', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'MonitorTarget', @level2type=N'COLUMN', @level2name=N'SortOrder';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.MonitorTarget') AND name = N'MS_Description' AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MonitorTarget'), N'NotifyUsers', 'ColumnId'))
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目标告警人 userid 列表，英文逗号分隔', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'MonitorTarget', @level2type=N'COLUMN', @level2name=N'NotifyUsers';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.TargetAliveAlertRule') AND name = N'MS_Description' AND minor_id = 0)
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目标在线数量告警规则表', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'TargetAliveAlertRule';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.TargetUrlMonitorRule') AND name = N'MS_Description' AND minor_id = 0)
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'目标 URL 监控规则表', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'TargetUrlMonitorRule';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.AlertState') AND name = N'MS_Description' AND minor_id = 0)
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'统一告警状态表', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'AlertState';
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.AlertNotifyLog') AND name = N'MS_Description' AND minor_id = 0)
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'告警通知日志表', @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'AlertNotifyLog';
GO
