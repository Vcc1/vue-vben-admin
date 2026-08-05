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
        var store = new FakeRedisStore(new Dictionary<string, string>
        {
            ["51_B"] = "MT-B-OLD",
            ["51_C"] = "MT-C"
        });
        var queue = new FakeTriggerQueue();
        var runner = new HotelMappingSyncRunner(source, store, queue);

        var result = await runner.RunAsync(null, CancellationToken.None);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(3, result.QueuedCount);
        Assert.Equal(0, result.PendingCount);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["51_A"] = "MT-A",
                ["51_B"] = "MT-B-NEW"
            },
            store.Mappings);
        Assert.Equal(["MT-A", "MT-B-NEW", "MT-C"], queue.Messages);
    }

    [Fact]
    public async Task RunAsync_KeepsFailedDeliveryInOutboxAndReplaysItNextRun()
    {
        var source = new FakeSourceReader(
        [new HotelMappingSourceRow(51, "A", "MT-A")]);
        var store = new FakeRedisStore(new Dictionary<string, string>());
        var failingQueue = new FakeTriggerQueue { FailNext = true };
        var firstRunner = new HotelMappingSyncRunner(source, store, failingQueue);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => firstRunner.RunAsync(null, CancellationToken.None));

        Assert.Equal("MT-A", store.Mappings["51_A"]);
        Assert.Single(store.Outbox);

        var recoveredQueue = new FakeTriggerQueue();
        var secondRunner = new HotelMappingSyncRunner(source, store, recoveredQueue);
        var result = await secondRunner.RunAsync(null, CancellationToken.None);

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(1, result.QueuedCount);
        Assert.Equal(0, result.PendingCount);
        Assert.Empty(store.Outbox);
        Assert.Equal(["MT-A"], recoveredQueue.Messages);
    }

    [Fact]
    public async Task RunAsync_DoesNotEnqueueUnchangedMappings()
    {
        var source = new FakeSourceReader(
        [new HotelMappingSourceRow(51, "A", "MT-A")]);
        var store = new FakeRedisStore(new Dictionary<string, string>
        {
            ["51_A"] = "MT-A"
        });
        var queue = new FakeTriggerQueue();
        var runner = new HotelMappingSyncRunner(source, store, queue);

        var result = await runner.RunAsync(null, CancellationToken.None);

        Assert.Equal(0, result.AddedCount + result.UpdatedCount + result.DeletedCount);
        Assert.Equal(0, result.QueuedCount);
        Assert.Empty(queue.Messages);
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
        public FakeRedisStore(Dictionary<string, string> mappings)
        {
            Mappings = new Dictionary<string, string>(mappings, StringComparer.Ordinal);
        }

        public Dictionary<string, string> Mappings { get; }
        public Dictionary<string, HotelMappingOutboxEntry> Outbox { get; } = [];

        public Task<IReadOnlyDictionary<string, string>> ReadMappingsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(Mappings, StringComparer.Ordinal));

        public Task ApplyAsync(
            IReadOnlyList<HotelMappingSyncOperation> operations,
            CancellationToken cancellationToken)
        {
            foreach (var operation in operations)
            {
                if (operation.Type == HotelMappingSyncOperationType.Delete)
                {
                    Mappings.Remove(operation.Field);
                }
                else
                {
                    Mappings[operation.Field] = operation.NewValue!;
                }

                var id = Guid.NewGuid().ToString("N");
                Outbox[id] = new HotelMappingOutboxEntry(
                    id,
                    operation.Type,
                    operation.Field,
                    operation.OldValue,
                    operation.NewValue,
                    operation.QueueValue,
                    DateTimeOffset.Now);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<HotelMappingOutboxEntry>> ReadOutboxAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<HotelMappingOutboxEntry>>(
                Outbox.Values.OrderBy(entry => entry.CreatedAt).ToArray());

        public Task AcknowledgeOutboxAsync(
            string id,
            CancellationToken cancellationToken)
        {
            Outbox.Remove(id);
            return Task.CompletedTask;
        }
    }

    sealed class FakeTriggerQueue : IHotelMappingQueueWriter
    {
        public List<string> Messages { get; } = [];
        public bool FailNext { get; init; }

        public Task EnqueueAsync(
            string mtHotelCode,
            CancellationToken cancellationToken)
        {
            if (FailNext && Messages.Count == 0)
            {
                throw new InvalidOperationException("Queue unavailable.");
            }

            Messages.Add(mtHotelCode);
            return Task.CompletedTask;
        }
    }
}
