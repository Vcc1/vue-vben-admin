using Infrastructure.Sql.MengTuYDOTA;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class RoomMappingSupStaticRoomInfoStatisticsWorker : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly ILogger<RoomMappingSupStaticRoomInfoStatisticsWorker> logger;

    public RoomMappingSupStaticRoomInfoStatisticsWorker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<RoomMappingSupStaticRoomInfoStatisticsWorker> logger)
    {
        this.serviceProvider = serviceProvider;
        this.hostApplicationLifetime = hostApplicationLifetime;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();

            var viewInfoGroups = await dbContext.SupStaticRoomInfos
                .Where(x => !string.IsNullOrWhiteSpace(x.ViewInfo))
                .GroupBy(x => x.ViewInfo)
                .Select(x => x.Key)
                .ToListAsync(stoppingToken);

            var viewInfoList = viewInfoGroups
                .SelectMany(SplitByPipe)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var bedInfoGroups = await dbContext.SupStaticRoomInfos
                .Where(x => !string.IsNullOrWhiteSpace(x.BedInfo))
                .GroupBy(x => x.BedInfo)
                .Select(x => x.Key)
                .ToListAsync(stoppingToken);

            var bedInfoList = bedInfoGroups
                .SelectMany(ParseBedInfo)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var outputDirectory = AppContext.BaseDirectory;
            var viewInfoFilePath = Path.Combine(outputDirectory, "SupStaticRoomInfo_ViewInfo.txt");
            var bedInfoFilePath = Path.Combine(outputDirectory, "SupStaticRoomInfo_BedInfo.txt");

            await File.WriteAllLinesAsync(viewInfoFilePath, viewInfoList, stoppingToken);
            await File.WriteAllLinesAsync(bedInfoFilePath, bedInfoList, stoppingToken);

            logger.LogInformation("ViewInfo 写入完成: {FilePath}, 共 {Count} 条", viewInfoFilePath, viewInfoList.Count);
            logger.LogInformation("BedInfo 写入完成: {FilePath}, 共 {Count} 条", bedInfoFilePath, bedInfoList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "统计 SupStaticRoomInfo 数据失败");
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }

    private static IEnumerable<string> SplitByPipe(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static IEnumerable<string> ParseBedInfo(string value)
    {
        foreach (var item in SplitByPipe(value))
        {
            var parts = item
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(1)
                .Take(2)
                .ToArray();

            if (parts.Length == 0)
            {
                continue;
            }

            var normalized = string.Join(' ', parts).Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }
}
