using System;
using System.Collections.Generic;
using System.Text;

namespace TestMetrics
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    namespace TestMetrics
    {
        /// <summary>
        /// GoodsMetrics 指标测试服务
        /// </summary>
        public sealed class GoodsMetricsTestWorker : BackgroundService
        {
            private readonly GoodsMetrics _metrics;
            private readonly ILogger<GoodsMetricsTestWorker> _logger;

            // 模拟并发请求数量
            private const int ConcurrentWorkers = 10;

            // 错误类型固定在有限集合中，避免 error_type 标签基数无限增长
            private static readonly string[] ErrorTypes =
            {
                "timeout",
                "database_error",
                "redis_error",
                "validation_error",
                "unknown_error"
            };

            public GoodsMetricsTestWorker(
                GoodsMetrics metrics,
                ILogger<GoodsMetricsTestWorker> logger)
            {
                _metrics = metrics;
                _logger = logger;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                _logger.LogInformation(
                    "GoodsMetrics 测试服务已启动，并发数：{ConcurrentWorkers}",
                    ConcurrentWorkers);

                var workers = new Task[ConcurrentWorkers];

                for (var i = 0; i < ConcurrentWorkers; i++)
                {
                    var workerId = i + 1;
                    workers[i] = RunWorkerAsync(workerId, stoppingToken);
                }

                await Task.WhenAll(workers);
            }

            private async Task RunWorkerAsync(
                int workerId,
                CancellationToken stoppingToken)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _metrics.BeginRequest();

                    var stopwatch = Stopwatch.StartNew();

                    try
                    {
                        // 模拟接口执行时间：20～1000 毫秒
                        var requestDuration = Random.Shared.Next(20, 1001);

                        await Task.Delay(requestDuration, stoppingToken);

                        // 约 90% 成功、10% 失败
                        var isSuccess = Random.Shared.Next(1, 101) <= 90;

                        stopwatch.Stop();

                        if (isSuccess)
                        {
                            _metrics.RecordSuccess(stopwatch.Elapsed.TotalMilliseconds);
                        }
                        else
                        {
                            var errorType = ErrorTypes[
                                Random.Shared.Next(ErrorTypes.Length)];

                            _metrics.RecordError(
                                stopwatch.Elapsed.TotalMilliseconds,
                                errorType);
                        }
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        // 应用正常关闭，不记录为业务错误
                        break;
                    }
                    catch (Exception exception)
                    {
                        stopwatch.Stop();

                        _metrics.RecordError(
                            stopwatch.Elapsed.TotalMilliseconds,
                            "worker_exception");

                        _logger.LogError(
                            exception,
                            "指标测试线程 {WorkerId} 执行异常",
                            workerId);
                    }
                    finally
                    {
                        _metrics.EndRequest();
                    }

                    // 模拟两次请求之间的随机间隔：10～200 毫秒
                    try
                    {
                        await Task.Delay(
                            Random.Shared.Next(10, 201),
                            stoppingToken);
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                _logger.LogInformation(
                    "GoodsMetrics 测试线程 {WorkerId} 已停止",
                    workerId);
            }
        }
    }
}
