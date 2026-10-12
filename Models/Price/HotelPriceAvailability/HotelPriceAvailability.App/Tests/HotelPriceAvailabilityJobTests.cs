using HotelPriceAvailability.App.Configuration;
using HotelPriceAvailability.App.Contracts;
using HotelPriceAvailability.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelPriceAvailability.App.Tests;

public sealed class HotelPriceAvailabilityJobTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessesMappedAndSupplierHotelsSeparately()
    {
        var repository = new FakeRepository(
        [
            new PriceCheckTask(1, 7, "MT-1", null, null, null, null),
            new PriceCheckTask(2, 7, null, "SUP-1", "flag", "CN", "SHA")
        ]);
        var gateway = new FakeGateway(static batch => PriceQueryOutcome.Success(batch.Tasks.Select(static task => task.Id).ToArray()));
        using var metrics = new HotelPriceAvailabilityMetrics();
        var job = CreateJob(repository, gateway, new HotelPriceAvailabilityOptions
        {
            BatchNo = "batch-1",
            BatchSize = 1,
            MaxConcurrency = 2,
            Persons = [1],
            DateWindows = [new DateWindowOptions { StayDays = 1, FromDay = 1, ToDay = 1 }]
        }, metrics);

        var summary = await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, summary.PricedTasks);
        Assert.Equal(2, repository.MarkedHits.Count);
        Assert.Contains(PriceLookupMode.MappedHotel, gateway.Modes);
        Assert.Contains(PriceLookupMode.SupplierHotel, gateway.Modes);
    }

    [Fact]
    public async Task ExecuteAsync_SelectsEarlierHitWhenLaterRequestCompletesFirst()
    {
        var repository = new FakeRepository(
        [new PriceCheckTask(1, 7, "MT-1", null, null, null, null)]);
        var gateway = new FakeGateway(async batch =>
        {
            if (batch.Combination.Priority == 0)
            {
                await Task.Delay(30);
            }

            return PriceQueryOutcome.Success([1]);
        });
        using var metrics = new HotelPriceAvailabilityMetrics();
        var job = CreateJob(repository, gateway, new HotelPriceAvailabilityOptions
        {
            BatchNo = "batch-1",
            BatchSize = 1,
            MaxConcurrency = 2,
            Persons = [1],
            DateWindows = [new DateWindowOptions { StayDays = 1, FromDay = 1, ToDay = 2 }]
        }, metrics);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Single(repository.MarkedHits);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddDays(1)), repository.MarkedHits[0].Combination.Window.CheckIn);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsRequestFailureWithoutMarkingUnpricedTask()
    {
        var repository = new FakeRepository(
        [new PriceCheckTask(1, 7, "MT-1", null, null, null, null)]);
        var gateway = new FakeGateway(_ => Task.FromResult(PriceQueryOutcome.Failure("timeout")));
        using var metrics = new HotelPriceAvailabilityMetrics();
        var job = CreateJob(repository, gateway, new HotelPriceAvailabilityOptions
        {
            BatchNo = "batch-1",
            BatchSize = 1,
            MaxConcurrency = 1,
            Persons = [1],
            DateWindows = [new DateWindowOptions { StayDays = 1, FromDay = 1, ToDay = 1 }]
        }, metrics);

        var summary = await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, summary.FailedRequests);
        Assert.Equal(1, summary.UnpricedTasks);
        Assert.Empty(repository.MarkedHits);
    }

    [Fact]
    public async Task ExecuteAsync_StopsWhenCancellationIsRequested()
    {
        var repository = new FakeRepository(
        [new PriceCheckTask(1, 7, "MT-1", null, null, null, null)]);
        var gateway = new BlockingGateway();
        using var metrics = new HotelPriceAvailabilityMetrics();
        var job = CreateJob(repository, gateway, new HotelPriceAvailabilityOptions
        {
            BatchNo = "batch-1",
            BatchSize = 1,
            MaxConcurrency = 1,
            Persons = [1],
            DateWindows = [new DateWindowOptions { StayDays = 1, FromDay = 1, ToDay = 1 }]
        }, metrics);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.ExecuteAsync(cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_RespectsConfiguredConcurrency()
    {
        var repository = new FakeRepository(Enumerable.Range(1, 8)
            .Select(id => new PriceCheckTask(id, 7, $"MT-{id}", null, null, null, null))
            .ToArray());
        var gateway = new FakeGateway(async _ =>
        {
            await Task.Delay(20);
            return PriceQueryOutcome.Success();
        });
        using var metrics = new HotelPriceAvailabilityMetrics();
        var job = CreateJob(repository, gateway, new HotelPriceAvailabilityOptions
        {
            BatchNo = "batch-1",
            BatchSize = 1,
            MaxConcurrency = 2,
            Persons = [1],
            DateWindows = [new DateWindowOptions { StayDays = 1, FromDay = 1, ToDay = 1 }]
        }, metrics);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.True(gateway.MaxConcurrency <= 2);
    }

    [Fact]
    public void OptionsValidator_RejectsMissingBatchAndWindows()
    {
        var result = new HotelPriceAvailabilityOptionsValidator().Validate(
            null,
            new HotelPriceAvailabilityOptions());

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures!, failure => failure.Contains("BatchNo", StringComparison.Ordinal));
    }

    private static HotelPriceAvailabilityJob CreateJob(
        FakeRepository repository,
        IPriceQueryGateway gateway,
        HotelPriceAvailabilityOptions options,
        HotelPriceAvailabilityMetrics metrics) => new(
            repository,
            gateway,
            Options.Create(options),
            metrics,
            TimeProvider.System,
            NullLogger<HotelPriceAvailabilityJob>.Instance);

    private sealed class FakeRepository : IPriceAvailabilityTaskRepository
    {
        private readonly IReadOnlyList<PriceCheckTask> _tasks;
        public List<PriceHit> MarkedHits { get; } = [];

        public FakeRepository(IReadOnlyList<PriceCheckTask> tasks) => _tasks = tasks;

        public Task<IReadOnlyList<PriceCheckTask>> LoadPendingAsync(string batchNo, CancellationToken cancellationToken) =>
            Task.FromResult(_tasks);

        public Task MarkPricedAsync(IReadOnlyList<PriceHit> hits, CancellationToken cancellationToken)
        {
            MarkedHits.AddRange(hits);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGateway : IPriceQueryGateway
    {
        private readonly Func<PriceQueryBatch, Task<PriceQueryOutcome>> _handler;
        private int _active;
        public int MaxConcurrency { get; private set; }
        public List<PriceLookupMode> Modes { get; } = [];

        public FakeGateway(Func<PriceQueryBatch, PriceQueryOutcome> handler)
            : this(batch => Task.FromResult(handler(batch)))
        {
        }

        public FakeGateway(Func<PriceQueryBatch, Task<PriceQueryOutcome>> handler) => _handler = handler;

        public async Task<PriceQueryOutcome> QueryAsync(PriceQueryBatch batch, CancellationToken cancellationToken)
        {
            Modes.Add(batch.LookupMode);
            var active = Interlocked.Increment(ref _active);
            MaxConcurrency = Math.Max(MaxConcurrency, active);
            try
            {
                return await _handler(batch);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class BlockingGateway : IPriceQueryGateway
    {
        public async Task<PriceQueryOutcome> QueryAsync(PriceQueryBatch batch, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return PriceQueryOutcome.Success();
        }
    }
}
