using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace HotelMatchingPipeline.Cli;

internal sealed record HotelMatchingPipelineCliOptions(string Command, int? DebugSupHotelId);

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
            else if (_options.Command == "match-debug")
            {
                await RunMatchDebugAsync(stoppingToken);
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

    async Task RunMatchDebugAsync(CancellationToken stoppingToken)
    {
        var supHotelId = _options.DebugSupHotelId
            ?? throw new InvalidOperationException("Debug SupHotelId is required.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var repository = provider.GetRequiredService<IHotelMatchPipelineRepository>();
        var evaluator = provider.GetRequiredService<HotelFactMatchEvaluator>();
        var options = provider.GetRequiredService<IOptions<HotelMatchingOptions>>().Value;

        _logger.LogInformation(
            "Hotel matching debug started. SupHotelId={SupHotelId}, RecallRadiusMeters={RecallRadiusMeters}, MaxCandidates={MaxCandidates}, MatchDistanceMeters={MatchDistanceMeters}",
            supHotelId,
            options.RecallRadiusMeters,
            options.MaxCandidates,
            options.MatchDistanceMeters);

        var stopwatch = Stopwatch.StartNew();
        HotelMatchingWorkItem workItem;
        try
        {
            workItem = await repository.LoadMatchingWorkItemAsync(
                supHotelId,
                options.RecallRadiusMeters,
                options.MaxCandidates,
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hotel matching debug recall failed. SupHotelId={SupHotelId}, ElapsedMs={ElapsedMs}",
                supHotelId,
                stopwatch.ElapsedMilliseconds);
            Console.Error.WriteLine($"Recall failed. SupHotelId={supHotelId}, ElapsedMs={stopwatch.ElapsedMilliseconds}");
            Console.Error.WriteLine(ex);
            throw;
        }

        var recallElapsedMs = stopwatch.ElapsedMilliseconds;
        var evaluation = evaluator.Evaluate(workItem, options.MatchDistanceMeters);
        stopwatch.Stop();

        PrintDebugSummary(workItem, evaluation, recallElapsedMs, stopwatch.ElapsedMilliseconds);

        _logger.LogInformation(
            "Hotel matching debug completed. SupHotelId={SupHotelId}, RecallElapsedMs={RecallElapsedMs}, TotalElapsedMs={TotalElapsedMs}, NearbyOtaHotels={NearbyOtaHotels}, NearbySupplierHotels={NearbySupplierHotels}, OtaLinkedSupplierHotels={OtaLinkedSupplierHotels}, Matches={Matches}, ResolvedMTHotelCode={ResolvedMTHotelCode}",
            supHotelId,
            recallElapsedMs,
            stopwatch.ElapsedMilliseconds,
            workItem.NearbyOtaHotels.Count,
            workItem.NearbySupplierHotels.Count,
            workItem.OtaLinkedSupplierHotels.Count,
            evaluation.Matches.Count,
            evaluation.ResolvedMTHotelCode);
    }

    static void PrintDebugSummary(
        HotelMatchingWorkItem workItem,
        HotelMatchEvaluation evaluation,
        long recallElapsedMs,
        long totalElapsedMs)
    {
        Console.WriteLine("========== Hotel matching debug ==========");
        Console.WriteLine($"SupHotelId: {workItem.SupHotelId}");
        Console.WriteLine($"RecallElapsedMs: {recallElapsedMs}");
        Console.WriteLine($"TotalElapsedMs: {totalElapsedMs}");
        Console.WriteLine(
            $"Pending: SupId={workItem.PendingSupplier.SupplierId}, SHotelCode={workItem.PendingSupplier.HotelCode}, MTHotelCode={ValueOrEmpty(workItem.PendingSupplier.MTHotelCode)}, Name={ValueOrEmpty(workItem.PendingSupplier.HotelName)}, NameEn={ValueOrEmpty(workItem.PendingSupplier.HotelNameEn)}, Lat={DecimalOrEmpty(workItem.PendingSupplier.Latitude)}, Lon={DecimalOrEmpty(workItem.PendingSupplier.Longitude)}");
        Console.WriteLine(
            $"RecallCounts: NearbyOta={workItem.NearbyOtaHotels.Count}, NearbySupplier={workItem.NearbySupplierHotels.Count}, OtaLinkedSupplier={workItem.OtaLinkedSupplierHotels.Count}");
        Console.WriteLine(
            $"Evaluation: Matches={evaluation.Matches.Count}, HasConflict={evaluation.HasConflict}, ResolvedMTHotelCode={ValueOrEmpty(evaluation.ResolvedMTHotelCode)}");

        PrintNearestFacts("NearestNearbyOta", workItem.PendingSupplier, workItem.NearbyOtaHotels);
        PrintNearestFacts("NearestNearbySupplier", workItem.PendingSupplier, workItem.NearbySupplierHotels);

        if (evaluation.Matches.Count > 0)
        {
            Console.WriteLine("MatchedCandidates:");
            foreach (var match in evaluation.Matches
                .OrderBy(match => match.CoordinateEvidence.DistanceMeters ?? double.MaxValue)
                .ThenBy(match => match.MTHotelCode, StringComparer.OrdinalIgnoreCase)
                .Take(20))
            {
                Console.WriteLine(
                    $"  MTHotelCode={match.MTHotelCode}, DistanceMeters={DoubleOrEmpty(match.CoordinateEvidence.DistanceMeters)}, NameKind={match.NameMatchKind}, NameHotelId={match.NameEvidence.HotelId}, CoordinateHotelId={match.CoordinateEvidence.HotelId}, OtaFacts={match.OtaFactCount}, SupplierFacts={match.SupplierFactCount}");
            }
        }
    }

    static void PrintNearestFacts(string title, HotelMatchFact pending, IReadOnlyCollection<HotelMatchFact> facts)
    {
        Console.WriteLine($"{title}:");
        foreach (var fact in facts
            .Select(fact => new
            {
                Fact = fact,
                Distance = DistanceMeters(pending.Latitude, pending.Longitude, fact.Latitude, fact.Longitude)
            })
            .OrderBy(candidate => candidate.Distance ?? double.MaxValue)
            .ThenBy(candidate => candidate.Fact.HotelId)
            .Take(20))
        {
            Console.WriteLine(
                $"  Source={fact.Fact.Source}, HotelId={fact.Fact.HotelId}, SupId={fact.Fact.SupplierId?.ToString(CultureInfo.InvariantCulture) ?? ""}, HotelCode={fact.Fact.HotelCode}, MTHotelCode={ValueOrEmpty(fact.Fact.MTHotelCode)}, DistanceMeters={DoubleOrEmpty(fact.Distance)}, Name={ValueOrEmpty(fact.Fact.HotelName)}");
        }
    }

    static double? DistanceMeters(decimal? latitude1, decimal? longitude1, decimal? latitude2, decimal? longitude2)
    {
        if (!latitude1.HasValue || !longitude1.HasValue || !latitude2.HasValue || !longitude2.HasValue)
        {
            return null;
        }

        const double radius = 6371008.8;
        var lat1 = (double)latitude1.Value * Math.PI / 180;
        var lat2 = (double)latitude2.Value * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = ((double)longitude2.Value - (double)longitude1.Value) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    static string ValueOrEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value;

    static string DecimalOrEmpty(decimal? value)
        => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";

    static string DoubleOrEmpty(double? value)
        => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";
}
