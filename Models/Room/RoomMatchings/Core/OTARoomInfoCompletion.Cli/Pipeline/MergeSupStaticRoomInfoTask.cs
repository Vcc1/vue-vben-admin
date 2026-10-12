using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sup.InterFace.SupBaseApi.EANEPS.Entitys.EanHotelInfo;
using System.Text.Json;
using System.Threading.Channels;

public sealed class MergeSupStaticRoomInfoTask
{
    private const string StageName = "merge_sup_static_room_info";
    private const string InputQueueName = "ean_hotels";
    private const string OutputQueueName = "merged_ean_hotels";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MergeSupStaticRoomInfoTask> _logger;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public MergeSupStaticRoomInfoTask(IServiceScopeFactory scopeFactory, ILogger<MergeSupStaticRoomInfoTask> logger, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<OtaRoomInfoCompletionWorkItem> eanHotelReader, ChannelWriter<OtaRoomInfoCompletionWorkItem> mergedEanHotelWriter, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in eanHotelReader.ReadAllAsync(cancellationToken))
            {
                _metrics.DecreaseQueueDepth(InputQueueName);
                _metrics.IncreaseRunningTasks(StageName);
                try
                {
                    await MergeAsync(item, cancellationToken);
                    await mergedEanHotelWriter.WriteAsync(item, cancellationToken);
                    _metrics.IncreaseQueueDepth(OutputQueueName);
                    _metrics.RecordCompletedTasks(StageName);
                }
                catch
                {
                    _metrics.RecordErrors(StageName);
                    throw;
                }
                finally
                {
                    _metrics.DecreaseRunningTasks(StageName);
                }
            }

            mergedEanHotelWriter.TryComplete();
        }
        catch (Exception ex)
        {
            mergedEanHotelWriter.TryComplete(ex);
            throw;
        }
    }

    private async Task MergeAsync(OtaRoomInfoCompletionWorkItem item, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        var existingRoomInfos = await dbContext.SupStaticRoomInfos
            .AsNoTracking()
            .Where(x => x.SupId == item.SupId && x.SHotelCode == item.SHotelCode)
            .ToListAsync(cancellationToken);

        var roomInfos = BuildRoomInfos(item);
        if (roomInfos.Count == 0)
        {
            item.SupStaticRooms = existingRoomInfos;
            return;
        }

        var existingKeys = existingRoomInfos
            .Select(CreateCompareKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newRoomInfos = roomInfos
            .Where(x => existingKeys.Add(CreateCompareKey(x)))
            .ToList();

        if (newRoomInfos.Count == 0)
        {
            item.SupStaticRooms = existingRoomInfos;
            return;
        }

        await dbContext.SupStaticRoomInfos.AddRangeAsync(newRoomInfos, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        item.SupStaticRooms = existingRoomInfos.Concat(newRoomInfos).ToList();
        _logger.LogInformation("写入 SupStaticRoomInfo 数据完成, MTHotelCode: {MTHotelCode}, InsertCount: {InsertCount}", item.MTHotelCode, newRoomInfos.Count);
    }

    private static List<SupStaticRoomInfo> BuildRoomInfos(OtaRoomInfoCompletionWorkItem item)
    {
        var roomInfos = new List<SupStaticRoomInfo>();
        var hotelInfo = item.HotelInfo;
        if (hotelInfo == null)
        {
            return roomInfos;
        }
        if (hotelInfo.Rooms == null || hotelInfo.Rooms.Count == 0)
        {
            return roomInfos;
        }

        foreach (var room in hotelInfo.Rooms.Values)
        {
            roomInfos.Add(new SupStaticRoomInfo
            {
                SHotelCode = item.SHotelCode,
                SupId = item.SupId,
                SRoomCode = room.Id?.Trim() ?? string.Empty,
                RoomNameEN = room.Name?.Trim() ?? string.Empty,
                BedInfo = BuildBedInfo(room),
                ViewInfo = BuildViewInfo(room),
                Area = BuildArea(room),
                CreateTime = DateTime.Now
            });
        }

        return roomInfos;
    }

    private static string BuildBedInfo(Room room)
    {
        if (room.BedGroups == null || room.BedGroups.Count == 0)
        {
            return string.Empty;
        }

        var bedInfos = room.BedGroups.Values
            .Select(BuildBedInfo)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(" | ", bedInfos);
    }

    private static string BuildBedInfo(BedGroup bedGroup)
    {
        //if (!string.IsNullOrWhiteSpace(bedGroup.Description))
        //{
        //    return bedGroup.Description.Trim();
        //}

        if (bedGroup.Configuration == null || bedGroup.Configuration.Length == 0)
        {
            return string.Empty;
        }

        var configurations = bedGroup.Configuration
            .Select(x => $"{x.Quantity} {x.Size} {x.Type}".Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" + ", configurations);
    }

    private static string BuildViewInfo(Room room)
    {
        if (room.Views == null || room.Views.Count == 0)
        {
            return string.Empty;
        }

        var viewInfos = room.Views.Values
            .Select(x => x.Name?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        return string.Join(" | ", viewInfos);
    }

    private static string BuildArea(Room room)
    {
        if (room.Area == null)
        {
            return string.Empty;
        }

        decimal? squareMeters = room.Area.SquareMeters;
        decimal? squareFeet = room.Area.SquareFeet;

        if (squareMeters == null && squareFeet == null)
        {
            return string.Empty;
        }

        if (squareMeters == null && squareFeet is decimal feetValue)
        {
            squareMeters = feetValue * 0.092903m;
        }

        if (squareFeet == null && squareMeters is decimal meterValue)
        {
            squareFeet = meterValue / 0.092903m;
        }

        return JsonSerializer.Serialize(new
        {
            squareMeters = decimal.Round(squareMeters!.Value, 2),
            squareFeet = decimal.Round(squareFeet!.Value, 2)
        });
    }

    private static string CreateCompareKey(SupStaticRoomInfo roomInfo)
    {
        return $"{Normalize(roomInfo.RoomNameEN)}||||{Normalize(roomInfo.BedInfo)}||||{Normalize(roomInfo.ViewInfo)}";
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
