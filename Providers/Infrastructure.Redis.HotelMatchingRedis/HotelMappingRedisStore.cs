using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace Infrastructure.Redis.HotelMatchingRedis;

public static class HotelMatchingRedisKeys
{
    public const string Mapping = "HotelMatching:Mapping";
    public const string Conflict = "HotelMatching:Conflict";
    public const string MappingSyncPending = "HotelMatching:MappingSyncPending";
}

public enum HotelMappingSyncOperationType
{
    Add,
    Update,
    Delete
}

public sealed record SupHotelInfoDto
{
    [JsonPropertyName("M")]
    public string MTHotelCode { get; init; } = string.Empty;

    [JsonPropertyName("I")]
    public int SupId { get; init; }

    [JsonPropertyName("C1")]
    public string SContryCode { get; init; } = string.Empty;

    [JsonPropertyName("C2")]
    public string SCityCode { get; init; } = string.Empty;

    [JsonPropertyName("C3")]
    public string SHotelCode { get; init; } = string.Empty;

    [JsonPropertyName("N")]
    public string SHotelName { get; init; } = string.Empty;

    [JsonPropertyName("LV")]
    public int LV { get; init; } = 1;
}

public sealed record HotelMappingSyncOperation(
    HotelMappingSyncOperationType Type,
    string MTHotelCode,
    IReadOnlyList<SupHotelInfoDto>? OldSupplierHotels,
    IReadOnlyList<SupHotelInfoDto>? NewSupplierHotels)
{
    public string QueueValue => MTHotelCode;
}

public interface IHotelMappingRedisStore
{
    HotelMappingRedisConnectionInfo GetConnectionInfo();

    Task<TimeSpan> PingAsync(CancellationToken cancellationToken);

    Task<int> GetPendingQueueLengthAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>>> ReadMappingsAsync(
        CancellationToken cancellationToken);

    Task ApplyAsync(
        IReadOnlyList<HotelMappingSyncOperation> operations,
        CancellationToken cancellationToken);

}

public sealed record HotelMappingRedisConnectionInfo(
    int Database,
    IReadOnlyList<string> Endpoints);

public sealed class HotelMappingRedisStore : RedisContextBase, IHotelMappingRedisStore
{
    const int HashScanPageSize = 1000;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public HotelMappingRedisStore(IConfiguration configuration)
        : base(configuration, "HotelMappingRedis")
    {
    }

    public Task<TimeSpan> PingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return DB.PingAsync();
    }

    public HotelMappingRedisConnectionInfo GetConnectionInfo()
    {
        return new HotelMappingRedisConnectionInfo(
            DB.Database,
            RedisMultiplexer.GetEndPoints()
                .Select(endpoint => endpoint.ToString() ?? string.Empty)
                .Where(endpoint => endpoint.Length > 0)
                .OrderBy(endpoint => endpoint, StringComparer.Ordinal)
                .ToArray());
    }

    public async Task<int> GetPendingQueueLengthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var length = await DB.ListLengthAsync(HotelMatchingRedisKeys.MappingSyncPending);
        return checked((int)length);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>>> ReadMappingsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mappings = new Dictionary<string, IReadOnlyList<SupHotelInfoDto>>(StringComparer.Ordinal);
        await foreach (var entry in DB.HashScanAsync(
            HotelMatchingRedisKeys.Mapping,
            pageSize: HashScanPageSize).WithCancellation(cancellationToken))
        {
            var field = entry.Name.ToString();
            var value = entry.Value.ToString().Trim();
            try
            {
                mappings[field] = DeserializeSupplierHotels(field, value);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Redis mapping field '{field}' is not a supplier hotel list.",
                    ex);
            }
        }

        return mappings;
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
                ? transaction.HashDeleteAsync(
                    HotelMatchingRedisKeys.Mapping,
                    operation.MTHotelCode)
                : transaction.HashSetAsync(
                    HotelMatchingRedisKeys.Mapping,
                    operation.MTHotelCode,
                    JsonSerializer.Serialize(
                        NormalizeSupplierHotels(operation.NewSupplierHotels ?? [], operation.MTHotelCode),
                        JsonOptions)));

            tasks.Add(transaction.ListLeftPushAsync(
                HotelMatchingRedisKeys.MappingSyncPending,
                operation.MTHotelCode));
        }

        if (!await transaction.ExecuteAsync())
        {
            throw new RedisException("Hotel mapping Redis synchronization transaction was not committed.");
        }

        await Task.WhenAll(tasks);
    }

    static IReadOnlyList<SupHotelInfoDto> DeserializeSupplierHotels(string mtHotelCode, string json)
    {
        var hotels = JsonSerializer.Deserialize<SupHotelInfoDto[]>(json, JsonOptions) ?? [];
        return NormalizeSupplierHotels(hotels, mtHotelCode);
    }

    internal static SupHotelInfoDto[] NormalizeSupplierHotels(
        IEnumerable<SupHotelInfoDto> values,
        string mtHotelCode)
    {
        return values
            .Select(value => value with
            {
                MTHotelCode = mtHotelCode.Trim(),
                SContryCode = value.SContryCode.Trim(),
                SCityCode = value.SCityCode.Trim(),
                SHotelCode = value.SHotelCode.Trim(),
                SHotelName = value.SHotelName.Trim()
            })
            .Where(value => value.SHotelCode.Length > 0)
            .GroupBy(value => $"{value.SupId}_{value.SHotelCode}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.SupId)
            .ThenBy(value => value.SHotelCode, StringComparer.Ordinal)
            .ToArray();
    }

}
