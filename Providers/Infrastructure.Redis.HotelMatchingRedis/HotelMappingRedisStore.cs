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
    // Used only to migrate values written by the previous payload shape.
    [JsonIgnore]
    public bool HasLegacyM { get; init; }

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

    Task RemoveSupplierHotelAsync(
        string mtHotelCode,
        int supId,
        string supplierHotelCode,
        CancellationToken cancellationToken);
}

public sealed record HotelMappingRedisConnectionInfo(
    int Database,
    IReadOnlyList<string> Endpoints);

public sealed class HotelMappingRedisStore :
    RedisContextBase,
    IHotelMappingRedisStore
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
                mappings[field] = DeserializeSupplierHotels(value);
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
                        NormalizeSupplierHotels(operation.NewSupplierHotels ?? []),
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

    public async Task RemoveSupplierHotelAsync(
        string mtHotelCode,
        int supId,
        string supplierHotelCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mtHotelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierHotelCode);
        var normalizedMtHotelCode = mtHotelCode.Trim();
        var normalizedSupplierHotelCode = supplierHotelCode.Trim();
        var conflictField = $"{supId}_{normalizedSupplierHotelCode}";

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentValue = await DB.HashGetAsync(
                HotelMatchingRedisKeys.Mapping,
                normalizedMtHotelCode);
            var remaining = currentValue.IsNull
                ? []
                : RemoveSupplierHotel(
                    DeserializeSupplierHotels(currentValue.ToString()),
                    supId,
                    normalizedSupplierHotelCode);

            var transaction = DB.CreateTransaction();
            transaction.AddCondition(currentValue.IsNull
                ? Condition.HashNotExists(
                    HotelMatchingRedisKeys.Mapping,
                    normalizedMtHotelCode)
                : Condition.HashEqual(
                    HotelMatchingRedisKeys.Mapping,
                    normalizedMtHotelCode,
                    currentValue));
            var tasks = new List<Task>(3)
            {
                remaining.Length == 0
                    ? transaction.HashDeleteAsync(
                        HotelMatchingRedisKeys.Mapping,
                        normalizedMtHotelCode)
                    : transaction.HashSetAsync(
                        HotelMatchingRedisKeys.Mapping,
                        normalizedMtHotelCode,
                        JsonSerializer.Serialize(
                            NormalizeSupplierHotels(remaining),
                            JsonOptions)),
                transaction.HashDeleteAsync(HotelMatchingRedisKeys.Conflict, conflictField),
                transaction.ListLeftPushAsync(
                    HotelMatchingRedisKeys.MappingSyncPending,
                    normalizedMtHotelCode)
            };

            if (await transaction.ExecuteAsync())
            {
                await Task.WhenAll(tasks);
                return;
            }

            await Task.Yield();
        }
    }

    static IReadOnlyList<SupHotelInfoDto> DeserializeSupplierHotels(string json)
    {
        var hotels = JsonSerializer.Deserialize<SupHotelInfoDto[]>(json, JsonOptions) ?? [];
        var hasLegacyM = false;
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            hasLegacyM = document.RootElement.EnumerateArray()
                .Any(item => item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("M", out _));
        }

        return NormalizeSupplierHotels(hotels, hasLegacyM);
    }

    internal static SupHotelInfoDto[] NormalizeSupplierHotels(
        IEnumerable<SupHotelInfoDto> values,
        bool hasLegacyM = false)
    {
        return values
            .Select(value => value with
            {
                SContryCode = value.SContryCode.Trim(),
                SCityCode = value.SCityCode.Trim(),
                SHotelCode = value.SHotelCode.Trim(),
                SHotelName = value.SHotelName.Trim(),
                LV = 1,
                HasLegacyM = hasLegacyM
            })
            .Where(value => value.SHotelCode.Length > 0)
            .GroupBy(value => $"{value.SupId}_{value.SHotelCode}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.SupId)
            .ThenBy(value => value.SHotelCode, StringComparer.Ordinal)
            .ToArray();
    }

    internal static SupHotelInfoDto[] RemoveSupplierHotel(
        IEnumerable<SupHotelInfoDto> values,
        int supId,
        string supplierHotelCode)
        => values
            .Where(hotel => hotel.SupId != supId
                || !string.Equals(
                    hotel.SHotelCode,
                    supplierHotelCode,
                    StringComparison.Ordinal))
            .ToArray();

}
