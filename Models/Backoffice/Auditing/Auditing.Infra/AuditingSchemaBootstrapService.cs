using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auditing.Infra;

/// <summary>
/// 操作审计表的幂等加列引导。与 <c>SqlScripts/Auditing_Tables.Init.sql</c> 同源：
/// 脚本负责全新库的标准初始化，这里保证已部署环境升级时不会因为漏跑脚本而让审计写入整条失败。
/// </summary>
internal sealed class AuditingSchemaBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditingSchemaBootstrapService> logger) : IHostedService
{
    // 新加的列必须等下一批次才能被索引定义引用，否则编译期就报"列名无效"。
    private static readonly string[] Ddl =
    [
        """
        IF OBJECT_ID(N'dbo.Auditing_RequestLogs', N'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N'dbo.Auditing_RequestLogs', N'FeatureName') IS NULL
                ALTER TABLE dbo.Auditing_RequestLogs ADD FeatureName nvarchar(100) NULL;

            IF COL_LENGTH(N'dbo.Auditing_RequestLogs', N'OperationName') IS NULL
                ALTER TABLE dbo.Auditing_RequestLogs ADD OperationName nvarchar(100) NULL;
        END;
        """,
        """
        IF OBJECT_ID(N'dbo.Auditing_RequestLogs', N'U') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = N'IX_Auditing_RequestLogs_Feature_Occurred'
                             AND object_id = OBJECT_ID(N'dbo.Auditing_RequestLogs'))
            CREATE INDEX IX_Auditing_RequestLogs_Feature_Occurred
                ON dbo.Auditing_RequestLogs(FeatureName, OccurredAtUtc);
        """,
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        foreach (var batch in Ddl)
        {
            await db.Database.ExecuteSqlRawAsync(batch, cancellationToken);
        }
        logger.LogInformation("Auditing schema check completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
