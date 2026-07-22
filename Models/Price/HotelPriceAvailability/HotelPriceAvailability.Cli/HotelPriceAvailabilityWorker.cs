using HotelPriceAvailability.App;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HotelPriceAvailability.Cli;

public sealed class HotelPriceAvailabilityWorker : BackgroundService
{
    private readonly HotelPriceAvailabilityJob _job;
    private readonly JobExitState _exitState;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<HotelPriceAvailabilityWorker> _logger;

    public HotelPriceAvailabilityWorker(
        HotelPriceAvailabilityJob job,
        JobExitState exitState,
        IHostApplicationLifetime lifetime,
        ILogger<HotelPriceAvailabilityWorker> logger)
    {
        _job = job;
        _exitState = exitState;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var summary = await _job.ExecuteAsync(stoppingToken);
            _exitState.ExitCode = summary.HasErrors ? 1 : 0;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _exitState.ExitCode = 1;
        }
        catch (Exception ex)
        {
            _exitState.ExitCode = 1;
            _logger.LogCritical(ex, "Hotel price availability job failed.");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}

public sealed class JobExitState
{
    public int ExitCode { get; set; }
}
