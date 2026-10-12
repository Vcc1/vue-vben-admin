using Infrastructure.Redis.HotelMatchingRedis;
using Tests.Host.HotelMappingSync;

namespace Tests.Host.Tests;

public sealed class HotelMappingSyncRunnerTests
{
    [Fact]
    public async Task RunAsync_AppliesFullDiffAndEnqueuesOneMessagePerChange()
    {
        var source = new FakeSourceReader(
        [
            new HotelMappingSourceRow(51, "A", "MT-A"),
            new HotelMappingSourceRow(51, "B", "MT-B-NEW")
        ]);
        var store = new FakeRedisStore(new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>
        {
            ["MT-B-NEW"] = [Hotel(51, "B-OLD")],
            ["MT-C"] = [Hotel(51, "C")]
        });
        var runner = new HotelMappingSyncRunner(source, store);

        var result = await runner.RunAsync(null, CancellationToken.None);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(3, result.QueuedCount);
        Assert.Equal(3, result.PendingCount);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["MT-A"] = "A",
                ["MT-B-NEW"] = "B"
            },
            store.Mappings.ToDictionary(
                pair => pair.Key,
                pair => Assert.Single(pair.Value).SHotelCode));
        Assert.Equal(["MT-C", "MT-B-NEW", "MT-A"], store.PendingQueue);
        Assert.Equal("MT-A", store.RightPop());
        Assert.Equal("MT-B-NEW", store.RightPop());
        Assert.Equal("MT-C", store.RightPop());
    }

    [Fact]
    public async Task RunAsync_PreservesPendingListWithoutDuplicatingUnchangedMappings()
    {
        var source = new FakeSourceReader(
        [new HotelMappingSourceRow(51, "A", "MT-A")]);
        var store = new FakeRedisStore(
            new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>());
        var firstRunner = new HotelMappingSyncRunner(source, store);

        var firstResult = await firstRunner.RunAsync(null, CancellationToken.None);

        Assert.Equal("A", Assert.Single(store.Mappings["MT-A"]).SHotelCode);
        Assert.Equal(["MT-A"], store.PendingQueue);
        Assert.Equal(1, firstResult.QueuedCount);
        Assert.Equal(1, firstResult.PendingCount);

        var secondRunner = new HotelMappingSyncRunner(source, store);
        var result = await secondRunner.RunAsync(null, CancellationToken.None);

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.QueuedCount);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(["MT-A"], store.PendingQueue);
    }

    [Fact]
    public async Task RunAsync_DoesNotEnqueueUnchangedMappings()
    {
        var source = new FakeSourceReader(
        [new HotelMappingSourceRow(51, "A", "MT-A")]);
        var store = new FakeRedisStore(new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>
        {
            ["MT-A"] = [Hotel(51, "A")]
        });
        var runner = new HotelMappingSyncRunner(source, store);

        var result = await runner.RunAsync(null, CancellationToken.None);

        Assert.Equal(0, result.AddedCount + result.UpdatedCount + result.DeletedCount);
        Assert.Equal(0, result.QueuedCount);
        Assert.Empty(store.PendingQueue);
    }

    sealed class FakeSourceReader(IReadOnlyList<HotelMappingSourceRow> rows)
        : IHotelMappingSourceReader
    {
        public Task<IReadOnlyList<HotelMappingSourceRow>> ReadAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(rows);
    }

    sealed class FakeRedisStore : IHotelMappingRedisStore
    {
        public FakeRedisStore(
            Dictionary<string, IReadOnlyList<SupHotelInfoDto>> mappings)
        {
            Mappings = mappings.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<SupHotelInfoDto>)pair.Value.ToArray(),
                StringComparer.Ordinal);
        }

        public Dictionary<string, IReadOnlyList<SupHotelInfoDto>> Mappings { get; }
        public List<string> PendingQueue { get; } = [];

        public HotelMappingRedisConnectionInfo GetConnectionInfo()
            => new(0, []);

        public Task<TimeSpan> PingAsync(CancellationToken cancellationToken)
            => Task.FromResult(TimeSpan.Zero);

        public Task<int> GetPendingQueueLengthAsync(CancellationToken cancellationToken)
            => Task.FromResult(PendingQueue.Count);

        public Task<IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>>> ReadMappingsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>>>(
                Mappings.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<SupHotelInfoDto>)pair.Value.ToArray(),
                    StringComparer.Ordinal));

        public Task ApplyAsync(
            IReadOnlyList<HotelMappingSyncOperation> operations,
            CancellationToken cancellationToken)
        {
            foreach (var operation in operations)
            {
                if (operation.Type == HotelMappingSyncOperationType.Delete)
                {
                    Mappings.Remove(operation.MTHotelCode);
                }
                else
                {
                    Mappings[operation.MTHotelCode] =
                        operation.NewSupplierHotels!.ToArray();
                }

                PendingQueue.Insert(0, operation.MTHotelCode);
            }

            return Task.CompletedTask;
        }

        public Task RemoveSupplierHotelAsync(
            string mtHotelCode,
            int supId,
            string supplierHotelCode,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public string? RightPop()
        {
            if (PendingQueue.Count == 0)
            {
                return null;
            }

            var index = PendingQueue.Count - 1;
            var value = PendingQueue[index];
            PendingQueue.RemoveAt(index);
            return value;
        }
    }

    static SupHotelInfoDto Hotel(int supId, string supplierHotelCode)
        => new()
        {
            SupId = supId,
            SHotelCode = supplierHotelCode
        };
}
