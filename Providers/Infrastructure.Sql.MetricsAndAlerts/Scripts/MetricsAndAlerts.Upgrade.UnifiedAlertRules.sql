SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.TargetAliveAlertRule', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TargetAliveAlertRule', N'RuleName') IS NULL
        ALTER TABLE dbo.TargetAliveAlertRule ADD RuleName NVARCHAR(256) NULL;

    IF COL_LENGTH(N'dbo.TargetAliveAlertRule', N'NotifyUsers') IS NULL
        ALTER TABLE dbo.TargetAliveAlertRule ADD NotifyUsers NVARCHAR(1024) NULL;

    UPDATE dbo.TargetAliveAlertRule
    SET RuleName = N'在线实例数'
    WHERE RuleName IS NULL OR LTRIM(RTRIM(RuleName)) = N'';

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.TargetAliveAlertRule')
          AND name = N'UX_TargetAliveAlertRule_Target'
    )
        DROP INDEX UX_TargetAliveAlertRule_Target ON dbo.TargetAliveAlertRule;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.TargetAliveAlertRule')
          AND name = N'IX_TargetAliveAlertRule_Target'
    )
        CREATE INDEX IX_TargetAliveAlertRule_Target ON dbo.TargetAliveAlertRule(TargetKey, TargetType);
END;
GO

IF OBJECT_ID(N'dbo.TargetUrlMonitorRule', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TargetUrlMonitorRule', N'RuleName') IS NULL
        ALTER TABLE dbo.TargetUrlMonitorRule ADD RuleName NVARCHAR(256) NULL;

    IF COL_LENGTH(N'dbo.TargetUrlMonitorRule', N'NotifyUsers') IS NULL
        ALTER TABLE dbo.TargetUrlMonitorRule ADD NotifyUsers NVARCHAR(1024) NULL;

    UPDATE dbo.TargetUrlMonitorRule
    SET RuleName = COALESCE(NULLIF(LTRIM(RTRIM(UrlName)), N''), NULLIF(LTRIM(RTRIM(Url)), N''), N'URL巡检')
    WHERE RuleName IS NULL OR LTRIM(RTRIM(RuleName)) = N'';
END;
GO

IF OBJECT_ID(N'dbo.TargetAliveAlertRule', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.TargetAliveAlertRule')
          AND name = N'MS_Description'
          AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.TargetAliveAlertRule'), N'RuleName', 'ColumnId')
    )
        EXEC sys.sp_addextendedproperty
            @name=N'MS_Description',
            @value=N'在线实例数告警规则名称',
            @level0type=N'SCHEMA', @level0name=N'dbo',
            @level1type=N'TABLE', @level1name=N'TargetAliveAlertRule',
            @level2type=N'COLUMN', @level2name=N'RuleName';

    IF NOT EXISTS (
        SELECT 1
        FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.TargetAliveAlertRule')
          AND name = N'MS_Description'
          AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.TargetAliveAlertRule'), N'NotifyUsers', 'ColumnId')
    )
        EXEC sys.sp_addextendedproperty
            @name=N'MS_Description',
            @value=N'规则级企业微信通知 userid 列表，英文逗号分隔；为空时使用目标通知人，再为空时使用系统默认通知人',
            @level0type=N'SCHEMA', @level0name=N'dbo',
            @level1type=N'TABLE', @level1name=N'TargetAliveAlertRule',
            @level2type=N'COLUMN', @level2name=N'NotifyUsers';
END;
GO

IF OBJECT_ID(N'dbo.TargetUrlMonitorRule', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.TargetUrlMonitorRule')
          AND name = N'MS_Description'
          AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.TargetUrlMonitorRule'), N'RuleName', 'ColumnId')
    )
        EXEC sys.sp_addextendedproperty
            @name=N'MS_Description',
            @value=N'URL 巡检告警规则名称；为空时前端可使用 UrlName 或 Url 展示',
            @level0type=N'SCHEMA', @level0name=N'dbo',
            @level1type=N'TABLE', @level1name=N'TargetUrlMonitorRule',
            @level2type=N'COLUMN', @level2name=N'RuleName';

    IF NOT EXISTS (
        SELECT 1
        FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.TargetUrlMonitorRule')
          AND name = N'MS_Description'
          AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.TargetUrlMonitorRule'), N'NotifyUsers', 'ColumnId')
    )
        EXEC sys.sp_addextendedproperty
            @name=N'MS_Description',
            @value=N'规则级企业微信通知 userid 列表，英文逗号分隔；为空时使用目标通知人，再为空时使用系统默认通知人',
            @level0type=N'SCHEMA', @level0name=N'dbo',
            @level1type=N'TABLE', @level1name=N'TargetUrlMonitorRule',
            @level2type=N'COLUMN', @level2name=N'NotifyUsers';
END;
GO
