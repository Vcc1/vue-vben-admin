using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HotelMatchingPipeline.App;

namespace HotelMatchingPipeline.Cli;

internal sealed record HotelMatchingPipelineCliOptions(string Command);

internal sealed class HotelMatchingPipelineWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IHostApplicationLifetime _lifetime;
    readonly HotelMatchingPipelineCliOptions _options;
    readonly ILogger<HotelMatchingPipelineWorker> _logger;

    public HotelMatchingPipelineWorker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        HotelMatchingPipelineCliOptions options,
        ILogger<HotelMatchingPipelineWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            if (_options.Command == "clean-once")
            {
                var count = await scope.ServiceProvider
                    .GetRequiredService<HotelCleaningService>()
                    .RunOnceAsync(stoppingToken);
                _logger.LogInformation("Hotel cleaning completed. Count={Count}", count);
            }
            else
            {
                await scope.ServiceProvider
                    .GetRequiredService<HotelMatchingService>()
                    .RunLoopAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            _logger.LogError(ex, "Hotel matching pipeline command failed. Command={Command}", _options.Command);
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
