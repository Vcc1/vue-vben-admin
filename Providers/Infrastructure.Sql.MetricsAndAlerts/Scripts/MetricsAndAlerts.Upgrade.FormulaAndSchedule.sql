SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.MetricAlertRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MetricAlertRule
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MetricAlertRule PRIMARY KEY,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        RuleName NVARCHAR(256) NOT NULL,
        QueryItemsJson NVARCHAR(MAX) NOT NULL,
        Formula NVARCHAR(2048) NOT NULL,
        DurationMinutes INT NOT NULL CONSTRAINT DF_MetricAlertRule_DurationMinutes DEFAULT 0,
        NotifyMinuteQueue NVARCHAR(256) NOT NULL CONSTRAINT DF_MetricAlertRule_NotifyMinuteQueue DEFAULT '0,5,10,120',
        NotifyUsers NVARCHAR(1024) NULL,
        MessageTemplate NVARCHAR(MAX) NULL,
        RecoverTemplate NVARCHAR(MAX) NULL,
        LastValuesJson NVARCHAR(MAX) NULL,
        AggregateInstances BIT NOT NULL CONSTRAINT DF_MetricAlertRule_AggregateInstances DEFAULT 0,
        IsEnabled BIT NOT NULL CONSTRAINT DF_MetricAlertRule_IsEnabled DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MetricAlertRule_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MetricAlertRule_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_MetricAlertRule_Target_Enabled
        ON dbo.MetricAlertRule(TargetKey, TargetType, IsEnabled);
END;
GO

IF COL_LENGTH(N'dbo.MetricAlertRule', N'AggregateInstances') IS NULL
BEGIN
    ALTER TABLE dbo.MetricAlertRule
        ADD AggregateInstances BIT NOT NULL
            CONSTRAINT DF_MetricAlertRule_AggregateInstances DEFAULT 0;
END;
GO

IF OBJECT_ID(N'dbo.ScheduledMetricMessageRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledMetricMessageRule
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduledMetricMessageRule PRIMARY KEY,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        RuleName NVARCHAR(256) NOT NULL,
        QueryItemsJson NVARCHAR(MAX) NOT NULL,
        MessageTemplate NVARCHAR(MAX) NOT NULL,
        Cron NVARCHAR(128) NOT NULL,
        TimeZoneId NVARCHAR(128) NOT NULL CONSTRAINT DF_ScheduledMetricMessageRule_TimeZoneId DEFAULT 'Asia/Shanghai',
        NextRunAt DATETIME2 NULL,
        LastRunAt DATETIME2 NULL,
        NotifyUsers NVARCHAR(1024) NULL,
        AggregateInstances BIT NOT NULL CONSTRAINT DF_ScheduledMetricMessageRule_AggregateInstances DEFAULT 0,
        IsEnabled BIT NOT NULL CONSTRAINT DF_ScheduledMetricMessageRule_IsEnabled DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ScheduledMetricMessageRule_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ScheduledMetricMessageRule_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_ScheduledMetricMessageRule_Target_Enabled
        ON dbo.ScheduledMetricMessageRule(TargetKey, TargetType, IsEnabled);

    CREATE INDEX IX_ScheduledMetricMessageRule_Due
        ON dbo.ScheduledMetricMessageRule(IsEnabled, NextRunAt);
END;
GO

IF COL_LENGTH(N'dbo.ScheduledMetricMessageRule', N'AggregateInstances') IS NULL
BEGIN
    ALTER TABLE dbo.ScheduledMetricMessageRule
        ADD AggregateInstances BIT NOT NULL
            CONSTRAINT DF_ScheduledMetricMessageRule_AggregateInstances DEFAULT 0;
END;
GO

IF OBJECT_ID(N'dbo.ScheduledMessageSendLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledMessageSendLog
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduledMessageSendLog PRIMARY KEY,
        RuleId BIGINT NOT NULL,
        TargetKey NVARCHAR(512) NOT NULL,
        TargetType INT NOT NULL,
        Title NVARCHAR(256) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        ScheduledAt DATETIME2 NULL,
        NextRunAt DATETIME2 NULL,
        IsSuccess BIT NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ScheduledMessageSendLog_CreatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_ScheduledMessageSendLog_Rule_CreatedAt
        ON dbo.ScheduledMessageSendLog(RuleId, CreatedAt);

    CREATE INDEX IX_ScheduledMessageSendLog_Target_CreatedAt
        ON dbo.ScheduledMessageSendLog(TargetKey, TargetType, CreatedAt);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE major_id = OBJECT_ID(N'dbo.MetricAlertRule')
      AND name = N'MS_Description'
      AND minor_id = 0)
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'公式指标告警规则表',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'MetricAlertRule';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE major_id = OBJECT_ID(N'dbo.MetricAlertRule')
      AND name = N'MS_Description'
      AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MetricAlertRule'), N'QueryItemsJson', 'ColumnId'))
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Prometheus 查询变量 JSON，包含 name、promql、valueMode',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'MetricAlertRule',
        @level2type = N'COLUMN', @level2name = N'QueryItemsJson';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE major_id = OBJECT_ID(N'dbo.MetricAlertRule')
      AND name = N'MS_Description'
      AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MetricAlertRule'), N'Formula', 'ColumnId'))
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'轻量公式表达式，结果必须为布尔值',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'MetricAlertRule',
        @level2type = N'COLUMN', @level2name = N'Formula';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE major_id = OBJECT_ID(N'dbo.MetricAlertRule')
      AND name = N'MS_Description'
      AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.MetricAlertRule'), N'LastValuesJson', 'ColumnId'))
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'上一轮按实例保存的变量值 JSON，用于 changed 和 delta 函数',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'MetricAlertRule',
        @level2type = N'COLUMN', @level2name = N'LastValuesJson';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE major_id = OBJECT_ID(N'dbo.ScheduledMetricMessageRule')
      AND name = N'MS_Description'
      AND minor_id = 0)
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'定时指标消息规则表',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'ScheduledMetricMessageRule';
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE major_id = OBJECT_ID(N'dbo.ScheduledMessageSendLog')
      AND name = N'MS_Description'
      AND minor_id = 0)
BEGIN
    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'定时指标消息发送日志表',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'ScheduledMessageSendLog';
END;
GO
