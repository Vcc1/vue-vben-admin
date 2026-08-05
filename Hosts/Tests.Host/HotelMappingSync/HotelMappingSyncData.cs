using System.Text.Encodings.Web;
using System.Text.Json;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace Tests.Host.HotelMappingSync;

public interface IHotelMappingSourceReader
{
    Task<IReadOnlyList<HotelMappingSourceRow>> ReadAsync(CancellationToken cancellationToken);
}

public sealed class HotelMappingSourceReader(RoomMappingDbContext dbContext) : IHotelMappingSourceReader
{
    public async Task<IReadOnlyList<HotelMappingSourceRow>> ReadAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.HotelMatchResults
            .AsNoTracking()
            .Where(row => row.MTHotelCode != null && row.MTHotelCode != string.Empty)
            .Select(row => new HotelMappingSourceRow(
                row.SupplierHotel.SupId,
                row.SupplierHotel.SHotelCode,
                row.MTHotelCode))
            .ToArrayAsync(cancellationToken);
    }
}

public interface IHotelMappingRedisStore
{
    Task<IReadOnlyDictionary<string, string>> ReadMappingsAsync(
        CancellationToken cancellationToken);

    Task ApplyAsync(
        IReadOnlyList<HotelMappingSyncOperation> operations,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HotelMappingOutboxEntry>> ReadOutboxAsync(
        CancellationToken cancellationToken);

    Task AcknowledgeOutboxAsync(string id, CancellationToken cancellationToken);
}

public interface IHotelMappingQueueWriter
{
    Task EnqueueAsync(string mtHotelCode, CancellationToken cancellationToken);
}

public sealed class HotelMappingQueueWriter : RedisContextBase, IHotelMappingQueueWriter
{
    readonly string _queueKey;

    public HotelMappingQueueWriter(IConfiguration configuration)
        : base(configuration, "BaseDataTriggerQueue")
    {
        var configuredQueueKey = configuration["TriggerQueue:RoomBuildHotelKey"]?.Trim();
        _queueKey = string.IsNullOrWhiteSpace(configuredQueueKey)
            ? "OTARoomAddQueue"
            : configuredQueueKey;
    }

    public async Task EnqueueAsync(
        string mtHotelCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DB.ListLeftPushAsync(_queueKey, mtHotelCode);
    }
}

public sealed class HotelMappingRedisStore : RedisContextBase, IHotelMappingRedisStore
{
    public const string MappingHashKey = "HotelMatching:Mapping";
    public const string OutboxHashKey = "HotelMatching:MappingSyncOutbox";

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public HotelMappingRedisStore(IConfiguration configuration)
        : base(configuration, "RoomMappingRedis")
    {
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadMappingsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await DB.HashGetAllAsync(MappingHashKey);
        return entries.ToDictionary(
            entry => entry.Name.ToString(),
            entry => entry.Value.ToString(),
            StringComparer.Ordinal);
    }

    public async Task ApplyAsync(
        IReadOnlyList<HotelMappingSyncOperation> operations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (operations.Count == 0)
        {
            return;
        }

        var transaction = DB.CreateTransaction();
        var tasks = new List<Task>(operations.Count * 2);
        foreach (var operation in operations)
        {
            tasks.Add(operation.Type == HotelMappingSyncOperationType.Delete
                ? transaction.HashDeleteAsync(MappingHashKey, operation.Field)
                : transaction.HashSetAsync(MappingHashKey, operation.Field, operation.NewValue));

            var outbox = new HotelMappingOutboxEntry(
                Guid.NewGuid().ToString("N"),
                operation.Type,
                operation.Field,
                operation.OldValue,
                operation.NewValue,
                operation.QueueValue,
                DateTimeOffset.Now);
            tasks.Add(transaction.HashSetAsync(
                OutboxHashKey,
                outbox.Id,
                JsonSerializer.Serialize(outbox, JsonOptions)));
        }

        if (!await transaction.ExecuteAsync())
        {
            throw new RedisException("Hotel mapping Redis synchronization transaction was not committed.");
        }

        await Task.WhenAll(tasks);
    }

    public async Task<IReadOnlyList<HotelMappingOutboxEntry>> ReadOutboxAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await DB.HashGetAllAsync(OutboxHashKey);
        return entries
            .Select(entry => JsonSerializer.Deserialize<HotelMappingOutboxEntry>(
                entry.Value.ToString(),
                JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Unable to deserialize hotel mapping outbox entry {entry.Name}."))
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task AcknowledgeOutboxAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DB.HashDeleteAsync(OutboxHashKey, id);
    }
}
