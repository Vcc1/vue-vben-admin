using Infrastructure.Sql.MengTuSup.Entitys;
using RoomMatch.Core.RoomDictionary;
using RoomMatch.Core.RoomAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Framework.Core.IOC;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using Infrastructure.Sql.MengTuSup;

namespace RoomMatch.Core.Apps
{
    //[Service]

    public class WordFrequencyApp : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        MengTuSupContext _mengTuSupContext;
        IRoomAnalyzer _roomAnalysis;
        private readonly IServiceProvider _serviceProvider;
        public WordFrequencyApp(IRoomAnalyzer roomAnalysis, MengTuSupContext mengTuSupContext, IServiceProvider serviceProvider, IServiceScopeFactory scopeFactory)
        {
            _roomAnalysis = roomAnalysis;
            _mengTuSupContext = mengTuSupContext;
            _serviceProvider = serviceProvider;
            _scopeFactory = scopeFactory;
        }

        ConcurrentBag<WordAttribute> _words = new ConcurrentBag<WordAttribute>();
        ConcurrentQueue<RoomContext> queryableWords = new ConcurrentQueue<RoomContext>();
        int maxConcurrentTasks = 12;
        List<Task> tasks = new List<Task>();
        public void Run()
        {
            Task.Factory.StartNew(() => Producer());
            Task.Factory.StartNew(() => ProcessQueueAsync());
            Task.Factory.StartNew(() => WSQL());
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RoomsReadFileAsync();
            //return Task.CompletedTask;
        }

        public void RoomsDbToFile()
        {
            var pageSize = 1000000;
            var currentPage = 0;
            while (true)
            {

                var pageData = _mengTuSupContext.SupRoomInfos
                            .OrderByDescending(e => e.Id)
                            .Skip(currentPage * pageSize)
                            .Take(pageSize)
                            .Select(e => System.Text.Json.JsonSerializer.Serialize(new SupRoomInfo() { Id = e.Id, SRoomNameEN = e.SRoomNameEN, SRoomNameCN = e.SRoomNameCN }))
                            .ToList();
                File.AppendAllLines("C:\\project\\code\\Hive\\Hive\\Hive\\RoomMatchTest\\Db\\rooms.txt", pageData);
                currentPage++;
            }


        }

        public async Task RoomsReadFileAsync()
        {
            var settings = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var s = Stopwatch.StartNew();
            var lines = File.ReadAllLines("C:\\project\\code\\Hive\\Hive\\Hive\\RoomMatchTest\\Db\\rooms.txt");
            s.Stop();
            Console.WriteLine($"数据读取完成 耗时{s.ElapsedMilliseconds}");
            s.Restart();
            var roomInfos = lines
                .Select(e => System.Text.Json.JsonSerializer.Deserialize<SupRoomInfo>(e, settings))
                .OfType<SupRoomInfo>()
                .ToList();

            roomInfos.Where(e => e.SRoomNameEN == e.SRoomNameCN).Select(e => e.SRoomNameCN = "").ToList();

            var roomContexts = roomInfos.SelectMany(e =>
            {
                return new string[] { e.SRoomNameEN, e.SRoomNameCN }
                       .Where(e => !string.IsNullOrEmpty(e))
                       .Select(s => new RoomContext<SupRoomInfo>(s, e));
            }).ToList();

            s.Stop();
            Console.WriteLine($"数据整理完成 耗时:{s.ElapsedMilliseconds}");
            s.Restart();
            var index = 0;
            foreach (var item in roomContexts)
            {
                index++;
                tasks = tasks.Where(t => !t.IsCompleted).ToList();
                if (tasks.Count > maxConcurrentTasks)
                {
                    await Task.WhenAny(tasks);
                }
                tasks.Add(Task.Run(() => Exe(item)));
                if (index % (10 * 10000) == 0)
                {
                    Console.WriteLine($"循环第 {index} ,时间: {s.ElapsedMilliseconds / 1000}");
                }
            }
            s.Stop();
            Console.WriteLine($"数据分词解析完成 耗时:{s.ElapsedMilliseconds}");




            Console.Read();
        }

        private void WordCombinationSearch(List<RoomContext> roomContexts, List<SearchMode> modes)
        {
            foreach (var item in roomContexts)
            {
                WordCombinationSearch(item, modes);
            }
        }


