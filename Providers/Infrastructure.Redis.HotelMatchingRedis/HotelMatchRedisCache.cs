using System.Text.Encodings.Web;
using System.Text.Json;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace Infrastructure.Redis.HotelMatchingRedis;

public sealed class HotelMatchRedisCache : RedisContextBase, IHotelMatchCache
{
    readonly IHotelMappingRedisStore _mappingStore;
    readonly SemaphoreSlim _writeLock = new(1, 1);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public HotelMatchRedisCache(
        IConfiguration configuration,
        IHotelMappingRedisStore mappingStore)
        : base(configuration, "HotelMappingRedis")
    {
        _mappingStore = mappingStore;
    }

    public async Task SaveAsync(
        IReadOnlyCollection<HotelMatchEvaluation> evaluations,
        int matchingVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (evaluations.Count == 0)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var current = await _mappingStore.ReadMappingsAsync(cancellationToken);
            var desired = current.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToDictionary(GetSupplierHotelCode, StringComparer.Ordinal),
                StringComparer.Ordinal);
            var currentSupplierHotels = current.Values
                .SelectMany(hotels => hotels)
                .ToDictionary(GetSupplierHotelCode, StringComparer.Ordinal);
            var affectedSupplierHotels = evaluations
                .Select(GetSupplierHotelCode)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var supplierHotels in desired.Values)
            {
                foreach (var supplierHotel in affectedSupplierHotels)
                {
                    supplierHotels.Remove(supplierHotel);
                }
            }

            foreach (var emptyGroup in desired
                .Where(pair => pair.Value.Count == 0)
                .Select(pair => pair.Key)
                .ToArray())
            {
                desired.Remove(emptyGroup);
            }

            foreach (var evaluation in evaluations)
            {
                var mtHotelCode = evaluation.ResolvedMTHotelCode.Trim();
                if (mtHotelCode.Length == 0)
                {
                    continue;
                }

                if (!desired.TryGetValue(mtHotelCode, out var supplierHotels))
                {
                    supplierHotels = new Dictionary<string, SupHotelInfoDto>(StringComparer.Ordinal);
                    desired.Add(mtHotelCode, supplierHotels);
                }

                var supplierHotelCode = GetSupplierHotelCode(evaluation);
                var supplierHotel = currentSupplierHotels.TryGetValue(supplierHotelCode, out var existing)
                    ? existing
                    : new SupHotelInfoDto
                    {
                        SupId = evaluation.SupId,
                        SHotelCode = evaluation.SupHotelCode.Trim()
                    };
                supplierHotels[supplierHotelCode] = supplierHotel;
            }

            await _mappingStore.ApplyAsync(
                CreateMappingOperations(current, desired),
                cancellationToken);
            await SaveConflictsAsync(evaluations, matchingVersion, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    async Task SaveConflictsAsync(
        IReadOnlyCollection<HotelMatchEvaluation> evaluations,
        int matchingVersion,
        CancellationToken cancellationToken)
    {
        var transaction = DB.CreateTransaction();
        var operations = new List<Task>(evaluations.Count);
        foreach (var evaluation in evaluations)
        {
            var field = GetSupplierHotelCode(evaluation);
            if (evaluation.HasConflict)
            {
                var conflictJson = JsonSerializer.Serialize(
                    new
                    {
                        evaluation.SupHotelId,
                        evaluation.SupId,
                        evaluation.SupHotelCode,
                        candidateCount = evaluation.Matches.Count,
                        mtHotelCodes = evaluation.Matches
                            .Select(match => match.MTHotelCode)
                            .ToArray(),
                        matchingVersion
                    },
                    JsonOptions);
                operations.Add(transaction.HashSetAsync(
                    HotelMatchingRedisKeys.Conflict,
                    field,
                    conflictJson));
            }
            else
            {
                operations.Add(transaction.HashDeleteAsync(
                    HotelMatchingRedisKeys.Conflict,
                    field));
            }
        }

        if (!await transaction.ExecuteAsync())
        {
            throw new RedisException("Hotel matching Redis transaction was not committed.");
        }

        await Task.WhenAll(operations);
    }

    static IReadOnlyList<HotelMappingSyncOperation> CreateMappingOperations(
        IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>> current,
        IReadOnlyDictionary<string, Dictionary<string, SupHotelInfoDto>> desired)
    {
        var operations = new List<HotelMappingSyncOperation>();
        foreach (var pair in desired.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var desiredSupplierHotels = pair.Value.Values
                .OrderBy(GetSupplierHotelCode, StringComparer.Ordinal)
                .ToArray();
            if (!current.TryGetValue(pair.Key, out var currentSupplierHotels))
            {
                operations.Add(new HotelMappingSyncOperation(
                    HotelMappingSyncOperationType.Add,
                    pair.Key,
                    null,
                    desiredSupplierHotels));
            }
            else if (!desiredSupplierHotels.SequenceEqual(
                currentSupplierHotels.OrderBy(GetSupplierHotelCode, StringComparer.Ordinal)))
            {
                operations.Add(new HotelMappingSyncOperation(
                    HotelMappingSyncOperationType.Update,
                    pair.Key,
                    currentSupplierHotels,
                    desiredSupplierHotels));
            }
        }

        foreach (var pair in current.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!desired.ContainsKey(pair.Key))
            {
                operations.Add(new HotelMappingSyncOperation(
                    HotelMappingSyncOperationType.Delete,
                    pair.Key,
                    pair.Value,
                    null));
            }
        }

        return operations;
    }

    static string GetSupplierHotelCode(HotelMatchEvaluation evaluation)
        => $"{evaluation.SupId}_{evaluation.SupHotelCode.Trim()}";

    static string GetSupplierHotelCode(SupHotelInfoDto hotel)
        => $"{hotel.SupId}_{hotel.SHotelCode}";
}
