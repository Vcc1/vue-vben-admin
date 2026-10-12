using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Infra.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class HotelCleaningPipelineTests
{
    [Fact]
    public void Cleaning_parallelism_defaults_to_eight()
    {
        Assert.Equal(8, new HotelCleaningOptions().CleanParallelism);
    }

    [Fact]
    public async Task Cleaning_pipeline_caps_write_batches_at_one_thousand()
    {
        var store = new InMemoryHotelMatchingStore();
        for (var index = 1; index <= HotelCleaningOptions.MaxSaveBatchSize + 1; index++)
        {
            store.AddRaw(CreateRaw(HotelDataType.Platform, $"MT-{index}", index));
        }

        var repository = new BatchTrackingCleaningRepository(store);
        var service = new HotelCleaningService(
            repository,
            new HotelCleaner(new FixedMissingRegionResolver()),
            Options.Create(new HotelCleaningOptions
            {
                BatchSize = 2_000,
                SaveBatchSize = 5_000,
                CleanParallelism = 8,
                QueueCapacity = 2_000,
                QueueRefillThreshold = 1_000,
                QueueRefillDelaySeconds = 0
            }),
            new NoOpStarLevelIssueRecorder(),
            NullLogger<HotelCleaningService>.Instance);

        Assert.Equal(HotelCleaningOptions.MaxSaveBatchSize + 1, await service.RunOnceAsync(CancellationToken.None));
        Assert.Equal(
            HotelCleaningOptions.MaxSaveBatchSize + 1,
            repository.SavedBatchSizes.Sum());
        Assert.All(
            repository.SavedBatchSizes,
            size => Assert.InRange(size, 1, HotelCleaningOptions.MaxSaveBatchSize));
    }

    [Fact]
    public async Task Cleaning_pipeline_limits_concurrent_clean_tasks()
    {
        var store = new InMemoryHotelMatchingStore();
        for (var index = 1; index <= 24; index++)
        {
            store.AddRaw(CreateRaw(HotelDataType.Platform, $"MT-{index}", index));
        }

        var resolver = new ConcurrencyTrackingRegionResolver();
        var service = new HotelCleaningService(
            store,
            new HotelCleaner(resolver),
            Options.Create(new HotelCleaningOptions
            {
                BatchSize = 16,
                SaveBatchSize = 16,
                CleanParallelism = 4,
                QueueCapacity = 32,
                QueueRefillThreshold = 16,
                QueueRefillDelaySeconds = 0
            }),
            new NoOpStarLevelIssueRecorder(),
            NullLogger<HotelCleaningService>.Instance);

        Assert.Equal(24, await service.RunOnceAsync(CancellationToken.None));
        Assert.InRange(resolver.MaximumConcurrency, 2, 4);
    }

    [Fact]
    public async Task Cleaning_pipeline_loads_id_batches_without_reprocessing_completed_rows()
    {
        var store = new InMemoryHotelMatchingStore();
        for (var index = 1; index <= 3; index++)
        {
            store.AddRaw(CreateRaw(HotelDataType.Platform, $"MT-{index}", index));
            store.AddRaw(CreateRaw(HotelDataType.Supplier, $"SUP-{index}", index));
        }

        var service = new HotelCleaningService(
            store,
            new HotelCleaner(new FixedMissingRegionResolver()),
            Options.Create(new HotelCleaningOptions
            {
                BatchSize = 2,
                SaveBatchSize = 2,
                QueueCapacity = 4,
                QueueRefillThreshold = 1,
                QueueRefillDelaySeconds = 0
            }),
            new NoOpStarLevelIssueRecorder(),
            NullLogger<HotelCleaningService>.Instance);

        Assert.Equal(6, await service.RunOnceAsync(CancellationToken.None));
        Assert.Equal(0, await service.RunOnceAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public async Task Cleaning_pipeline_reprocesses_rows_when_version_changes(
        int nextCleanVersion,
        int nextRegionBoundaryVersion)
    {
        var store = new InMemoryHotelMatchingStore();
        store.AddRaw(CreateRaw(HotelDataType.Platform, "MT-1", 1));

        var firstRun = CreateService(store, cleanVersion: 1, regionBoundaryVersion: 1);
        Assert.Equal(1, await firstRun.RunOnceAsync(CancellationToken.None));
        Assert.Equal(0, await firstRun.RunOnceAsync(CancellationToken.None));

        var versionChanged = CreateService(
            store,
            nextCleanVersion,
            nextRegionBoundaryVersion);
        Assert.Equal(1, await versionChanged.RunOnceAsync(CancellationToken.None));
        Assert.Equal(0, await versionChanged.RunOnceAsync(CancellationToken.None));
    }

    static HotelCleaningService CreateService(
        InMemoryHotelMatchingStore store,
        int cleanVersion,
        int regionBoundaryVersion)
        => new(
            store,
            new HotelCleaner(new FixedMissingRegionResolver(regionBoundaryVersion)),
            Options.Create(new HotelCleaningOptions
            {
                Version = cleanVersion,
                RegionFenceVersion = regionBoundaryVersion,
                BatchSize = 2,
                SaveBatchSize = 2,
                QueueCapacity = 4,
                QueueRefillThreshold = 1,
                QueueRefillDelaySeconds = 0
            }),
            new NoOpStarLevelIssueRecorder(),
            NullLogger<HotelCleaningService>.Instance);

    static RawHotelData CreateRaw(HotelDataType type, string hotelCode, int id)
        => new(type, hotelCode)
        {
            Id = id,
            PlatformHotelId = type == HotelDataType.Platform ? id : null,
            SupplierId = type == HotelDataType.Supplier ? 51 : null,
            SourceSystemId = type == HotelDataType.Supplier ? id : null,
            HotelName = hotelCode,
            GpsType = (int)HotelCoordinateType.Wgs84,
            Longitude = 121.47m,
            Latitude = 31.23m,
            SourceDataVersion = 1
        };

    sealed class NoOpStarLevelIssueRecorder : IHotelStarLevelIssueRecorder
    {
        public void Report(HotelDataType dataType, int? supplierId, int? sourceId, string? rawValue, string reason)
        {
        }

        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    sealed class BatchTrackingCleaningRepository(InMemoryHotelMatchingStore inner) : IHotelCleaningRepository
    {
        public List<int> SavedBatchSizes { get; } = [];

        public Task<HotelCleaningWorkSet> LoadCleaningWorkSetAsync(
            HotelDataType type,
            DateTimeOffset changedSince,
            int cleanVersion,
            int regionBoundaryVersion,
            int pendingIdBatchSize,
            Action<HotelCleaningScanProgress> reportScanProgress,
            Func<int, CancellationToken, ValueTask> enqueuePendingId,
            CancellationToken cancellationToken)
            => inner.LoadCleaningWorkSetAsync(
                type,
                changedSince,
                cleanVersion,
                regionBoundaryVersion,
                pendingIdBatchSize,
                reportScanProgress,
                enqueuePendingId,
                cancellationToken);

        public Task<IReadOnlyList<RawHotelData>> GetHotelsByIdsAsync(
            HotelDataType type,
            IReadOnlyCollection<int> dataIds,
            CancellationToken cancellationToken)
            => inner.GetHotelsByIdsAsync(type, dataIds, cancellationToken);

        public Task<int> SaveCleanedHotelsAsync(
            IReadOnlyCollection<HotelCleaningResult> results,
            CancellationToken cancellationToken)
        {
            SavedBatchSizes.Add(results.Count);
            return inner.SaveCleanedHotelsAsync(results, cancellationToken);
        }

        public Task RecordCleaningFailuresAsync(
            IReadOnlyCollection<HotelCleaningFailureRecord> failures,
            CancellationToken cancellationToken)
            => inner.RecordCleaningFailuresAsync(failures, cancellationToken);
    }

    sealed class FixedMissingRegionResolver(int boundaryVersion = 1) : IHotelRegionResolver
    {
        public RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default)
            => RegionResolution.NotFound(boundaryVersion);
    }

    sealed class ConcurrencyTrackingRegionResolver : IHotelRegionResolver
    {
        int _active;
        int _maximum;

        public int MaximumConcurrency => Volatile.Read(ref _maximum);

        public RegionResolution Resolve(
            decimal longitude,
            decimal latitude,
            CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _active);
            var maximum = Volatile.Read(ref _maximum);
            while (active > maximum)
            {
                var observed = Interlocked.CompareExchange(ref _maximum, active, maximum);
                if (observed == maximum)
                {
                    break;
                }

                maximum = observed;
            }

            try
            {
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(30));
                cancellationToken.ThrowIfCancellationRequested();
                return RegionResolution.NotFound();
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