        private void WordCombinationSearch(RoomContext roomContext, List<SearchMode> modes)
        {
            var words = roomContext.RoomNameWords;
            for (int i = 0; i < words.Count; i++)
            {
                var word = words[i];
                foreach (var mode in modes)
                {
                    if (mode.IsKeyWord(word))
                    {
                        SearchWord(new SearchContext() { RoomContext = roomContext, SearchMode = mode, WordKeyIndex = i });
                    }
                }
            }
        }
        void SearchWord(SearchContext searchContext)
        {
            #region 向后搜索
            var startIndex = 0;
            var endIndex = 0;

            for (int i = endIndex = searchContext.WordKeyIndex + 1; i < searchContext.RoomContext.RoomNameWords.Count; i++)
            {
                var word = searchContext.RoomContext.RoomNameWords[i];
                var type = searchContext.SearchMode.WordType(word);
                if (type == RoomWordTypeEnum.EndSymbol)
                {
                    break;
                }
                else if (type == RoomWordTypeEnum.Connector)
                {
                    continue;
                }
                else if (type == RoomWordTypeEnum.EndWord)
                {
                    endIndex = i;
                    break;
                }
                else if (type == RoomWordTypeEnum.Word)
                {
                    endIndex = i;
                    break;
                }
            }
            #endregion

            #region 向前搜索
            for (int i = startIndex = searchContext.WordKeyIndex - 1; i > 0; i--)
            {
                var word = searchContext.RoomContext.RoomNameWords[i];
                var type = searchContext.SearchMode.WordType(word);
                if (type == RoomWordTypeEnum.EndSymbol)
                {
                    break;
                }
                else if (type == RoomWordTypeEnum.Connector)
                {
                    continue;
                }
                else if (type == RoomWordTypeEnum.EndWord)
                {
                    startIndex = i;
                    break;
                }
                else if (type == RoomWordTypeEnum.Word)
                {
                    startIndex = i;
                    break;
                }
            }
            #endregion


            #region 读取搜索内容
            if (searchContext.SearchMode.SearchDirection == SearchDirectionEnum.BothSides)
            {
                if (startIndex != endIndex)
                    searchContext.Result = new RoomWord[][] { searchContext.RoomContext.RoomNameWords.Skip(startIndex).Take(endIndex - startIndex).ToArray() };

            }
            if (searchContext.SearchMode.SearchDirection == SearchDirectionEnum.Forward)
            {
                if (startIndex != endIndex)
                    searchContext.Result = new RoomWord[][] { searchContext.RoomContext.RoomNameWords.Skip(startIndex).Take(endIndex - startIndex).ToArray() };
            }
            if (searchContext.SearchMode.SearchDirection == SearchDirectionEnum.Backward)
            {
                if (startIndex != endIndex)
                    searchContext.Result = new RoomWord[][] { searchContext.RoomContext.RoomNameWords.Skip(startIndex).Take(endIndex - startIndex).ToArray() };
            }
            #endregion

            #region 模式匹配

            #endregion

        }

        class SearchContext
        {
            public RoomContext RoomContext { get; set; }
            public SearchMode SearchMode { get; set; }
            public int WordKeyIndex { get; set; }

            public RoomWord[][] Result { get; set; }
        }

        enum RoomWordTypeEnum
        {
            Connector = 1,
            Word = 2,
            EndWord = 3,
            EndSymbol = 4,
        }
        class SearchMode()
        {
            /// <summary>
            /// 关键Word
            /// </summary>
            public Func<RoomWord, bool> IsKeyWord { get; set; }

            public SearchDirectionEnum SearchDirection { get; set; }

            public Func<RoomWord, RoomWordTypeEnum> WordType { get; set; }


        }
        enum SearchDirectionEnum
        {
            /// <summary>
            /// 向前
            /// </summary>
            Forward = 1,
            /// <summary>
            /// 向后
            /// </summary>
            Backward = 2,
            /// <summary>
            /// 向两端
            /// </summary>
            BothSides = 3,
            /// <summary>
            /// 分别向前和向后
            /// </summary>
            ForwardBackward = 4
        }





