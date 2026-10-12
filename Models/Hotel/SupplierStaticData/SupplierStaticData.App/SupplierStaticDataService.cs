using Microsoft.Extensions.Logging;
using SupplierStaticDataApp.Contracts;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataService : ISupplierStaticDataService
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly IReadOnlyDictionary<int, ISupplierStaticDataProvider> _providers;
    readonly ISupplierStaticDataCacheRepository _cacheRepository;
    readonly ILogger<SupplierStaticDataService> _logger;
    readonly SupplierStaticDataMetrics _metrics;

    public SupplierStaticDataService(
        IEnumerable<ISupplierStaticDataProvider> providers,
        ISupplierStaticDataCacheRepository cacheRepository,
        ILogger<SupplierStaticDataService> logger,
        SupplierStaticDataMetrics metrics)
    {
        _providers = providers.ToDictionary(provider => provider.SupId);
        _cacheRepository = cacheRepository;
        _logger = logger;
        _metrics = metrics;
    }

    static SupplierStaticDataService()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public async Task<SupplierStaticDataResult<SupplierStaticHotelDto>> GetHotelBySupplierHotelCodeAsync(
        int supId,
        string supplierHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var result = await GetHotelAndRoomsBySupplierHotelCodeAsync(supId, supplierHotelCode, options, cancellationToken);
        return ProjectHotel(result);
    }

    public async Task<SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>> GetRoomsBySupplierHotelCodeAsync(
        int supId,
        string supplierHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsBySupplierHotelCodesAsync(
            supId,
            [supplierHotelCode],
            options,
            cancellationToken,
            SupplierStaticDataKind.Room);

        var result = results.TryGetValue(supplierHotelCode.Trim(), out var item)
            ? item
            : SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                supplierHotelCode,
                "request_invalid",
                "Supplier hotel code is invalid.");

        return ProjectRooms(result);
    }

    public async Task<SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>> GetHotelAndRoomsBySupplierHotelCodeAsync(
        int supId,
        string supplierHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsBySupplierHotelCodesAsync(
            supId,
            [supplierHotelCode],
            options,
            cancellationToken);

        return results.TryGetValue(supplierHotelCode.Trim(), out var result)
            ? result
            : SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                supplierHotelCode,
                "request_invalid",
                "Supplier hotel code is invalid.");
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelDto>>> GetHotelsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsBySupplierHotelCodesAsync(supId, supplierHotelCodes, options, cancellationToken);
        return results.ToDictionary(pair => pair.Key, pair => ProjectHotel(pair.Value), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>>> GetRoomsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsBySupplierHotelCodesAsync(
            supId,
            supplierHotelCodes,
            options,
            cancellationToken,
            SupplierStaticDataKind.Room);

        return results.ToDictionary(pair => pair.Key, pair => ProjectRooms(pair.Value), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>> GetHotelAndRoomsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        return await GetHotelAndRoomsBySupplierHotelCodesAsync(
            supId,
            supplierHotelCodes,
            options,
            cancellationToken,
            SupplierStaticDataKind.HotelAndRoom);
    }

    async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>> GetHotelAndRoomsBySupplierHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken,
        SupplierStaticDataKind requestedKind)
    {
        var normalizedCodes = NormalizeCodes(supplierHotelCodes);
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var results = await GetHotelAndRoomsBySupplierHotelCodesCoreAsync(
                supId,
                normalizedCodes,
                options,
                cancellationToken,
                requestedKind);

            RecordResults(supId, normalizedCodes, options, requestedKind, results, Stopwatch.GetElapsedTime(startedAt));
            return results;
        }
        catch (Exception ex)
        {
            RecordException(supId, normalizedCodes, options, requestedKind, Stopwatch.GetElapsedTime(startedAt), ex);
            throw;
        }
    }

    async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>> GetHotelAndRoomsBySupplierHotelCodesCoreAsync(
        int supId,
        IReadOnlyCollection<string> normalizedCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken,
        SupplierStaticDataKind requestedKind)
    {
        var provider = GetProvider(supId);
        if (provider is null)
        {
            return BuildProviderMissingResults<SupplierStaticHotelWithRoomsDto>(normalizedCodes, supId);
        }

        if (!provider.Supports(requestedKind))
        {
            throw new NotSupportedException($"Supplier static data provider for SupId {supId} does not support {requestedKind}.");
        }

        var results = new Dictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>(StringComparer.OrdinalIgnoreCase);
        var codesToFetch = new List<string>();
        var partials = new Dictionary<string, PartialStaticData>(StringComparer.OrdinalIgnoreCase);
        var needHotel = requestedKind is SupplierStaticDataKind.Hotel or SupplierStaticDataKind.HotelAndRoom;
        var needRooms = requestedKind is SupplierStaticDataKind.Room or SupplierStaticDataKind.HotelAndRoom;

        if (!options.ForceRefresh)
        {
            var cachedEnvelopes = await _cacheRepository.GetHotelAndRoomsAsync(supId, normalizedCodes, cancellationToken);
            var cachedHotels = needHotel
                ? await _cacheRepository.GetAsync(supId, normalizedCodes, SupplierStaticDataKind.Hotel, cancellationToken)
                : new Dictionary<string, SupplierStaticDataEnvelope>(StringComparer.OrdinalIgnoreCase);
            var cachedRooms = needRooms
                ? await _cacheRepository.GetAsync(supId, normalizedCodes, SupplierStaticDataKind.Room, cancellationToken)
                : new Dictionary<string, SupplierStaticDataEnvelope>(StringComparer.OrdinalIgnoreCase);

            foreach (var code in normalizedCodes)
            {
                if (cachedEnvelopes.TryGetValue(code, out var envelope))
                {
                    var parsed = provider.ParseHotelAndRooms(code, envelope);
                    if (parsed.Success
                        && parsed.Data is not null
                        && CombinedEnvelopeSatisfies(supId, envelope, needHotel, needRooms))
                    {
                        var rawInfo = options.IncludeRawInfo
                            ? CreateCacheRawInfo(envelope, requestedKind)
                            : null;

                        results[code] = SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.SuccessResult(
                            code,
                            parsed.Data,
                            code,
                            fromCache: true,
                            refreshed: false,
                            rawInfo,
                            source: "cache",
                            sourceDetails: CreateSourceDetails("cache", needHotel, needRooms));
                        continue;
                    }
                }

                var partial = new PartialStaticData
                {
                    HotelEnvelope = cachedHotels.GetValueOrDefault(code),
                    RoomEnvelope = cachedRooms.GetValueOrDefault(code),
                    HotelSource = cachedHotels.ContainsKey(code) ? "cache" : null,
                    RoomSource = cachedRooms.ContainsKey(code) ? "cache" : null
                };

                if (partial.HasRequired(needHotel, needRooms))
                {
                    var combined = CreateCombinedEnvelope(supId, code, partial.HotelEnvelope, partial.RoomEnvelope);
                    var parsed = provider.ParseHotelAndRooms(code, combined);
                    if (parsed.Success && parsed.Data is not null)
                    {
                        var sourceDetails = new SupplierStaticDataSourceDetails
                        {
                            Hotel = needHotel ? partial.HotelSource : null,
                            Rooms = needRooms ? partial.RoomSource : null
                        };
                        var rawInfo = options.IncludeRawInfo ? CreateRawInfo(partial, requestedKind) : null;
                        var source = CreateAggregateSource(sourceDetails);

                        results[code] = SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.SuccessResult(
                            code,
                            parsed.Data,
                            code,
                            fromCache: source == "cache",
                            refreshed: false,
                            rawInfo,
                            source,
                            sourceDetails);
                        continue;
                    }
                }

                partials[code] = partial;
                if (!partial.HasRequired(needHotel, needRooms))
                {
                    codesToFetch.Add(code);
                    continue;
                }
            }
        }
        else
        {
            codesToFetch.AddRange(normalizedCodes);
        }

        if (codesToFetch.Count > 0)
        {
            var fetchResult = await provider.FetchHotelAndRoomsEnvelopesAsync(codesToFetch, options, cancellationToken);
            var fetchedEnvelopes = fetchResult.Envelopes;

            foreach (var code in codesToFetch)
            {
                fetchResult.RawInfos.TryGetValue(code, out var rawInfo);
                fetchedEnvelopes.TryGetValue(code, out var envelopes);
                var envelopeList = envelopes ?? [];
                if (envelopeList.Count == 0)
                {
                    results[code] = SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                        code,
                        "data_not_found",
                        "Supplier static data was not returned by the provider.",
                        code,
                        rawInfo,
                        source: "direct");
                    continue;
                }

                foreach (var item in envelopeList)
                {
                    await _cacheRepository.SaveAsync(
                        new SupplierStaticDataCacheLookup
                        {
                            SupId = supId,
                            HotelCode = code,
                            DataKind = item.DataKind
                        },
                        item,
                        cancellationToken);
                }

                partials.TryGetValue(code, out var existingPartial);
                var partial = MergePartial(existingPartial, envelopeList, rawInfo);
                var envelope = envelopeList.Count == 1 && envelopeList[0].DataKind == SupplierStaticDataKind.HotelAndRoom
                    ? envelopeList[0]
                    : CreateCombinedEnvelope(supId, code, partial.HotelEnvelope, partial.RoomEnvelope);

                var parsed = provider.ParseHotelAndRooms(code, envelope);
                if (!parsed.Success || parsed.Data is null)
                {
                    results[code] = SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                        parsed.RequestedCode,
                        parsed.ErrorCode ?? "unknown_error",
                        parsed.ErrorMessage ?? "Supplier static data failed.",
                        parsed.ResolvedSupplierHotelCode,
                        rawInfo,
                        source: "direct");
                    continue;
                }

                results[code] = SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.SuccessResult(
                    code,
                    parsed.Data,
                    code,
                    fromCache: CreateAggregateSource(partial.ToSourceDetails(needHotel, needRooms)) == "cache",
                    refreshed: true,
                    options.IncludeRawInfo ? CreateRawInfo(partial, requestedKind, rawInfo) : null,
                    CreateAggregateSource(partial.ToSourceDetails(needHotel, needRooms)),
                    partial.ToSourceDetails(needHotel, needRooms));
            }
        }

        foreach (var code in normalizedCodes.Where(code => !results.ContainsKey(code)))
        {
            results[code] = SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                code,
                "data_not_found",
                "Supplier static data could not be resolved.",
                code,
                source: "direct");
        }

        return results;
    }

    void RecordResults(
        int supId,
        IReadOnlyCollection<string> normalizedCodes,
        SupplierStaticDataRequestOptions options,
        SupplierStaticDataKind requestedKind,
        IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>> results,
        TimeSpan elapsed)
    {
        foreach (var code in normalizedCodes)
        {
            var result = results.TryGetValue(code, out var item)
                ? item
                : SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                    code,
                    "request_invalid",
                    "Supplier hotel code is invalid.");

            var roomCount = SupplierStaticDataMetrics.GetRoomCount(result);
            var source = SupplierStaticDataMetrics.NormalizeSource(result.Source);
            var errorCode = SupplierStaticDataMetrics.NormalizeErrorCode(result.ErrorCode);
            var mtHotelCode = options.GetMTHotelCode(code);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Supplier static data request completed. SupId={SupId}, SHotelCode={SHotelCode}, MTHotelCode={MTHotelCode}, Success={Success}, ElapsedMilliseconds={ElapsedMilliseconds}, RoomCount={RoomCount}, Source={Source}, ErrorCode={ErrorCode}",
                    supId,
                    code,
                    mtHotelCode,
                    true,
                    Math.Max(0, elapsed.TotalMilliseconds),
                    roomCount,
                    source,
                    errorCode);
            }
            else
            {
                _logger.LogWarning(
                    "Supplier static data request completed. SupId={SupId}, SHotelCode={SHotelCode}, MTHotelCode={MTHotelCode}, Success={Success}, ElapsedMilliseconds={ElapsedMilliseconds}, RoomCount={RoomCount}, Source={Source}, ErrorCode={ErrorCode}",
                    supId,
                    code,
                    mtHotelCode,
                    false,
                    Math.Max(0, elapsed.TotalMilliseconds),
                    roomCount,
                    source,
                    errorCode);
            }

            _metrics.RecordLookup(supId, requestedKind, result, elapsed);
        }
    }

    void RecordException(
        int supId,
        IReadOnlyCollection<string> normalizedCodes,
        SupplierStaticDataRequestOptions options,
        SupplierStaticDataKind requestedKind,
        TimeSpan elapsed,
        Exception exception)
    {
        foreach (var code in normalizedCodes.DefaultIfEmpty(string.Empty))
        {
            var mtHotelCode = string.IsNullOrWhiteSpace(code) ? options.MTHotelCode : options.GetMTHotelCode(code);
            _logger.LogWarning(
                exception,
                "Supplier static data request completed. SupId={SupId}, SHotelCode={SHotelCode}, MTHotelCode={MTHotelCode}, Success={Success}, ElapsedMilliseconds={ElapsedMilliseconds}, RoomCount={RoomCount}, Source={Source}, ErrorCode={ErrorCode}",
                supId,
                code,
                mtHotelCode,
                false,
                Math.Max(0, elapsed.TotalMilliseconds),
                0,
                "unknown",
                "exception");

            _metrics.RecordException(supId, requestedKind, elapsed);
        }
    }

    public async Task<SupplierStaticDataResult<SupplierStaticHotelDto>> GetHotelByMTHotelCodeAsync(
        int supId,
        string mtHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var result = await GetHotelAndRoomsByMTHotelCodeAsync(supId, mtHotelCode, options, cancellationToken);
        return ProjectHotel(result);
    }

    public async Task<SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>> GetRoomsByMTHotelCodeAsync(
        int supId,
        string mtHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var result = await GetHotelAndRoomsByMTHotelCodeAsync(supId, mtHotelCode, options, cancellationToken);
        return ProjectRooms(result);
    }

    public async Task<SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>> GetHotelAndRoomsByMTHotelCodeAsync(
        int supId,
        string mtHotelCode,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsByMTHotelCodesAsync(
            supId,
            [mtHotelCode],
            options,
            cancellationToken);

        return results.TryGetValue(mtHotelCode.Trim(), out var result)
            ? result
            : SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                mtHotelCode,
                "request_invalid",
                "MTHotelCode is invalid.");
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelDto>>> GetHotelsByMTHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsByMTHotelCodesAsync(supId, mtHotelCodes, options, cancellationToken);
        return results.ToDictionary(pair => pair.Key, pair => ProjectHotel(pair.Value), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>>> GetRoomsByMTHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var results = await GetHotelAndRoomsByMTHotelCodesAsync(supId, mtHotelCodes, options, cancellationToken);
        return results.ToDictionary(pair => pair.Key, pair => ProjectRooms(pair.Value), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>>> GetHotelAndRoomsByMTHotelCodesAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("Querying supplier static data by MTHotelCode is not supported yet. Use SHotelCode instead.");
    }

    static SupplierStaticDataResult<SupplierStaticHotelDto> ProjectHotel(
        SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> source)
    {
        if (!source.Success || source.Data is null)
        {
            return SupplierStaticDataResult<SupplierStaticHotelDto>.Failure(
                source.RequestedCode,
                source.ErrorCode ?? "unknown_error",
                source.ErrorMessage ?? "Supplier static data failed.",
                source.ResolvedSupplierHotelCode,
                source.RawInfo,
                source.Source,
                source.SourceDetails);
        }

        return SupplierStaticDataResult<SupplierStaticHotelDto>.SuccessResult(
            source.RequestedCode,
            source.Data.Hotel,
            source.ResolvedSupplierHotelCode,
            source.FromCache,
            source.Refreshed,
            source.RawInfo,
            source.Source,
            source.SourceDetails);
    }

    static SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>> ProjectRooms(
        SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> source)
    {
        if (!source.Success || source.Data is null)
        {
            return SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>.Failure(
                source.RequestedCode,
                source.ErrorCode ?? "unknown_error",
                source.ErrorMessage ?? "Supplier static data failed.",
                source.ResolvedSupplierHotelCode,
                source.RawInfo,
                source.Source,
                source.SourceDetails);
        }

        return SupplierStaticDataResult<IReadOnlyList<SupplierStaticRoomDto>>.SuccessResult(
            source.RequestedCode,
            source.Data.Rooms,
            source.ResolvedSupplierHotelCode,
            source.FromCache,
            source.Refreshed,
            source.RawInfo,
            source.Source,
            source.SourceDetails);
    }

    ISupplierStaticDataProvider? GetProvider(int supId)
    {
        if (_providers.TryGetValue(supId, out var provider))
        {
            return provider;
        }

        _logger.LogWarning("No supplier static data provider registered. SupId={SupId}", supId);
        return null;
    }

    static string[] NormalizeCodes(IReadOnlyCollection<string> codes)
    {
        return codes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static IReadOnlyDictionary<string, SupplierStaticDataResult<T>> BuildProviderMissingResults<T>(
        IReadOnlyCollection<string> requestedCodes,
        int supId)
    {
        return requestedCodes.ToDictionary(
            code => code,
            code => SupplierStaticDataResult<T>.Failure(
                code,
                "provider_not_registered",
                $"No supplier static data provider is registered for SupId {supId}."),
            StringComparer.OrdinalIgnoreCase);
    }

    static SupplierStaticDataEnvelope CreateCombinedEnvelope(
        int supId,
        string hotelCode,
        SupplierStaticDataEnvelope? hotelEnvelope,
        SupplierStaticDataEnvelope? roomEnvelope)
    {
        var payload = new JsonObject
        {
            ["hotel"] = ParseNodeOrNull(hotelEnvelope?.Payload),
            ["rooms"] = ParseNodeOrNull(roomEnvelope?.Payload)
        }.ToJsonString(JsonOptions);

        return new SupplierStaticDataEnvelope
        {
            SupId = supId,
            DataKind = SupplierStaticDataKind.HotelAndRoom,
            HotelCode = hotelCode,
            FetchedAt = DateTimeOffset.Now,
            PayloadFormat = "json",
            Payload = payload
        };
    }

    static SupplierStaticDataRawInfo CreateCacheRawInfo(
        SupplierStaticDataEnvelope envelope,
        SupplierStaticDataKind requestedKind)
    {
        var rawInfo = new SupplierStaticDataRawInfo
        {
            Source = "cache",
            ResponseBody = envelope.Payload
        };

        if (requestedKind == SupplierStaticDataKind.HotelAndRoom)
        {
            rawInfo.Items = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["hotelAndRooms"] = new()
                {
                    Source = "cache",
                    ResponseBody = envelope.Payload
                }
            };
        }

        return rawInfo;
    }

    static SupplierStaticDataRawInfo CreateRawInfo(
        PartialStaticData partial,
        SupplierStaticDataKind requestedKind,
        SupplierStaticDataRawInfo? directRawInfo = null)
    {
        var items = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
        if (requestedKind is SupplierStaticDataKind.Hotel or SupplierStaticDataKind.HotelAndRoom)
        {
            if (partial.HotelSource == "cache" && partial.HotelEnvelope is not null)
            {
                items["hotel"] = new SupplierStaticDataRawInfo
                {
                    Source = "cache",
                    ResponseBody = partial.HotelEnvelope.Payload
                };
            }
            else if (directRawInfo?.Items?.TryGetValue("hotel", out var hotelRawInfo) == true)
            {
                items["hotel"] = hotelRawInfo;
            }
        }

        if (requestedKind is SupplierStaticDataKind.Room or SupplierStaticDataKind.HotelAndRoom)
        {
            if (partial.RoomSource == "cache" && partial.RoomEnvelope is not null)
            {
                items["rooms"] = new SupplierStaticDataRawInfo
                {
                    Source = "cache",
                    ResponseBody = partial.RoomEnvelope.Payload
                };
            }
            else if (directRawInfo?.Items?.TryGetValue("rooms", out var roomRawInfo) == true)
            {
                items["rooms"] = roomRawInfo;
            }
        }

        return new SupplierStaticDataRawInfo
        {
            Source = CreateAggregateSource(partial.ToSourceDetails(
                requestedKind is SupplierStaticDataKind.Hotel or SupplierStaticDataKind.HotelAndRoom,
                requestedKind is SupplierStaticDataKind.Room or SupplierStaticDataKind.HotelAndRoom)),
            Items = items.Count == 0 ? null : items,
            RequestUrl = directRawInfo?.RequestUrl,
            ResponseStatusCode = directRawInfo?.ResponseStatusCode,
            ResponseBody = directRawInfo?.ResponseBody
        };
    }

    static PartialStaticData MergePartial(
        PartialStaticData? existing,
        IReadOnlyList<SupplierStaticDataEnvelope> fetchedEnvelopes,
        SupplierStaticDataRawInfo? directRawInfo)
    {
        var partial = existing ?? new PartialStaticData();
        foreach (var envelope in fetchedEnvelopes)
        {
            if (envelope.DataKind == SupplierStaticDataKind.HotelAndRoom)
            {
                partial.HotelAndRoomsEnvelope = envelope;
                partial.HotelSource ??= "direct";
                partial.RoomSource ??= "direct";
                continue;
            }

            if (envelope.DataKind == SupplierStaticDataKind.Hotel && partial.HotelEnvelope is null)
            {
                partial.HotelEnvelope = envelope;
                partial.HotelSource = "direct";
            }

            if (envelope.DataKind == SupplierStaticDataKind.Room && partial.RoomEnvelope is null)
            {
                partial.RoomEnvelope = envelope;
                partial.RoomSource = "direct";
            }
        }

        if (partial.HotelAndRoomsEnvelope is not null)
        {
            partial.HotelEnvelope ??= partial.HotelAndRoomsEnvelope;
            partial.RoomEnvelope ??= partial.HotelAndRoomsEnvelope;
        }

        return partial;
    }

    static SupplierStaticDataSourceDetails CreateSourceDetails(string source, bool includeHotel, bool includeRooms)
    {
        return new SupplierStaticDataSourceDetails
        {
            Hotel = includeHotel ? source : null,
            Rooms = includeRooms ? source : null
        };
    }

    static string CreateAggregateSource(SupplierStaticDataSourceDetails? sourceDetails)
    {
        var sources = new[] { sourceDetails?.Hotel, sourceDetails?.Rooms }
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sources.Length == 0)
        {
            return string.Empty;
        }

        return sources.Length == 1 ? sources[0]! : "mixed";
    }

    static JsonNode? ParseNodeOrNull(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return JsonValue.Create(payload);
        }
    }

    static bool CombinedEnvelopeSatisfies(
        int supId,
        SupplierStaticDataEnvelope envelope,
        bool needHotel,
        bool needRooms)
    {
        if (supId != 32 || envelope.DataKind != SupplierStaticDataKind.HotelAndRoom)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(envelope.Payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return !needHotel;
            }

            var hasHotel = document.RootElement.TryGetProperty("hotel", out var hotel)
                && hotel.ValueKind != JsonValueKind.Null;
            var hasRooms = document.RootElement.TryGetProperty("rooms", out var rooms)
                && rooms.ValueKind != JsonValueKind.Null;

            return (!needHotel || hasHotel) && (!needRooms || hasRooms);
        }
        catch (JsonException)
        {
            return !needHotel;
        }
    }

    sealed class PartialStaticData
    {
        public SupplierStaticDataEnvelope? HotelAndRoomsEnvelope { get; set; }

        public SupplierStaticDataEnvelope? HotelEnvelope { get; set; }

        public SupplierStaticDataEnvelope? RoomEnvelope { get; set; }

        public string? HotelSource { get; set; }

        public string? RoomSource { get; set; }

        public bool HasRequired(bool needHotel, bool needRooms)
        {
            return (!needHotel || HotelEnvelope is not null || HotelAndRoomsEnvelope is not null)
                && (!needRooms || RoomEnvelope is not null || HotelAndRoomsEnvelope is not null);
        }

        public SupplierStaticDataSourceDetails ToSourceDetails(bool includeHotel, bool includeRooms)
        {
            return new SupplierStaticDataSourceDetails
            {
                Hotel = includeHotel ? HotelSource : null,
                Rooms = includeRooms ? RoomSource : null
            };
        }
    }
}
