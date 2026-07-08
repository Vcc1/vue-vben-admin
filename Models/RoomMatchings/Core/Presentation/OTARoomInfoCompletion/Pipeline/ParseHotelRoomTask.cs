using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

public sealed class ParseHotelRoomTask
{
    private const string StageName = "parse_hotel_rooms";
    private const string InputQueueName = "hotel_room_data";
    private const string OutputQueueName = "parsed_hotels";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParseHotelRoomTask> _logger;
    private readonly OtaRoomInfoCompletionOptions _options;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public ParseHotelRoomTask(IServiceScopeFactory scopeFactory, ILogger<ParseHotelRoomTask> logger, OtaRoomInfoCompletionOptions options, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<OtaRoomInfoCompletionWorkItem> hotelRoomDataReader, ChannelWriter<OtaRoomInfoCompletionWorkItem> parsedHotelWriter, CancellationToken cancellationToken)
    {
        var runningTasks = new List<Task>();

        try
        {
            await foreach (var item in hotelRoomDataReader.ReadAllAsync(cancellationToken))
            {
                _metrics.DecreaseQueueDepth(InputQueueName);
                _metrics.IncreaseRunningTasks(StageName);
                runningTasks.Add(ProcessItemWithMetricsAsync(item, parsedHotelWriter, cancellationToken));
                if (runningTasks.Count < _options.ParseHotelConcurrency)
                {
                    continue;
                }

                var completedTask = await Task.WhenAny(runningTasks);
                runningTasks.Remove(completedTask);
                await completedTask;
            }

            await WaitAllAsync(runningTasks);
            parsedHotelWriter.TryComplete();
        }
        catch (Exception ex)
        {
            parsedHotelWriter.TryComplete(ex);
            throw;
        }
    }

