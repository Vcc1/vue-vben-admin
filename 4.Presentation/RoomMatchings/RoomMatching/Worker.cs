using Cronos;
using Infrastructure.Redis.SupRoomInfo;
using Infrastructure.Redis.SupRoomInfo.Entitys;
using Infrastructure.Sql.MengTuAPI;
using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.MixedData;
using Infrastructure.Sql.MixedData.Entitys;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoomMatch.Core.RoomAnalysis;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Channels;
using Infrastructure.Sql.RoomMapping.Entitys;
using Infrastructure.Sql.MengTuSup;
using Infrastructures.RoomMatchingRedis;
using Infrastructures.RoomMatchingRedis.Entitys;
using OTADomain.Matchings;
using Infrastructure.Sql.RoomMapping;
using System.Linq;

class DataPair
{
    public string MTHotelCode { get; set; }
    public List<OTARoomInfo> MRooms { get; set; }
    public List<SupRoomInfoView> CRooms { get; set; }
}
public class Worker : BackgroundService
{
    private const int ReadHotelBatchSize = 100;
    private const int WriteHotelBatchSize = 100;

    IServiceProvider serviceProvider { get; set; }
    IRoomAnalyzer roomAnalysis { get; set; }
    ILogger<Worker> logger { get; set; }
    public Worker(IServiceProvider serviceProvider, IRoomAnalyzer roomAnalysis, ILogger<Worker> logger)
    {
        this.serviceProvider = serviceProvider;
        this.roomAnalysis = roomAnalysis;
        this.logger = logger;
    }
    /// <summary>
    /// 待匹配处理数据队列
    /// </summary>
    Channel<DataPair> readQueue = Channel.CreateBounded<DataPair>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = false
    });

    /// <summary>
    /// 待写入处理数据队列
    /// </summary>
    Channel<List<RoomMatching>> writeQueue = Channel.CreateBounded<List<RoomMatching>>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = false,
        SingleReader = true
    });

    /// <summary>
    /// 循环匹配
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ThreadPool.SetMinThreads(16, 16);
        logger.LogInformation($"CPU 核心数: {Environment.ProcessorCount}");
        logger.LogInformation($"匹配开始时间: {DateTime.Now.ToString("HH:mm:ss")}");
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        var cronExpression = CronExpression.Parse("0 */10 * * * *", CronFormat.IncludeSeconds); // 每10分钟整点
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("匹配任务开始执行");
                QueueReload();
                var readTask = ReadData(stoppingToken);
                var exeTask = ExeDataAsync(stoppingToken);
                //入库：按 batch 操作
                await BulkInsertData(stoppingToken);
                await Task.WhenAll(readTask, exeTask);
                logger.LogInformation("匹配任务执行完成");
            }
            catch (Exception e)
            {
                logger.LogError(e, $"匹配任务严重异常退出");
            }

            await Task.Delay(cronExpression.GetDelayMilliseconds(), stoppingToken);

        }
    }

    private void QueueReload()
    {
        #region 初始化
        readQueue = Channel.CreateBounded<DataPair>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        /// <summary>
        /// 待写入处理数据队列
        /// </summary>
        writeQueue = Channel.CreateBounded<List<RoomMatching>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        });
        #endregion
    }

    private async Task ReadData(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var roomMappingDbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        var stopwatch = new Stopwatch();
        // 计时：读取数据耗时
        stopwatch.Start();

        var mtHotelCodeList = await roomMappingDbContext.OTARoomInfos
            .GroupBy(e => e.MTHotelCode)
            .Select(e => e.Key)
            .ToListAsync(stoppingToken);

        //var onlineHotelCodes = await MengTuAPIContext.Database
        //    .SqlQuery<string>($"SELECT MTHotelCode FROM dbo.V_OTAOnlineHotel")
        //    .ToListAsync(stoppingToken);

        //if (onlineHotelCodes is { Count: > 0 })
        //{
        //    mtHotelCodeList.AddRange(onlineHotelCodes);
        //}

        mtHotelCodeList = mtHotelCodeList
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            //.Where(e => e == "MT-051339")
            .ToList();


        stopwatch.Stop();
        logger.LogTrace($"读取MTHotelCode数据耗时: {stopwatch.ElapsedMilliseconds} ms");
        try
        {
            var page = 0;
            foreach (var batch in mtHotelCodeList.Chunk(ReadHotelBatchSize))
            {
                stopwatch.Restart();
                List<OTARoomInfo> mrooms = null;
                mrooms = await GetOTARoomInfo(roomMappingDbContext, stopwatch, batch, mrooms);
                if (mrooms == null)
                {
                    logger.LogError("数据读取失败 GetOTARoomInfo");
                    continue;
                }

                List<SupRoomInfoView> crooms = null;
                crooms = await GetSupRoomInfo(roomMappingDbContext, batch, crooms);
                if (crooms == null)
                {
                    logger.LogError("数据读取失败 GetSupRoomInfo");
                    continue;
                }

                var mroomDic = mrooms.GroupBy(e => e.MTHotelCode).ToDictionary(e => e.Key, e => e.ToList());
                var croomDic = crooms.GroupBy(e => e.MTHotelCode).ToDictionary(e => e.Key, e => e.ToList());

                foreach (var code in batch)
                {
                    var pair = new DataPair
                    {
                        MTHotelCode = code,
                        MRooms = mroomDic.GetValueOrDefault(code) ?? new List<OTARoomInfo>(),
                        CRooms = croomDic.GetValueOrDefault(code) ?? new List<SupRoomInfoView>(),
                    };
                    await readQueue.Writer.WriteAsync(pair, stoppingToken);
                }

                stopwatch.Stop();
                logger.LogTrace($"读取第{page++}批酒店房型数据耗时: {stopwatch.ElapsedMilliseconds} ms，本批酒店数: {batch.Length}");
            }
            logger.LogTrace($"房型数据读取全部完成");
            readQueue.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            readQueue.Writer.TryComplete(ex);
            throw;
        }
    }

    private async Task ExeDataAsync(CancellationToken stoppingToken)
    {
        var index = 0;
        var tasks = new List<Task>();
        for (int i = 0; i < 8; i++)
        {
            var task = Task.Run(async () =>
            {
                while (await readQueue.Reader.WaitToReadAsync(stoppingToken))
                {
                    if (!readQueue.Reader.TryRead(out var pair))
                    {
                        continue;
                    }

                    var estimatedCount = pair.CRooms?.Count ?? 0;
                    List<RoomMatching> roomMatchings = estimatedCount > 0
                        ? new List<RoomMatching>(estimatedCount)
                        : new List<RoomMatching>();
                    var idx = Interlocked.Increment(ref index);
                    var sw = Stopwatch.StartNew();
                    logger.LogTrace($"线程 {Thread.CurrentThread.ManagedThreadId} 第 {idx} 个酒店匹配开始，酒店:{pair.MTHotelCode} 读取队列剩余:{readQueue.Reader.Count} 写入队列剩余:{writeQueue.Reader.Count}");
                    var result = RoomMatch.Core.RoomMatch.RoomMatch_A.MatchRoom(pair.MRooms, pair.CRooms, roomAnalysis);
                    var mRoomById = pair.MRooms.Count > 0
                        ? pair.MRooms.ToDictionary(e => e.Id)
                        : null;
                    foreach (var item in result)
                    {
                        if (!string.IsNullOrEmpty(item.CRoom.CreateBy))
                        {
                            var mroom = item.CRoom.OTARoomId.HasValue && mRoomById != null && mRoomById.TryGetValue(item.CRoom.OTARoomId.Value, out var manualMatchedRoom)
                                ? manualMatchedRoom
                                : null;
                            roomMatchings.Add(new RoomMatching()
                            {
                                MTHoteCode = item.CRoom.MTHotelCode,
                                CRoom = item.CRoom,
                                MRoom = mroom,
                                CRoomId = item.CRoom.Id,
                                MRoomId = mroom?.Id,
                                RoomMatchingStatus = RoomMatchStatus.TextMatch,
                                SupId = item.CRoom.SupId,
                                Laval = item.Lavel
                            });
                        }
                        else
                        {
                            roomMatchings.Add(new RoomMatching()
                            {
                                MTHoteCode = item.CRoom.MTHotelCode,
                                CRoom = item.CRoom,
                                MRoom = item.MRoom,
                                CRoomId = item.CRoom.Id,
                                MRoomId = item.MRoom?.Id,
                                RoomMatchingStatus = item.RoomMatchStatus,
                                SupId = item.CRoom.SupId,
                                Laval = item.Lavel
                            });
                        }
                    }
                    await writeQueue.Writer.WriteAsync(roomMatchings, stoppingToken);
                    logger.LogTrace($"线程 {Thread.CurrentThread.ManagedThreadId} 第 {idx} 个酒店匹配完成，酒店:{pair.MTHotelCode} 房型数:{roomMatchings.Count} 用时:{sw.ElapsedMilliseconds} ms 读取队列剩余:{readQueue.Reader.Count} 写入队列剩余:{writeQueue.Reader.Count}");
                }

            });
            tasks.Add(task);
        }
        try
        {
            await Task.WhenAll(tasks);
            logger.LogTrace($"匹配完成");
            writeQueue.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            writeQueue.Writer.TryComplete(ex);
            throw;
        }
    }

    private async Task BulkInsertData(CancellationToken stoppingToken)
    {
        var index = 0;
        while (await writeQueue.Reader.WaitToReadAsync(stoppingToken))
        {
            var hotelBuffers = new List<List<RoomMatching>>(WriteHotelBatchSize);
            hotelBuffers.Add(await writeQueue.Reader.ReadAsync(stoppingToken));
            while (hotelBuffers.Count < WriteHotelBatchSize && writeQueue.Reader.TryRead(out var hotelBuffer))
            {
                hotelBuffers.Add(hotelBuffer);
            }

            var totalRoomCount = hotelBuffers.Sum(e => e.Count);
            var buffer = totalRoomCount > 0 ? new List<RoomMatching>(totalRoomCount) : new List<RoomMatching>();
            foreach (var hotelBuffer in hotelBuffers)
            {
                buffer.AddRange(hotelBuffer);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            await SaveDataAsync(buffer, stoppingToken);
            stopwatch.Stop();
            logger.LogTrace($"第{(index++)}批写入完成，酒店数:{hotelBuffers.Count} 房型数:{buffer.Count} 耗时:{stopwatch.ElapsedMilliseconds} ms");
        }
        logger.LogTrace($"匹配结束时间: {DateTime.Now.ToString("HH:mm:ss")}");

    }

    private async Task SaveDataAsync(List<RoomMatching> list, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
            var persistedList = list.Where(e => string.IsNullOrEmpty(e.CRoom.CreateBy)).ToList();
            if (persistedList.Count == 0)
            {
                return;
            }

            var conn = (SqlConnection)context.Database.GetDbConnection();
            await conn.OpenAsync(stoppingToken);

            using var transaction = conn.BeginTransaction();

            // 1. 创建临时表
            var createTempSql = @"
                 CREATE TABLE #TempRoomMapping (
                    SupRoomId INT NOT NULL PRIMARY KEY CLUSTERED,
                    OTARoomId INT NULL,
                    MTHotelCode varchar(64)  NULL,
                    Status INT NOT NULL,
                    MatchType INT NOT NULL,
                    MatchScore DECIMAL(5, 4) NOT NULL
        );";
            using (var cmd = new SqlCommand(createTempSql, conn, transaction))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. 写入临时表
            using (var bulkCopy = new SqlBulkCopy(conn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#TempRoomMapping";
                bulkCopy.BatchSize = persistedList.Count;
                bulkCopy.BulkCopyTimeout = 0;

                var table = new DataTable();
                table.Columns.Add("SupRoomId", typeof(int));
                table.Columns.Add("OTARoomId", typeof(int));
                table.Columns.Add("MTHotelCode", typeof(string));
                table.Columns.Add("Status", typeof(int));
                table.Columns.Add("MatchType", typeof(int));
                table.Columns.Add("MatchScore", typeof(decimal));  // Nullable

                table.BeginLoadData();
                foreach (var item in persistedList)
                {
                    var row = table.NewRow();
                    row["SupRoomId"] = item.CRoomId;
                    row["OTARoomId"] = item.MRoomId.HasValue ? (object)item.MRoomId.Value : DBNull.Value;
                    row["MTHotelCode"] = item.MRoom?.MTHotelCode ?? (object)DBNull.Value;
                    row["Status"] = item.RoomMatchingStatus == RoomMatchStatus.TextMatch ? 1 : 0;
                    row["MatchType"] = item.RoomMatchingStatus;
                    row["MatchScore"] = item.RoomMatchingScore;
                    table.Rows.Add(row);
                }
                table.EndLoadData();

                await bulkCopy.WriteToServerAsync(table);
            }


            var sql = @"
                       MERGE INTO RoomMap AS target
                            USING (
                                SELECT * FROM #TempRoomMapping WHERE OTARoomId IS NOT NULL
                            ) AS source
                            ON target.SupRoomId = source.SupRoomId
                            WHEN MATCHED AND (target.CreateBy IS NULL OR target.CreateBy = '') 
                                          AND (
                                                ISNULL(target.OTARoomId, -1) <> ISNULL(source.OTARoomId, -1)
                                                OR target.Status <> source.Status
                                                OR ISNULL(target.MatchType, -1) <> source.MatchType
                                                OR ISNULL(target.MatchScore, -1) <> source.MatchScore
                                              )
                            THEN
                                UPDATE SET 
                                    target.OTARoomId = source.OTARoomId,
                                    target.Status = source.Status,
                                    target.MatchType = source.MatchType,
                                    target.MatchScore = source.MatchScore,
                                    target.MTHotelCode = source.MTHotelCode,
                                    target.CreateDate = GETDATE()
                            WHEN NOT MATCHED BY TARGET THEN
                                INSERT (SupRoomId, OTARoomId, Status, MatchType, MatchScore, MTHotelCode, CreateDate)
                                VALUES (source.SupRoomId, source.OTARoomId, source.Status, source.MatchType, source.MatchScore, source.MTHotelCode, GETDATE());

                            DELETE target
                            FROM RoomMap target
                            INNER JOIN #TempRoomMapping temp ON temp.SupRoomId = target.SupRoomId
                            WHERE temp.OTARoomId IS NULL AND (target.CreateBy IS NULL OR target.CreateBy = '');
                                            ";



            using (var cmd = new SqlCommand(sql, conn, transaction))
            {
                await cmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "写入数据时发生错误");
            //throw;
        }
    }

    private async Task<List<OTARoomInfo>> GetOTARoomInfo(RoomMappingDbContext RoomMappingDbContext, Stopwatch stopwatch, string[] batch, List<OTARoomInfo> mrooms)
    {
        try
        {

            var parameterList = string.Join(",", batch.Select((v, i) => $"@p{i}"));
            var parameters = batch.Select((v, i) => new SqlParameter($"p{i}", v)).ToArray();

            var sql = $"SELECT * FROM OTARoomInfo(NOLock) WHERE MTHotelCode IN ({parameterList}) And Status=1";
            mrooms = await RoomMappingDbContext.OTARoomInfos
                                    .FromSqlRaw(sql, parameters)
                                    .Select(e => new OTARoomInfo()
                                    {
                                        Id = e.Id,
                                        RoomNameCN = e.RoomNameCN,
                                        RoomNameEN = e.RoomNameEN,
                                        BedInfo = e.BedInfo,
                                        MTHotelCode = e.MTHotelCode,
                                    })
                                    .ToListAsync();
            logger.LogInformation($"读取母房型基础数据耗时 {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception e)
        {
            logger.LogError(e, "数据读取失败 SELECT * FROM CtripHotelRoomInfo(NOLock) WHERE MTHotelCode IN ");
            return null;
        }
        return mrooms;
    }

    private async Task<List<SupRoomInfoView>> GetSupRoomInfo(RoomMappingDbContext roomMappingDbContext, string[] batch, List<SupRoomInfoView> crooms)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            #region 注释
            //crooms = await MengTuSupContext.SupRoomInfoView
            //   .Where(e => batch.Contains(e.MTHotelCode))
            //   .Select(e => new SupRoomInfoView()
            //   {
            //       Id = e.Id,
            //       SupId = e.SupId,
            //       Status = e.Status,
            //       BedInfoKey = e.BedInfoKey,
            //       SRoomNameCN = e.SRoomNameCN,
            //       SRoomNameEN = e.SRoomNameEN,
            //       MTHotelCode = e.MTHotelCode,
            //       CreaterBy = e.CreateBy,
            //       MappingStatus = e.MappingStatus,
            //       OTARoomId = e.OTARoomId
            //   }).ToListAsync();

            //获取 供应商酒店列表
            //var list = string.Join(",", batch.Select(e => $"'{e}'"));
            //var parameters = batch.Select((v, i) => new SqlParameter($"p{i}", v)).ToArray();
            //var parameterList = string.Join(",", batch.Select((v, i) => $"@p{i}"));

            //using var transaction = await MengTuSupContext.Database.BeginTransactionAsync();
            //var setupSql = @$"
            //                     IF OBJECT_ID('tempdb..#HotelFilter') IS NOT NULL
            //                       DROP TABLE #HotelFilter;

            //                     SELECT SupId, SHotelCode, MTHotelCode
            //                            INTO #HotelFilter
            //                            FROM MengTuAPI.dbo.SupHotelInfo WITH(NOLOCK)
            //                            WHERE MTHotelCode IN({parameterList});";

            //await MengTuSupContext.Database.ExecuteSqlRawAsync(setupSql, parameters);

            //var sql = @$" 
            //                     SELECT
            //                       room.Id,
            //                       room.SupId,
            //                       room.SRoomName_CN,
            //                       room.SRoomName_EN,
            //                       room.BedInfoKey,
            //                       room.BedInfo,
            //                       room.Status,
            //                       rmc.CreateBy,
            //                       rmc.OTARoomId,
            //                       rmc.Status AS MappingStatus,
            //                       hotel.MTHotelCode
            //                     FROM SupRoomInfo room WITH(NOLOCK)
            //                     JOIN #HotelFilter hotel 
            //                       ON room.SupId = hotel.SupId AND room.SHotelCode = hotel.SHotelCode
            //                     LEFT JOIN MengtuSup.dbo.RoomMapping_Ctrip_test rmc
            //                       ON room.ID = rmc.SupRoomId";

            //var sql = $"SELECT * FROM V_SupRoomInfo WHERE MTHotelCode IN ({parameterList})";
            #endregion

            var parameterList = string.Join(",", batch);
            var dt = new DataTable();
            dt.Columns.Add("Value", typeof(string));
            foreach (var code in batch)
            {

                dt.Rows.Add(code);
            }
            var param = new SqlParameter("@MTHotelCodes", SqlDbType.Structured)
            {
                TypeName = "dbo.StringList", // 注意大小写和名称完全一致
                Value = dt
            };

            crooms = await roomMappingDbContext.SupRoomInfoView
                .FromSqlRaw("EXEC sp_GetSupRoomInfoByMTHotelCodes_TVP @MTHotelCodes", param).ToListAsync();
            //crooms = crooms.Where(e => e.Id == 157401639).ToList();

            //crooms = await MengTuSupContext.SupRoomInfoView
            //    .FromSqlRaw(sql, parameters)
            //    .Select(e => new SupRoomInfoView()
            //    {
            //        Id = e.Id,
            //        SupId = e.SupId,
            //        Status = e.Status,
            //        BedInfoKey = e.BedInfoKey,
            //        SRoomNameCN = e.SRoomNameCN,
            //        SRoomNameEN = e.SRoomNameEN,
            //        MTHotelCode = e.MTHotelCode,
            //        CreaterBy = e.CreateBy,
            //        MappingStatus = e.MappingStatus,
            //        OTARoomId = e.OTARoomId
            //    }).ToListAsync();
        }
        catch (Exception e)
        {

            logger.LogError(e, "数据读取失败 SELECT * FROM V_SupRoomInfo WHERE MTHotelCode IN");
            return null;
        }
        finally
        {
            await roomMappingDbContext.Database.CloseConnectionAsync();
            logger.LogInformation($"读取子房型基础数据耗时 {stopwatch.ElapsedMilliseconds}ms");
        }

        return crooms;
    }

}
