using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.SupplierStaticData;

public sealed class SupplierStaticDataCacheRepository : ISupplierStaticDataCacheRepository
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly RoomMappingDbContext _dbContext;

    static SupplierStaticDataCacheRepository()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public SupplierStaticDataCacheRepository(RoomMappingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataEnvelope>> GetHotelAndRoomsAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        CancellationToken cancellationToken)
    {
        return await GetAsync(supId, supplierHotelCodes, SupplierStaticDataKind.HotelAndRoom, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataEnvelope>> GetAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataKind dataKind,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = supplierHotelCodes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, SupplierStaticDataEnvelope>(StringComparer.OrdinalIgnoreCase);
        }

        var candidates = await _dbContext.SupHotelDataSources
            .Where(item => item.SupId == supId && normalizedCodes.Contains(item.SHotelCode))
            .OrderByDescending(item => item.DataUpdateTime)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, SupplierStaticDataEnvelope>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (result.ContainsKey(candidate.SHotelCode))
            {
                continue;
            }

            var envelope = TryDeserializeEnvelope(candidate.Data);
            if (envelope is null
                || envelope.DataKind != dataKind
                || !string.Equals(envelope.HotelCode, candidate.SHotelCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[candidate.SHotelCode] = envelope;
        }

        return result;
    }

    public async Task SaveAsync(
        SupplierStaticDataCacheLookup lookup,
        SupplierStaticDataEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var normalizedEnvelope = NormalizeEnvelope(lookup, envelope);
        var serializedEnvelope = JsonSerializer.Serialize(normalizedEnvelope, JsonOptions);

        var candidates = await _dbContext.SupHotelDataSources
            .AsTracking()
            .Where(item => item.SupId == lookup.SupId && item.SHotelCode == lookup.HotelCode)
            .OrderByDescending(item => item.DataUpdateTime)
            .ToListAsync(cancellationToken);

        var existing = candidates.FirstOrDefault(item =>
        {
            var cachedEnvelope = TryDeserializeEnvelope(item.Data);
            return cachedEnvelope is not null
                && cachedEnvelope.DataKind == lookup.DataKind
                && string.Equals(cachedEnvelope.HotelCode, lookup.HotelCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(cachedEnvelope.RoomCode ?? string.Empty, lookup.RoomCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });

        existing ??= candidates.FirstOrDefault(item => TryDeserializeEnvelope(item.Data) is null);

        if (existing is null)
        {
            _dbContext.SupHotelDataSources.Add(new SupHotelDataSource
            {
                MTHotelCode = lookup.MTHotelCode ?? string.Empty,
                SHotelCode = lookup.HotelCode,
                SupId = lookup.SupId,
                Data = serializedEnvelope,
                CreateTime = now,
                UpdateTime = now,
                DataUpdateTime = now
            });
        }
        else
        {
            existing.MTHotelCode = lookup.MTHotelCode ?? existing.MTHotelCode;
            existing.SHotelCode = lookup.HotelCode;
            existing.SupId = lookup.SupId;
            existing.Data = serializedEnvelope;
            existing.UpdateTime = now;
            existing.DataUpdateTime = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    static SupplierStaticDataEnvelope NormalizeEnvelope(
        SupplierStaticDataCacheLookup lookup,
        SupplierStaticDataEnvelope envelope)
    {
        envelope.SupId = lookup.SupId;
        envelope.DataKind = lookup.DataKind;
        envelope.HotelCode = lookup.HotelCode;
        envelope.RoomCode = lookup.RoomCode;
        envelope.FetchedAt = envelope.FetchedAt == default ? DateTimeOffset.Now : envelope.FetchedAt;
        envelope.PayloadFormat = string.IsNullOrWhiteSpace(envelope.PayloadFormat) ? "json" : envelope.PayloadFormat;
        envelope.Payload ??= string.Empty;
        return envelope;
    }

    static SupplierStaticDataEnvelope? TryDeserializeEnvelope(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SupplierStaticDataEnvelope>(data, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
