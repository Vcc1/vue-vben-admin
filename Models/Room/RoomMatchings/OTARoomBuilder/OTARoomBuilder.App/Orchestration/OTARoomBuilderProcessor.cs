using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTARoomBuilder.Contracts;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomNameNormalizer;

namespace OTARoomBuilder;

public sealed class OTARoomBuilderProcessor
{
    readonly IRoomAnalyzer _roomAnalyzer;
    readonly IRoomNameNormalizer _roomNameNormalizer;
    readonly IOtaRoomBuilderRepository _repository;
    readonly IOtaRoomCreateClient _roomCreateClient;
    readonly IReadOnlyDictionary<int, ISupplierRoomBuildPlugin> _pluginsBySupId;
    readonly IOptionsMonitor<OTARoomBuilderOptions> _options;
    readonly OTARoomBuilderMetrics _metrics;
    readonly ILogger<OTARoomBuilderProcessor> _logger;
    readonly IServiceProvider _serviceProvider;

    public OTARoomBuilderProcessor(
        IRoomAnalyzer roomAnalyzer,
        IRoomNameNormalizer roomNameNormalizer,
        IOtaRoomBuilderRepository repository,
        IOtaRoomCreateClient roomCreateClient,
        IEnumerable<ISupplierRoomBuildPlugin> plugins,
        IOptionsMonitor<OTARoomBuilderOptions> options,
        OTARoomBuilderMetrics metrics,
        ILogger<OTARoomBuilderProcessor> logger,
        IServiceProvider serviceProvider)
    {
        _roomAnalyzer = roomAnalyzer;
        _roomNameNormalizer = roomNameNormalizer;
        _repository = repository;
        _roomCreateClient = roomCreateClient;
        _pluginsBySupId = plugins
            .OrderBy(plugin => plugin.Priority)
            .GroupBy(plugin => plugin.SupId)
            .ToDictionary(group => group.Key, group => group.First());
        _options = options;
        _metrics = metrics;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var currentOptions = _options.CurrentValue;
        var mtHotelCodes = await LoadFullSyncHotelCodesAsync(cancellationToken);
        _metrics.ResetRun();
        _metrics.SetPendingHotels(mtHotelCodes.Count);
        _logger.LogInformation(
            "Loaded {HotelCount} hotel codes. TriggerSource={TriggerSource}, LastDaysOffset={LastDaysOffset}, OutputMode={OutputMode}",
            mtHotelCodes.Count,
            "full_sync",
            currentOptions.LastDaysOffset,
            currentOptions.OutputMode);

        foreach (var hotelChunk in mtHotelCodes.Chunk(Math.Max(1, currentOptions.TaskChunkSize)))
        {
            var batchContext = await LoadHotelBuildBatchContextAsync(
                hotelChunk,
                currentOptions.LastDaysOffset,
                currentOptions.OutputMode,
                cancellationToken);
            await ProcessHotelBuildBatchContextAsync(batchContext, cancellationToken);
        }
    }

    public Task RunIncrementalAsync(
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken)
    {
        var currentOptions = _options.CurrentValue;
        _metrics.ResetRun();
        _metrics.SetPendingHotels(mtHotelCodes.Count);
        _logger.LogInformation(
            "Loaded {HotelCount} hotel codes. TriggerSource={TriggerSource}, LastDaysOffset={LastDaysOffset}, OutputMode={OutputMode}",
            mtHotelCodes.Count,
            "incremental_queue",
            null,
            currentOptions.OutputMode);
        return RunIncrementalInternalAsync(mtHotelCodes, currentOptions, cancellationToken);
    }

    async Task RunIncrementalInternalAsync(
        IReadOnlyCollection<string> mtHotelCodes,
        OTARoomBuilderOptions currentOptions,
        CancellationToken cancellationToken)
    {
        foreach (var hotelChunk in mtHotelCodes.Chunk(Math.Max(1, currentOptions.TaskChunkSize)))
        {
            var batchContext = await LoadHotelBuildBatchContextAsync(
                hotelChunk,
                null,
                currentOptions.OutputMode,
                cancellationToken);
            await ProcessHotelBuildBatchContextAsync(batchContext, cancellationToken);
        }
    }

