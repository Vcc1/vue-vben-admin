using Cronos;
using EFCore.BulkExtensions;
using Infrastructure.Redis.SupRoomInfo;
using Infrastructure.Redis.SupRoomInfo.Entitys;
using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.MengTuYDOTA;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Index.HPRtree;
using OpenTelemetry;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomMatchingStrategies;
using RoomMatch.Core.RoomNameNormalizer;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using VBaseFramework.Core.IOC;

[ConfigSection("RoomCreate", Reloadable = true, ValidateDataAnnotations = true, ValidateOnStart = true)]
public class RoomCreateOptions
{
    public string Url { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
}
public class Worker : BackgroundService
{
    readonly ILogger<Worker> _logger;
    readonly SupRoomInfoRedisToSqlMetrics _metrics;
    IServiceProvider serviceProvider { get; set; }
    readonly SupRoomInfoDBContext _redis;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger, SupRoomInfoDBContext redis, SupRoomInfoRedisToSqlMetrics metrics)
    {
        this.serviceProvider = serviceProvider;
        this._logger = logger;
        _redis = redis;
        _metrics = metrics;
    }

    static readonly Channel<TempSupRoomInfo2> channel =
    Channel.CreateBounded<TempSupRoomInfo2>(new BoundedChannelOptions(100_00)
    {
        FullMode = BoundedChannelFullMode.Wait,   // 队列满时写端会等待，不会丢
        SingleWriter = true,
        SingleReader = true
    });
    public async Task ExeAddOrUpdateSupRoomInfo()
    {
        var batchSize = 1000;
        var batchList = new List<TempSupRoomInfo2>(batchSize);
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<RoomMappingDbContext>();

        while (await channel.Reader.WaitToReadAsync())
        {
            batchList.Clear();
            // 一次读batchSize条
            while (batchList.Count < batchSize && channel.Reader.TryRead(out var item))
            {
                batchList.Add(item);
            }

            if (batchList.Count == 0)
                continue;
            await WriteDB(batchList, default);

        }
    }

    private async Task<int> WriteDB(List<TempSupRoomInfo2> batchList, CancellationToken ct)
    {
        using var writeDbScope = _metrics.MeasureWriteDb();
        var insertedCount = 0;

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();


        //var json = File.ReadAllText("C:/1/1.txt.txt");
        //batchList = json.FromJson<List<TempSupRoomInfo2>>(); //JsonSerializer.Deserialize<List<TempSupRoomInfo2>>(json);

        //var jsons = File.ReadAllLines("C:/1/3.txt");
        //batchList = jsons.Where(e => !string.IsNullOrEmpty(e)).Select(value =>
        //{
        //    var e = JsonSerializer.Deserialize<SupRoomModel>(value!);
        //    if (e != null)
        //    {
        //        return new TempSupRoomInfo2()
        //        {
        //            AcreageFrom = e.AcreageFrom,
        //            AcreageTo = e.AcreageTo,
        //            BedInfo = e.BedInfo,
        //            BedInfoKey = e.BedInfoKey,
        //            MaxAdt = e.MaxAdt,
        //            MaxChd = null,
        //            SRoomCode = e.RoomCode,
        //            SRoomNameCN = e.RoomName,
        //            SRoomNameEN = e.RoomEnName,
        //            MTHotelCode = e.MTHotelCode,
        //            SmokeType = e.SmokeType,
        //            SHotelCode = e.HotelCode,
        //            SupId = e.SupId,
        //            Status = 1,
        //            CreaterBy = "System:RedisToSql",
        //            CreaterDate = DateTime.Now,
        //        };
        //    }
        //    return null;
        //}).ToList(); //JsonSerializer.Deserialize<List<TempSupRoomInfo2>>(json);



        var deduped = batchList
                        .GroupBy(x => new
                        {
                            x.SupId,
                            x.SHotelCode,
                            x.SRoomNameCN,
                            x.SRoomNameEN,
                            x.BedInfoKey,
                            x.BedInfo,
                            x.SmokeType,
                            x.AcreageFrom,
                            x.AcreageTo,
                        })
                        .Select(g => g.Last())
                        .ToList();

        // 批量插入到临时表
        try
        {
            var noList = deduped.Where(e => e.SRoomNameCN.Length >= 200 || e.SRoomNameEN.Length >= 200 || e.SRoomCode.Length >= 200).ToList();
            if (noList.Count > 0)
            {
                for (var i = 0; i < noList.Count; i++)
                {
                }
                //写超出日志
            }
            deduped.ForEach(e =>
            {
                e.SRoomCode = e.SRoomCode ?? "";
                e.BedInfo = e.BedInfo ?? "[]";

            });
            foreach (var item in deduped.Where(e => e.SRoomNameCN.Length < 200 && e.SRoomNameEN.Length < 200 && e.SRoomCode.Length < 200).ToList())
            {
                try
                {
                    await dbContext.BulkInsertAsync(new List<TempSupRoomInfo2>() { item }, cancellationToken: ct);
                    insertedCount++;
                }
                catch (Exception)
                {
                    _metrics.RecordError("temp_insert_item");
                }

            }

        }
        catch (Exception e)
        {
            _metrics.RecordError("temp_insert_batch");
            _logger.LogError(e, $"插入表TempSupRoomInfo2表时出现异常 {deduped.ToJson()}");
        }

        try
        {
            // 调用存储过程
            using var syncScope = _metrics.MeasureSyncStoredProcedure();
            await dbContext.Database.ExecuteSqlRawAsync("EXEC SyncSupRoomInfoFromTemp2", ct);
        }
        catch (Exception e)
        {
            _metrics.RecordError("sync_stored_procedure");
            _logger.LogError(e, "执行SyncSupRoomInfoFromTemp存储过程时出现异常 ");
        }

        try
        {
            using var cleanupScope = _metrics.MeasureTempTableCleanup();
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE TempSupRoomInfo2", ct);
        }
        catch (Exception e)
        {
            _metrics.RecordError("temp_table_cleanup");
            _logger.LogError(e, "执行TempSupRoomInfo表清理时出现异常");
        }

        return insertedCount;
    }




    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ThreadPool.SetMinThreads(16, 16);
        _logger.LogInformation($"CPU 核心数: {Environment.ProcessorCount}");
        _logger.LogInformation($"匹配开始时间: {DateTime.Now.ToString("HH:mm:ss")}");