    private async Task ProcessItemWithMetricsAsync(OtaRoomInfoCompletionWorkItem item, ChannelWriter<OtaRoomInfoCompletionWorkItem> parsedHotelWriter, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessItemAsync(item, parsedHotelWriter, cancellationToken);
            _metrics.RecordCompletedTasks(StageName);
        }
        catch (Exception e)
        {
            _metrics.RecordErrors(StageName);
            var errorCount = _metrics.GetErrorSnapshot().TryGetValue(StageName, out var currentErrorCount)
                ? currentErrorCount
                : 0;
            _logger.LogError(e, "解析酒店房型异常已抑制, Stage: {StageName}, MTHotelCode: {MTHotelCode}, ErrorCount: {ErrorCount}", StageName, item.MTHotelCode, errorCount);
            //throw;
        }
        finally
        {
            _metrics.DecreaseRunningTasks(StageName);
        }
    }

    private async Task ProcessItemAsync(OtaRoomInfoCompletionWorkItem model, ChannelWriter<OtaRoomInfoCompletionWorkItem> parsedHotelWriter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("解析阶段完成占位处理, MTHotelCode: {MTHotelCode}", model.MTHotelCode);
        var setting = new RoomAnalysisSetting() { CleanFormatType = CleanFormatType.BASIC_NORMALIZE, IsNumberEscape = true, IsUnknownWordSplitBySpace = true, WordDictSource = WordDictSource.Official };
        using var scope = _scopeFactory.CreateScope();
        var roomAnalyzer = scope.ServiceProvider.GetRequiredService<IRoomAnalyzer>();

        var supStaticRooms = model.SupStaticRooms.GroupBy(e => e.RoomNameEN).Where(e => e.Count() == 1).Select(e => new RoomContext<SupStaticRoomInfo>(e.First().RoomNameEN, e.First(), setting)).ToList();
        foreach (var supContext in supStaticRooms)
        {
            roomAnalyzer.Analysis(supContext);
        }
        supStaticRooms = supStaticRooms.Where(e => !string.IsNullOrEmpty(e.RoomNameCleaned)).GroupBy(e => e.RoomNameCleaned).Where(g => g.Count() == 1).Select(e => e.First()).ToList();

        foreach (var otaRoom in model.OtaRooms)
        {
            string otaRoomName = string.IsNullOrWhiteSpace(otaRoom.RoomNameEN) ? otaRoom.RoomNameCN : otaRoom.RoomNameEN;
            var otaContext = new RoomContext<OTARoomInfo>(otaRoomName, otaRoom, setting);
            roomAnalyzer.Analysis(otaContext);
            var supStaticRoom = supStaticRooms.FirstOrDefault(e => e.RoomNameCleaned == otaContext.RoomNameCleaned);

            if (supStaticRoom == null)
            {
                continue;
            }

            var viewInfo = supStaticRoom.SourceData.ViewInfo;
            var area = supStaticRoom.SourceData.Area;
            var areaModel = ParseArea(area);
            var bedInfo = supStaticRoom.SourceData.BedInfo;
            var source = new { SupId = 51, SRoomCode = supStaticRoom.SourceData.SRoomCode }.ToJson();

            var viewInfoContext = new RoomContext(viewInfo, setting);
            roomAnalyzer.Analysis(viewInfoContext);
            //var bedInfoContext = new RoomContext(bedInfo, setting);
            //roomAnalyzer.Analysis(bedInfoContext);

            var externalView = TryBuildExternalView(otaContext, viewInfoContext, viewInfo, model.MTHotelCode, otaRoom.Id, otaRoomName);
            var externalBedInfo = TryBuildExternalBedInfo(otaContext, bedInfo);
            var externalArea = TryBuildExternalArea(otaContext, areaModel, area);




            model.OTARoomInfoExtensions.Add(new OTARoomInfoExtension
            {
                Id = otaRoom.Id,
                MTHotelCode = model.MTHotelCode,
                Source = source,
                InternalBedInfo = bedInfo,
                ExternalBedInfo = externalBedInfo,
                InternalArea = area,
                ExternalArea = externalArea,
                InternalView = viewInfo,
                ExternalView = externalView
            });
        }

        await parsedHotelWriter.WriteAsync(model, cancellationToken);
        _metrics.IncreaseQueueDepth(OutputQueueName);
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static OtaRoomInfoCompletionArea? ParseArea(string? area)
    {
        var normalizedArea = NormalizeValue(area);
        if (normalizedArea == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OtaRoomInfoCompletionArea>(normalizedArea, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string TryBuildExternalArea(RoomContext otaContext, OtaRoomInfoCompletionArea? areaModel, string? area)
    {
        if (areaModel == null || string.IsNullOrWhiteSpace(area))
        {
            return string.Empty;
        }

        var areaValues = new[] { areaModel.SquareMeters, areaModel.SquareFeet }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        if (areaValues.Count == 0)
        {
            return string.Empty;
        }

        var otaNumbers = ExtractNumbers(otaContext.RoomNameOriginal);
        if (otaNumbers.Any(otaNumber => areaValues.Any(areaValue => otaNumber >= areaValue - 2 && otaNumber <= areaValue + 2)))
        {
            return area;
        }

        return string.Empty;
    }

    private static string TryBuildExternalBedInfo(RoomContext otaContext, string? bedInfo)
    {
        if (otaContext == null || string.IsNullOrWhiteSpace(bedInfo))
        {
            return string.Empty;
        }

        var parsedBedInfo = ParseBedInfo(bedInfo);
        if (parsedBedInfo.OrGroups.Count == 0)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static OtaRoomInfoCompletionBedInfo ParseBedInfo(string bedInfo)
    {
        var result = new OtaRoomInfoCompletionBedInfo();
        foreach (var orPart in bedInfo.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var orGroup = new OtaRoomInfoCompletionBedOrGroup();
            foreach (var andPart in orPart.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var item = ParseBedInfoItem(andPart);
                if (item != null)
                {
                    orGroup.AndItems.Add(item);
                }
            }

            if (orGroup.AndItems.Count > 0)
            {
                result.OrGroups.Add(orGroup);
            }
        }

        return result;
    }

    private static OtaRoomInfoCompletionBedAndItem? ParseBedInfoItem(string text)
    {
        var normalizedText = NormalizeValue(text);
        if (normalizedText == null)
        {
            return null;
        }

        var parts = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        return new OtaRoomInfoCompletionBedAndItem
        {
            RawText = normalizedText,
            CountText = parts.ElementAtOrDefault(0),
            BedSizeType = parts.ElementAtOrDefault(1),
            BedType = parts.Length <= 2 ? parts.ElementAtOrDefault(2) : string.Join(' ', parts.Skip(2))
        };
    }

    private string TryBuildExternalView(RoomContext otaContext, RoomContext cViewContext, string viewInfo, string mtHotelCode, int otaRoomId, string otaRoomName)
    {
        var otaRoomNameView = otaContext.RoomNameWords.Where(e => e.WordAttr != null && e.WordAttr.WordTypeId == 6);
        var cView = cViewContext.RoomNameWords.Where(e => e.WordAttr != null && e.WordAttr.WordTypeId != 22);
        var cViewIds = CollectViewWordIds(cView);
        if (cViewIds.Count == 0)
        {
            return string.Empty;
        }
        var otaRoomNameViewIds = CollectViewWordIds(otaRoomNameView);

        if (!cViewIds.IsSubsetOf(otaRoomNameViewIds))
        {
            return string.Empty;
        }
        if (otaRoomNameViewIds.Count > cViewIds.Count)
        {
            _logger.LogWarning("景观未写入ExternalView, MTHotelCode: {MTHotelCode}, OTARoomId: {OTARoomId}, OtaRoomName: {OtaRoomName}, CView: {CView}, Reason: {Reason}", mtHotelCode, otaRoomId, otaRoomName, viewInfo, $"otaRoomNameView分组数量与cview不一致, otaRoomNameViewGroupCount: {otaRoomNameViewIds.Count}, cViewGroupCount: {cViewIds.Count}, otaRoomNameView: {FormatViewWordIds(otaRoomNameViewIds)}, cView: {FormatViewWordIds(cViewIds)}");
            return string.Empty;
        }


        return viewInfo;
    }

    private static HashSet<int> CollectViewWordIds(IEnumerable<RoomWord> words)
    {
        return words
            .Where(x => x.WordAttr != null && x.WordAttr.Id > 0 && !string.IsNullOrWhiteSpace(x.WordText))
            .Select(x => x.WordAttr.Id)
            .ToHashSet();
    }

    private static string FormatViewWordIds(HashSet<int> ids)
    {
        return string.Join(",", ids.OrderBy(x => x));
    }

    private static List<decimal> ExtractNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<decimal>();
        }

        return Regex.Matches(text, @"\d+(?:\.\d+)?")
            .Select(x => decimal.TryParse(x.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? (decimal?)value : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }

    private static async Task WaitAllAsync(List<Task> runningTasks)
    {
        while (runningTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(runningTasks);
            runningTasks.Remove(completedTask);
            await completedTask;
        }
    }
}