    public Task<IReadOnlyList<string>> LoadFullSyncHotelCodesAsync(CancellationToken cancellationToken)
    {
        var currentOptions = _options.CurrentValue;
        var overrideHotelCodes = currentOptions.GetFullSyncHotelCodeOverrides();
        if (overrideHotelCodes.Count > 0)
        {
            _logger.LogWarning(
                "Using configured OTARoomBuilder full sync hotel code override. HotelCount={HotelCount}, MTHotelCodes={MTHotelCodes}",
                overrideHotelCodes.Count,
                string.Join(",", overrideHotelCodes));
            return Task.FromResult(overrideHotelCodes);
        }

        return _repository.LoadMtHotelCodesAsync(
            currentOptions.LastDaysOffset,
            currentOptions.GetEnabledSupIds(),
            cancellationToken);
    }

    public async Task<HotelBuildBatchContext> LoadHotelBuildBatchContextAsync(
        string[] hotelChunk,
        int? lastDaysOffset,
        string outputMode,
        CancellationToken cancellationToken)
    {
        _metrics.RecordHotelBatch(hotelChunk.Length);

        var supplierRooms = await _repository.LoadPreferredSupplierRoomsAsync(
            hotelChunk,
            _options.CurrentValue.GetEnabledSupIds(),
            lastDaysOffset,
            cancellationToken);
        var otaRooms = await _repository.LoadOtaRoomsAsync(hotelChunk, cancellationToken);

        var otaRoomLookup = otaRooms
            .GroupBy(static room => room.MTHotelCode)
            .ToDictionary(static group => group.Key, static group => group.ToList());
        var supplierRoomLookup = supplierRooms
            .GroupBy(static room => room.MTHotelCode)
            .ToDictionary(static group => group.Key, static group => group.ToList());

        var hotels = new List<HotelRoomBuildContext>(hotelChunk.Length);
        foreach (var hotelCode in hotelChunk)
        {
            hotels.Add(new HotelRoomBuildContext
            {
                MTHotelCode = hotelCode,
                OtaRooms = otaRoomLookup.GetValueOrDefault(hotelCode) ?? [],
                SupplierRooms = supplierRoomLookup.GetValueOrDefault(hotelCode) ?? []
            });
        }

        return new HotelBuildBatchContext
        {
            Hotels = hotels,
            OutputMode = outputMode
        };
    }

