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
            if (_options.Command == "clean-once")
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider
                    .GetRequiredService<HotelCleaningService>()
                    .RunOnceAsync(stoppingToken);
                _logger.LogInformation("Hotel cleaning completed. Count={Count}", count);
            }
            else if (_options.Command == "match-loop")
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider
                    .GetRequiredService<HotelMatchingService>()
                    .RunLoopAsync(stoppingToken);
            }
            else
            {
                await RunAllAsync(stoppingToken);
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

    async Task RunAllAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting cleaning and matching pipelines concurrently.");

        var cleaningTask = Task.Run(
            async () =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                _logger.LogInformation("Hotel cleaning pipeline started.");
                return await scope.ServiceProvider
                    .GetRequiredService<HotelCleaningService>()
                    .RunOnceAsync(stoppingToken);
            },
            CancellationToken.None);

        var matchingTask = Task.Run(
            async () =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                _logger.LogInformation("Hotel matching pipeline started.");
                await scope.ServiceProvider
                    .GetRequiredService<HotelMatchingService>()
                    .RunLoopAsync(stoppingToken);
            },
            CancellationToken.None);

        var firstCompletedTask = await Task.WhenAny(cleaningTask, matchingTask);
        if (firstCompletedTask == matchingTask)
        {
            await matchingTask;
            return;
        }

        var cleanedCount = await cleaningTask;
        _logger.LogInformation(
            "Hotel cleaning completed while matching continues. Count={Count}",
            cleanedCount);
        await matchingTask;
    }
}
