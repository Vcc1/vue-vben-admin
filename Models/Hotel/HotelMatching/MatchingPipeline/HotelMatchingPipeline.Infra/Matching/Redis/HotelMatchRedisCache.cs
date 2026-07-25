using System.Text.Encodings.Web;
using System.Text.Json;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace HotelMatchingPipeline.Infra.Redis;

public sealed class HotelMatchRedisCache : RedisContextBase, IHotelMatchCache
{
    public const string MappingHashKey = "HotelMatching:Mapping";
    public const string ConflictHashKey = "HotelMatching:Conflict";

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public HotelMatchRedisCache(IConfiguration configuration)
        : base(configuration, "RoomMappingRedis")
    {
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

        var transaction = DB.CreateTransaction();
        var operations = new List<Task>(evaluations.Count * 2);
        foreach (var evaluation in evaluations)
        {
            var field = $"{evaluation.SupId}_{evaluation.SupHotelCode}";
            operations.Add(transaction.HashSetAsync(
                MappingHashKey,
                field,
                evaluation.ResolvedMTHotelCode));

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
                    ConflictHashKey,
                    field,
                    conflictJson));
            }
            else
            {
                operations.Add(transaction.HashDeleteAsync(ConflictHashKey, field));
            }
        }

        if (!await transaction.ExecuteAsync())
        {
            throw new RedisException("Hotel matching Redis transaction was not committed.");
        }
        await Task.WhenAll(operations);
    }
}