    public async Task ProcessHotelBuildBatchContextAsync(
        HotelBuildBatchContext batchContext,
        CancellationToken cancellationToken)
    {
        foreach (var hotel in batchContext.Hotels)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var createdRooms = await BuildOtaRoomsForHotelAsync(hotel, cancellationToken);

                if (createdRooms.Count == 0)
                {
                    _metrics.RecordSkipped(1, "no_new_rooms");
                    continue;
                }

                await PersistCreatedRoomsAsync(createdRooms, batchContext.OutputMode, cancellationToken);
                _metrics.RecordCreated(createdRooms.Count, batchContext.OutputMode);
            }
            finally
            {
                _metrics.DecrementPendingHotels();
            }
        }
    }

    public Task<List<OTARoomInfoPre>> BuildOtaRoomsForHotelAsync(
        HotelRoomBuildContext hotel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        return BuildOtaRoomsForHotelCoreAsync(hotel, cancellationToken);
    }

    public Task<HotelRoomMatchResult> MatchOtaRoomsForHotelAsync(
        HotelRoomBuildContext hotel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        cancellationToken.ThrowIfCancellationRequested();

        if (hotel.SupplierRooms.Count == 0)
        {
            _metrics.RecordSkipped(1, "supplier_rooms_empty");
            return Task.FromResult(HotelRoomMatchResult.Empty(hotel.MTHotelCode));
        }

        var selectedPlugin = SelectPlugin(hotel.SupplierRooms);
        if (selectedPlugin is null)
        {
            _metrics.RecordSkipped(1, "plugin_missing");
            _logger.LogWarning(
                "No supplier plugin found for hotel {MTHotelCode}. Available SupIds={SupIds}",
                hotel.MTHotelCode,
                string.Join(",", hotel.SupplierRooms.Select(room => room.SupId).Distinct()));
            return Task.FromResult(HotelRoomMatchResult.Empty(hotel.MTHotelCode));
        }

        var filteredSupplierRooms = hotel.SupplierRooms
            .Where(room => room.SupId == selectedPlugin.SupId)
            .ToList();

        return Task.FromResult(MatchFromSupplierRooms(
            hotel.OtaRooms,
            filteredSupplierRooms,
            selectedPlugin,
            cancellationToken,
            hotel.MTHotelCode));
    }

    public async Task<List<OTARoomInfoPre>> EnrichMatchedRoomsForHotelAsync(
        HotelRoomMatchResult matchResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchResult);

        var createdRooms = new List<OTARoomInfoPre>(matchResult.Candidates.Count);
        var enrichmentItems = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in matchResult.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var supplierRoom = candidate.SupplierRoom;
            var plugin = _pluginsBySupId.GetValueOrDefault(supplierRoom.SupId);
            if ((supplierRoom.MaxAdt ?? 0) == 0)
            {
                if (plugin is null || !plugin.CanEnrich(supplierRoom))
                {
                    _metrics.RecordSkipped(1, "max_adt_missing");
                    continue;
                }

                _logger.LogInformation(
                    "OTARoomBuilder static data load start. Plugin={Plugin}, SupId={SupId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}, SupplierRoomId={SupplierRoomId}",
                    plugin.GetType().Name,
                    supplierRoom.SupId,
                    supplierRoom.MTHotelCode,
                    supplierRoom.SHotelCode,
                    supplierRoom.SRoomCode,
                    supplierRoom.Id);

                SupplierRoomEnrichmentResult enrichmentResult;
                try
                {
                    enrichmentResult = await plugin.EnrichAsync(
                        new SupplierRoomBuildContext
                        {
                            SupplierRoom = supplierRoom,
                            RoomContext = candidate.RoomContext,
                            Items = enrichmentItems
                        },
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _metrics.RecordStaticDataRoomMatch(supplierRoom.SupId, "lookup_failed");
                    _logger.LogWarning(
                        ex,
                        "Failed to enrich supplier room with static data. SupId={SupId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}",
                        supplierRoom.SupId,
                        supplierRoom.MTHotelCode,
                        supplierRoom.SHotelCode,
                        supplierRoom.SRoomCode);
                    continue;
                }

                if (enrichmentResult.Success
                    && enrichmentResult.MaxAdt is int maxAdt
                    && maxAdt > 0)
                {
                    supplierRoom.MaxAdt = maxAdt;
                    candidate.DraftRoom.Person = (byte)Math.Min(maxAdt, byte.MaxValue);
                    _logger.LogInformation(
                        "OTARoomBuilder static data load success. SupId={SupId}, MTHotelCode={MTHotelCode}, SHotelCode={SHotelCode}, SRoomCode={SRoomCode}, MaxAdt={MaxAdt}",
                        supplierRoom.SupId,
                        supplierRoom.MTHotelCode,
                        supplierRoom.SHotelCode,
                        supplierRoom.SRoomCode,
                        maxAdt);
                }
            }

            var resolvedMaxAdt = supplierRoom.MaxAdt;
            if (resolvedMaxAdt is not > 0)
            {
                _metrics.RecordSkipped(1, "max_adt_missing");
                continue;
            }

            candidate.DraftRoom.Person = (byte)Math.Min(resolvedMaxAdt.Value, byte.MaxValue);
            createdRooms.Add(candidate.DraftRoom);
        }

        return createdRooms;
    }

    public Task PersistCreatedRoomsAsync(
        List<OTARoomInfoPre> rooms,
        string outputMode,
        CancellationToken cancellationToken)
    {
        return PersistAsync(rooms, outputMode, cancellationToken);
    }

    async Task<List<OTARoomInfoPre>> BuildOtaRoomsForHotelCoreAsync(
        HotelRoomBuildContext hotel,
        CancellationToken cancellationToken)
    {
        var matchResult = await MatchOtaRoomsForHotelAsync(hotel, cancellationToken);
        return await EnrichMatchedRoomsForHotelAsync(matchResult, cancellationToken);
    }

    HotelRoomMatchResult MatchFromSupplierRooms(
        List<OTARoomInfo> otaRoomInfos,
        List<SupRoomInfo> supplierRooms,
        ISupplierRoomBuildPlugin plugin,
        CancellationToken cancellationToken,
        string mtHotelCode)
    {
        if (supplierRooms.Count == 0)
        {
            return HotelRoomMatchResult.Empty(mtHotelCode);
        }

        var matcher = plugin.ResolveMatcher(_serviceProvider);
        var setting = new RoomAnalysisSetting
        {
            CleanFormatType = CleanFormatType.BASIC_NORMALIZE,
            IsUnknownWordSplitBySpace = true
        };

        var otaContexts = otaRoomInfos
            .SelectMany(room => BuildOtaContexts(room, setting))
            .Where(static context => context is not null
                && !string.IsNullOrWhiteSpace(context.RoomNameOriginal)
                && !string.IsNullOrWhiteSpace(context.RoomNameCleaned))
            .Cast<RoomContext>()
            .ToList();

        var supplierContexts = supplierRooms
            .Select(room => new SupplierRoomCandidate(room, BuildSupplierContext(room.SRoomNameCN, room.SRoomNameEN, setting)))
            .ToList();

        var candidates = new List<SupplierRoomBuildCandidate>();

        foreach (var candidate in supplierContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanCreateFromSupplierRoom(candidate.Context))
            {
                continue;
            }

            if (HasMatchedOtaRoom(candidate.Context, otaContexts, matcher))
            {
                _metrics.RecordSkipped(1, "already_matched");
                continue;
            }

            var otaRoom = new OTARoomInfo
            {
                MTHotelCode = candidate.Room.MTHotelCode,
                RoomNameCN = candidate.Context.CN is null ? string.Empty : _roomNameNormalizer.Normalize(candidate.Context.CN),
                RoomNameEN = candidate.Context.EN is null ? string.Empty : _roomNameNormalizer.Normalize(candidate.Context.EN),
                Smoke = (byte)(candidate.Room.SmokeType ?? 0),
                Person = (byte)(candidate.Room.MaxAdt ?? 0),
                FloorMax = 0,
                FloorMin = 0,
                AreaMax = 0,
                AreaMin = 0,
                RoomQuantity = 0,
                BedInfo = string.Empty,
                Source = candidate.Room.Id,
                SourceType = 3,
                Status = 1,
                Author = "SystemBuilder",
                CreateTime = DateTime.Now
            };

            var preRoom = (OTARoomInfoPre)otaRoom;
            preRoom.IsCleanedValid =
                (candidate.Context.CN is null || _roomNameNormalizer.FormatComplianceCheck(candidate.Context.CN)) &&
                (candidate.Context.EN is null || _roomNameNormalizer.FormatComplianceCheck(candidate.Context.EN));

            candidates.Add(new SupplierRoomBuildCandidate
            {
                SupplierRoom = candidate.Room,
                RoomContext = candidate.Context.CN ?? candidate.Context.EN,
                DraftRoom = preRoom
            });
            otaRoomInfos.Add(otaRoom);

            if (candidate.Context.EN is not null && !string.IsNullOrWhiteSpace(candidate.Context.EN.RoomNameCleaned))
            {
                otaContexts.Add(candidate.Context.EN);
            }

            if (candidate.Context.CN is not null && !string.IsNullOrWhiteSpace(candidate.Context.CN.RoomNameCleaned))
            {
                otaContexts.Add(candidate.Context.CN);
            }
        }

        return new HotelRoomMatchResult
        {
            MTHotelCode = mtHotelCode,
            Candidates = candidates
        };
    }

    ISupplierRoomBuildPlugin? SelectPlugin(List<SupRoomInfo> supplierRooms)
    {
        return supplierRooms
            .Select(room => _pluginsBySupId.GetValueOrDefault(room.SupId))
            .Where(static plugin => plugin is not null)
            .Cast<ISupplierRoomBuildPlugin>()
            .OrderBy(plugin => plugin.Priority)
            .FirstOrDefault();
    }

    static bool HasMatchedOtaRoom(
        SupplierRoomContexts context,
        List<RoomContext> otaContexts,
        RoomMatch.Core.RoomMatchingStrategies.IRoomMatcher matcher)
    {
        if (context.EN is not null
            && !string.IsNullOrWhiteSpace(context.EN.RoomNameCleaned)
            && (context.EN.RoomNameCleaned.Length >= 100 || matcher.Match(context.EN, otaContexts).Any()))
        {
            return true;
        }

        if (context.CN is not null
            && !string.IsNullOrWhiteSpace(context.CN.RoomNameCleaned)
            && (context.CN.RoomNameCleaned.Length >= 100 || matcher.Match(context.CN, otaContexts).Any()))
        {
            return true;
        }

        return false;
    }

    IEnumerable<RoomContext?> BuildOtaContexts(OTARoomInfo room, RoomAnalysisSetting setting)
    {
        if (!string.IsNullOrWhiteSpace(room.RoomNameEN))
        {
            yield return _roomAnalyzer.Analysis(new RoomContext<OTARoomInfo>(room.RoomNameEN, room, setting));
        }

        if (!string.IsNullOrWhiteSpace(room.RoomNameCN) && string.IsNullOrWhiteSpace(room.RoomNameEN) != string.IsNullOrWhiteSpace(room.RoomNameCN))
        {
            yield return _roomAnalyzer.Analysis(new RoomContext<OTARoomInfo>(room.RoomNameCN, room, setting));
        }
    }

    SupplierRoomContexts BuildSupplierContext(string? roomNameCn, string? roomNameEn, RoomAnalysisSetting setting)
    {
        RoomContext? cn = null;
        RoomContext? en = null;

        if (!string.IsNullOrWhiteSpace(roomNameEn))
        {
            en = _roomAnalyzer.Analysis(new RoomContext(roomNameEn, setting));
        }

        if (!string.IsNullOrWhiteSpace(roomNameCn))
        {
            cn = roomNameEn == roomNameCn
                ? en
                : _roomAnalyzer.Analysis(new RoomContext(roomNameCn, setting));
        }

        return new SupplierRoomContexts(cn, en);
    }

    static bool CanCreateFromSupplierRoom(SupplierRoomContexts context)
    {
        var candidates = new[] { context.CN, context.EN }
            .Where(static item => item is not null)
            .Cast<RoomContext>()
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        return candidates.Any(static item =>
            item.RoomNameWords?.Count > 0 &&
            item.IsExcludedInOtaRoom != true &&
            item.IsUnknownChar != true);
    }

    async Task PersistAsync(List<OTARoomInfoPre> rooms, string outputMode, CancellationToken cancellationToken)
    {
        if (rooms.Count == 0)
        {
            return;
        }

        if (string.Equals(outputMode, "create", StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputMode, "post", StringComparison.OrdinalIgnoreCase))
        {
            await _roomCreateClient.PostAsync(rooms, cancellationToken);
            return;
        }

        if (string.Equals(outputMode, "bulk", StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputMode, "database", StringComparison.OrdinalIgnoreCase))
        {
            await _repository.BulkInsertOtaRoomInfoPresAsync(rooms, cancellationToken);
            return;
        }

        _logger.LogInformation("Skip persistence because output mode is {OutputMode}", outputMode);
    }

    sealed record SupplierRoomCandidate(SupRoomInfo Room, SupplierRoomContexts Context);
    sealed record SupplierRoomContexts(RoomContext? CN, RoomContext? EN);
}
