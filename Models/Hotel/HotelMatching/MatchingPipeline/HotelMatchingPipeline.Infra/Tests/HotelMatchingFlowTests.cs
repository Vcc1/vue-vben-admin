using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Infra.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class HotelMatchingFlowTests
{
    [Fact]
    public async Task Matching_pipeline_processes_bounded_queues_and_batches_writes()
    {
        var pipelineRepository = new PipelineRepository(501);
        using var serviceProvider = CreateServiceProvider(pipelineRepository);
        var sharedStore = new InMemoryHotelMatchingStore();
        var service = new HotelMatchingService(
            pipelineRepository,
            sharedStore,
            sharedStore,
            new RecordingHotelMatchCache(),
            new InMemoryThirdPartyHotelMatchClient(),
            new HotelFactMatchEvaluator(),
            Options.Create(new HotelMatchingOptions
            {
                Version = 7,
                QueueCapacity = 999,
                WriteBatchSize = 100,
                RecallRadiusMeters = 3_000,
                MaxCandidates = 200,
                MatchDistanceMeters = 60
            }),
            NullLogger<HotelMatchingService>.Instance,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var processed = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(501, processed);
        Assert.Equal(501, pipelineRepository.SavedEvaluations.Count);
        Assert.All(pipelineRepository.WriteBatchSizes, size => Assert.InRange(size, 1, 100));
        Assert.Equal(6, pipelineRepository.WriteBatchSizes.Count);
        Assert.All(pipelineRepository.MatchingVersions, version => Assert.Equal(7, version));
    }

    [Fact]
    public async Task Information_load_retries_same_hotel_until_it_succeeds()
    {
        var pipelineRepository = new PipelineRepository(1, loadFailuresBeforeSuccess: 2);
        var scopeResolutions = 0;
        using var serviceProvider = new ServiceCollection()
            .AddScoped<IHotelMatchPipelineRepository>(_ =>
            {
                Interlocked.Increment(ref scopeResolutions);
                return pipelineRepository;
            })
            .BuildServiceProvider();
        var sharedStore = new InMemoryHotelMatchingStore();
        var service = new HotelMatchingService(
            pipelineRepository,
            sharedStore,
            sharedStore,
            new RecordingHotelMatchCache(),
            new InMemoryThirdPartyHotelMatchClient(),
            new HotelFactMatchEvaluator(),
            Options.Create(new HotelMatchingOptions
            {
                QueueCapacity = 1,
                WriteBatchSize = 1,
                InformationRetryDelayMilliseconds = 1
            }),
            NullLogger<HotelMatchingService>.Instance,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var processed = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(3, pipelineRepository.InformationLoadAttempts);
        Assert.Equal(3, scopeResolutions);
        Assert.Single(pipelineRepository.SavedEvaluations);
    }

    [Fact]
    public async Task Information_load_keeps_retrying_and_reports_retrying_state()
    {
        var pipelineRepository = new PipelineRepository(1, alwaysFailSupHotelId: 1);
        using var serviceProvider = CreateServiceProvider(pipelineRepository);
        var sharedStore = new InMemoryHotelMatchingStore();
        var metrics = new HotelMatchingMetrics();
        var service = new HotelMatchingService(
            pipelineRepository,
            sharedStore,
            sharedStore,
            new RecordingHotelMatchCache(),
            new InMemoryThirdPartyHotelMatchClient(),
            new HotelFactMatchEvaluator(),
            Options.Create(new HotelMatchingOptions
            {
                QueueCapacity = 1,
                WriteBatchSize = 1,
                InformationRetryDelayMilliseconds = 1,
                ProgressLogIntervalMilliseconds = 1_000
            }),
            NullLogger<HotelMatchingService>.Instance,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            metrics);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RunOnceAsync(cancellation.Token));

        var snapshot = metrics.Snapshot();
        Assert.True(snapshot.InformationRetryCount > 0);
        Assert.True(snapshot.InformationRetrying > 0);
        Assert.Equal(1, snapshot.InformationRetrySupHotelId);
        Assert.Empty(pipelineRepository.SavedEvaluations);
    }

    [Fact]
    public async Task Information_load_uses_configured_parallelism()
    {
        var pipelineRepository = new PipelineRepository(
            count: 12,
            informationDelayMilliseconds: 30);
        using var serviceProvider = CreateServiceProvider(pipelineRepository);
        var sharedStore = new InMemoryHotelMatchingStore();
        var service = new HotelMatchingService(
            pipelineRepository,
            sharedStore,
            sharedStore,
            new RecordingHotelMatchCache(),
            new InMemoryThirdPartyHotelMatchClient(),
            new HotelFactMatchEvaluator(),
            Options.Create(new HotelMatchingOptions
            {
                QueueCapacity = 12,
                WriteBatchSize = 12,
                InformationParallelism = 4
            }),
            NullLogger<HotelMatchingService>.Instance,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var processed = await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(12, processed);
        Assert.Equal(4, pipelineRepository.MaximumInformationConcurrency);
    }

    static ServiceProvider CreateServiceProvider(IHotelMatchPipelineRepository repository)
        => new ServiceCollection()
            .AddScoped<IHotelMatchPipelineRepository>(_ => repository)
            .BuildServiceProvider();

    sealed class RecordingHotelMatchCache : IHotelMatchCache
    {
        public Task SaveAsync(
            IReadOnlyCollection<HotelMatchEvaluation> evaluations,
            int matchingVersion,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    sealed class PipelineRepository(
        int count,
        int loadFailuresBeforeSuccess = 0,
        int? alwaysFailSupHotelId = null,
        int informationDelayMilliseconds = 0) : IHotelMatchPipelineRepository
    {
        int _remainingLoadFailures = loadFailuresBeforeSuccess;
        int _informationLoadAttempts;
        int _activeInformationLoads;
        int _maximumInformationConcurrency;

        public int InformationLoadAttempts => Volatile.Read(ref _informationLoadAttempts);
        public int MaximumInformationConcurrency => Volatile.Read(ref _maximumInformationConcurrency);
        public List<int> WriteBatchSizes { get; } = [];
        public List<int> MatchingVersions { get; } = [];
        public List<HotelMatchEvaluation> SavedEvaluations { get; } = [];

        public async Task LoadPendingSupplierHotelIdsAsync(
            Func<int, CancellationToken, ValueTask> enqueueId,
            CancellationToken cancellationToken)
        {
            for (var id = 1; id <= count; id++)
            {
                await enqueueId(id, cancellationToken);
            }
        }

        public async Task<HotelMatchingWorkItem> LoadMatchingWorkItemAsync(
            int supHotelId,
            int recallRadiusMeters,
            int maxCandidates,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _informationLoadAttempts);
            var active = Interlocked.Increment(ref _activeInformationLoads);
            UpdateMaximum(ref _maximumInformationConcurrency, active);
            try
            {
                if (Interlocked.Decrement(ref _remainingLoadFailures) >= 0)
                {
                    throw new TimeoutException("Simulated information load timeout.");
                }
                if (alwaysFailSupHotelId == supHotelId)
                {
                    throw new TimeoutException("Simulated permanent information load timeout.");
                }
                if (informationDelayMilliseconds > 0)
                {
                    await Task.Delay(informationDelayMilliseconds, cancellationToken);
                }

                var pending = Fact(
                    HotelMatchFactSource.Supplier,
                    supHotelId,
                    null,
                    "Test Hotel",
                    121m,
                    31m);
                var ota = Fact(
                    HotelMatchFactSource.Ota,
                    10_000 + supHotelId,
                    $"MT-{supHotelId}",
                    "Test Hotel",
                    121.0001m,
                    31m);
                return new HotelMatchingWorkItem(
                    supHotelId,
                    pending,
                    [ota],
                    [],
                    []);
            }
            finally
            {
                Interlocked.Decrement(ref _activeInformationLoads);
            }
        }

        public Task SaveMatchEvaluationsAsync(
            IReadOnlyCollection<HotelMatchEvaluation> evaluations,
            int matchingVersion,
            CancellationToken cancellationToken)
        {
            WriteBatchSizes.Add(evaluations.Count);
            MatchingVersions.Add(matchingVersion);
            SavedEvaluations.AddRange(evaluations);
            return Task.CompletedTask;
        }

        static void UpdateMaximum(ref int maximum, int value)
        {
            var current = Volatile.Read(ref maximum);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref maximum, value, current);
                if (observed == current)
                {
                    return;
                }
                current = observed;
            }
        }

        static HotelMatchFact Fact(
            HotelMatchFactSource source,
            int id,
            string? mtHotelCode,
            string hotelName,
            decimal longitude,
            decimal latitude)
            => new(
                source,
                id,
                source == HotelMatchFactSource.Supplier ? 51 : null,
                $"H-{id}",
                mtHotelCode,
                hotelName,
                null,
                longitude,
                latitude,
                null,
                null,
                []);
    }
}
