using HotelMatchingPipeline.App;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class HotelMatchVetoServiceTests
{
    [Fact]
    public async Task Returns_only_after_the_persisted_veto_is_redis_synced()
    {
        var syncedAt = DateTimeOffset.Now;
        var repository = new FakeVetoRepository
        {
            CreateResult = new HotelMatchVetoCreateResult(HotelMatchVetoCreateStatus.Created, 7),
            Item = Item(redisSyncedAt: syncedAt)
        };
        var service = new HotelMatchVetoService(repository, new HotelMatchVetoSyncSignal());

        var result = await service.VetoAsync(Request(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("vetoed", result.Status);
        Assert.Equal(syncedAt, result.RedisSyncedAt);
        Assert.Equal(1, repository.GetCount);
    }

    [Theory]
    [InlineData(HotelMatchVetoCreateStatus.SupplierNotFound, "supplierNotFound")]
    [InlineData(HotelMatchVetoCreateStatus.MatchNotFound, "matchNotFound")]
    [InlineData(HotelMatchVetoCreateStatus.CurrentMatchChanged, "currentMatchChanged")]
    public async Task Maps_creation_failures_without_waiting_for_redis(
        HotelMatchVetoCreateStatus createStatus,
        string expectedStatus)
    {
        var repository = new FakeVetoRepository
        {
            CreateResult = new HotelMatchVetoCreateResult(createStatus, null, "MT-NEW")
        };
        var service = new HotelMatchVetoService(repository, new HotelMatchVetoSyncSignal());

        var result = await service.VetoAsync(Request(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("MT-NEW", result.CurrentMTHotelCode);
        Assert.Equal(0, repository.GetCount);
    }

    [Fact]
    public async Task Rejects_missing_audit_fields_before_persistence()
    {
        var repository = new FakeVetoRepository();
        var service = new HotelMatchVetoService(repository, new HotelMatchVetoSyncSignal());

        await Assert.ThrowsAsync<ArgumentException>(() => service.VetoAsync(
            Request() with { Operator = " " },
            CancellationToken.None));

        Assert.Equal(0, repository.CreateCount);
    }

    static HotelMatchVetoRequest Request()
        => new(51, "SUP-1", "MT-1", "operator", "incorrect match");

    static HotelMatchVetoSyncItem Item(DateTimeOffset? redisSyncedAt)
    {
        var now = DateTimeOffset.Now;
        return new HotelMatchVetoSyncItem(
            7,
            1,
            51,
            "SUP-1",
            "MT-1",
            "operator",
            "incorrect match",
            redisSyncedAt.HasValue ? 1 : 0,
            now,
            now,
            null,
            redisSyncedAt,
            null,
            null,
            redisSyncedAt,
            null);
    }

    sealed class FakeVetoRepository : IHotelMatchVetoRepository
    {
        public HotelMatchVetoCreateResult CreateResult { get; init; } =
            new(HotelMatchVetoCreateStatus.Created, 7);
        public HotelMatchVetoSyncItem? Item { get; init; }
        public int CreateCount { get; private set; }
        public int GetCount { get; private set; }

        public Task<HotelMatchVetoCreateResult> CreateAsync(
            HotelMatchVetoRequest request,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            return Task.FromResult(CreateResult);
        }

        public Task<HotelMatchVetoSyncItem?> GetAsync(long vetoId, CancellationToken cancellationToken)
        {
            GetCount++;
            return Task.FromResult(Item);
        }

        public Task<IReadOnlyList<HotelMatchVetoSyncItem>> LoadPendingSyncAsync(int batchSize, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<HotelMatchVetoSyncItem> RecordRedisFailureAsync(long vetoId, string error, DateTimeOffset failedAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task MarkFailureAlertSentAsync(long vetoId, DateTimeOffset sentAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<HotelMatchVetoSyncItem> MarkRedisSyncedAsync(long vetoId, DateTimeOffset syncedAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task MarkRecoveryAlertSentAsync(long vetoId, DateTimeOffset sentAt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> IsVetoedAsync(int supId, string supplierHotelCode, string mtHotelCode, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