        var task1 = Producer(stoppingToken);
        var task2 = Consumer(stoppingToken);
        await Task.WhenAll(task1, task2);

    }

    async Task Producer(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            List<TempSupRoomInfo2> list;
            using (_metrics.MeasureRedisFetch())
            {
                list = _redis.GetSupRoomInfo(1000, ct);
            }

            _metrics.RecordFetched(list.Count);
            // 不再丢弃：写满则等待
            foreach (var item in list)
            {
                await channel.Writer.WriteAsync(item, ct);
                _metrics.IncreaseQueueDepth();
            }
            if (list.Count < 1000)
            {
                try
                {
                    await Task.Delay(2000, ct);
                }
                catch { }
            }
        }
        channel.Writer.TryComplete();

    }
    async Task Consumer(CancellationToken ct)
    {
        const int batchSize = 1000;
        var batch = new List<TempSupRoomInfo2>(capacity: batchSize);
        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                _metrics.DecreaseQueueDepth();
                batch.Add(item);
                if (batch.Count >= batchSize)
                {
                    var insertedCount = await WriteDB(batch, ct);
                    _metrics.RecordBatch(batch.Count, insertedCount);
                    batch.Clear();
                }
            }
            catch (Exception e)
            {
                _metrics.RecordError("consumer_dispatch");
                _logger.LogError(e, "消费任务调度严重异常");
            }
        }
        if (batch.Count > 0)
        {
            var insertedCount = await WriteDB(batch, ct);
            _metrics.RecordBatch(batch.Count, insertedCount);
        }

        //while (!ct.IsCancellationRequested)
        //{
        //    try
        //    {
        //        var batch = new List<TempSupRoomInfo2>(capacity: batchSize);
        //        // 阻塞直到凑满 batchSize 条，或被取消/通道关闭
        //        for (int i = 0; i < batchSize; i++)
        //        {
        //            TempSupRoomInfo2 item;
        //            item = await channel.Reader.ReadAsync(ct); // 通道空则等待
        //            batch.Add(item);
        //        }
        //        // 走到这里一定是满批次
        //        await WriteDB(batch, ct);
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.LogError(e, "消费任务调度严重异常");
        //    }
        //}
    }
}
