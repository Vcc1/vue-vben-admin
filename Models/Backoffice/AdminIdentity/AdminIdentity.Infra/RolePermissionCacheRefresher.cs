using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdminIdentity.Infra;

/// <summary>
/// 启动时加载一次角色权限映射，之后按固定间隔从数据库刷新到 <see cref="RolePermissionCatalog"/> 内存快照。
/// 间隔由配置 AdminIdentity:RolePermissionCache:RefreshSeconds 控制，默认 60 秒，最小 5 秒。
/// </summary>
internal sealed class RolePermissionCacheRefresher(
    RolePermissionCatalog catalog,
    IConfiguration configuration,
    ILogger<RolePermissionCacheRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = configuration.GetValue("AdminIdentity:RolePermissionCache:RefreshSeconds", 60);
        if (seconds < 5)
        {
            seconds = 5;
        }
        var interval = TimeSpan.FromSeconds(seconds);
        logger.LogInformation("角色权限缓存刷新间隔：{Seconds} 秒。", seconds);

        await catalog.RefreshAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await catalog.RefreshAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
    }
}
