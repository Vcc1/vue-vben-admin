using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OTAHotelInfoSync;

public sealed class OTAHotelInfoSyncWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<OTAHotelInfoSyncOptions> _options;
    readonly OTAHotelInfoSyncRetryExecutor _retryExecutor;

    public OTAHotelInfoSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OTAHotelInfoSyncOptions> options,
        OTAHotelInfoSyncRetryExecutor retryExecutor)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _retryExecutor = retryExecutor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceWithRetryAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.CurrentValue.IntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceWithRetryAsync(stoppingToken);
        }
    }

    Task RunOnceWithRetryAsync(CancellationToken cancellationToken)
    {
        return _retryExecutor.ExecuteAsync(
            "同步任务",
            "执行一次同步周期",
            async token =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IOTAHotelInfoSyncService>();
                await service.RunOnceAsync(token);
            },
            cancellationToken);
    }
}
