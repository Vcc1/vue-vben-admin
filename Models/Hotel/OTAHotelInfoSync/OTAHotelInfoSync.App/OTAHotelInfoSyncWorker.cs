using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OTAHotelInfoSync;

public sealed class OTAHotelInfoSyncWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<OTAHotelInfoSyncOptions> _options;
    readonly ILogger<OTAHotelInfoSyncWorker> _logger;

    public OTAHotelInfoSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OTAHotelInfoSyncOptions> options,
        ILogger<OTAHotelInfoSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceSafeAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.CurrentValue.IntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceSafeAsync(stoppingToken);
        }
    }

    async Task RunOnceSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IOTAHotelInfoSyncService>();
            await service.RunOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OTA hotel information synchronization failed.");
        }
    }
}