        private async void WSQL()
        {
            while (true)
            {
                await
                     Task.Delay(1000 * 30);
                try
                {
                    var words = _words;
                    _words = new ConcurrentBag<WordAttribute>();
                    var itemDtos = words.ToList();
                    if (itemDtos.Count() == 0)
                    {
                        continue;
                    }
                    using (var scope = _serviceProvider.CreateScope())
                    using (var context = scope.ServiceProvider.GetService<MengTuSupContext>())
                    using (var transaction = context.Database.BeginTransaction())
                    {
                        // 临时表构建
                        var dt = new DataTable();
                        dt.Columns.Add("Word", typeof(string));
                        dt.Columns.Add("WordId", typeof(int));

                        foreach (var dto in itemDtos)
                            dt.Rows.Add(dto.WordText, dto.Id);


                        // 创建临时表
                        context.Database.ExecuteSqlRaw(@"
                            CREATE TABLE #TempItems (
                                Word NVARCHAR(200),
                                WordId INT
                            );");

                        // 使用SqlBulkCopy批量插入到临时表
                        //using (var connection = (SqlConnection)context.Database.GetDbConnection())
                        //{
                        //(SqlConnection)context.Database.GetDbConnection()
                        //transaction.GetDbTransaction().Connection

                        using (var bulkCopy = new SqlBulkCopy((SqlConnection)transaction.GetDbTransaction().Connection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction.GetDbTransaction()))
                        {
                            bulkCopy.DestinationTableName = "#TempItems";
                            bulkCopy.WriteToServer(dt);
                        }
                        //}

                        // Merge语句执行批量更新或插入
                        context.Database.ExecuteSqlRaw(@"
                        MERGE INTO WordFrequency AS Target
                        USING (
                            SELECT Word, MIN(WordId) AS WordId, COUNT(*) AS Count
                            FROM #TempItems
                            GROUP BY Word
                        ) AS Source
                        ON Target.Word = Source.Word
                        WHEN MATCHED THEN
                            UPDATE SET Frequency = Target.Frequency + Source.Count
                        WHEN NOT MATCHED THEN
                            INSERT (Word, Frequency, WordId)
                            VALUES (Source.Word, Source.Count, Source.WordId);

                        DROP TABLE #TempItems;
                    ");
                        transaction.Commit();
                    }

                }
                catch (Exception e)
                {

                    Console.WriteLine(e.ToString());
                }



            }
        }

        async Task ProcessQueueAsync()
        {
            while (true)
            {
                if (queryableWords.Count == 0)
                {
                    await Task.Delay(1000 * 5);
                    continue;
                }
                // 清理已完成的任务
                tasks = tasks.Where(t => !t.IsCompleted).ToList();
                // 补充新的任务，直到达到 maxConcurrentTasks
                while (tasks.Count < maxConcurrentTasks && queryableWords.TryDequeue(out var item))
                {
                    tasks.Add(Task.Run(() => Exe(item)));
                }
                // 等待任意一个任务完成再继续
                if (tasks.Count > 0)
                {
                    await Task.WhenAny(tasks);
                }
            }

        }

        string[] ends = ",[]{}()".Select(e => e.ToString()).ToArray();
        string[] connectors = "- /\\_".Select(e => e.ToString()).ToArray();
        private void Exe(RoomContext item)
        {
            _roomAnalysis.Analysis(item);
            //var SearchResult =
            WordCombinationSearch(item, new List<SearchMode>() {
             new SearchMode(){ IsKeyWord = e=>
                 e.WordAttr.WordText == "room" ||  e.WordAttr.WordText == "rooms" || e.WordAttr.WordText == "non" || e.WordAttr.WordText == "no",
                 SearchDirection = SearchDirectionEnum.BothSides,
                 WordType = ( e=> {
                     if(ends.Contains(e.WordAttr.WordText))
                     {
                         return RoomWordTypeEnum.EndSymbol;
                     }
                     else if(connectors.Contains(e.WordAttr.WordText))
                     {
                         return RoomWordTypeEnum.Connector;
                     }
                     if(e.WordAttr.Id== 37610)
                     {
                         return RoomWordTypeEnum.Word;
                     }
                     if(e.WordAttr.WordTypeId>0)
                     {
                         return RoomWordTypeEnum.EndWord;
                     }
                     else
                     {
                         return RoomWordTypeEnum.Word;
                     }
                 })
                },

            });
        }

        private void Producer()
        {
            int pageSize = 10000;
            int currentPage = 0;
            bool hasMoreData = true;

            while (hasMoreData)
            {
                if (queryableWords.Count >= 100000)
                {
                    Console.WriteLine($"Queue size {queryableWords.Count} reached limit. Waiting...");
                    Task.Delay(1000 * 10).Wait();
                    continue;
                }
                try
                {
                    Console.WriteLine($"加载第{currentPage}页 从{pageSize * currentPage}条开始 当前队列{queryableWords.Count}条");

                    var pageData = _mengTuSupContext.SupRoomInfos
                         .OrderByDescending(e => e.Id)
                         .Skip(currentPage * pageSize)
                         .Take(pageSize)
                         .Select(e => new SupRoomInfo() { SRoomNameCN = e.SRoomNameCN, SRoomNameEN = e.SRoomNameEN, Id = e.Id })
                         .ToList();
                    pageData.Where(e => e.SRoomNameEN == e.SRoomNameCN).Select(e => e.SRoomNameCN == "");

                    var list = pageData.SelectMany(e =>
                    {
                        return new string[] { e.SRoomNameEN, e.SRoomNameCN }
                               .Where(e => !string.IsNullOrEmpty(e))
                               .Select(s => new RoomContext<SupRoomInfo>(s,e) );
                    }).ToList();
                    foreach (var item in list)
                    {
                        queryableWords.Enqueue(item);
                    }

                    if (!pageData.Any())
                    {
                        hasMoreData = false;
                        break;
                    }
                    //queryableWords.Except(pageData);
                    currentPage++;
                }
                catch (Exception)
                {
                    Task.Delay(1000 * 10).Wait();
                }
            }

            //queryableWords.CompleteAdding();
            Console.WriteLine("Finished adding items to queue.");
        }


    }
}
