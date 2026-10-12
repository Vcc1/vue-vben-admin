using System;
using System.Collections.Generic;
using System.Text;

namespace TestMetrics
{
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using VBase.AppMetrics;

    public sealed class GoodsMetrics
    {
        private long _inflight;
        public const string Provider = "TestMetrics.GoodsMetrics";
        public GoodsMetrics(IMetricsFactory metricsFactory)
        {
            var metrics = metricsFactory.Create(Provider);
            RequestDuration = metrics.Meter.CreateHistogram<double>(
                name: "goods_request_duration",
                unit: "ms",
                description: "Goods request duration");
            SuccessCount = metrics.Meter.CreateCounter<long>(
                name: "goods_success_total",
                unit: "{count}",
                description: "Goods success count");
            ErrorCount = metrics.Meter.CreateCounter<long>(
                name: "goods_error_total",
                unit: "{count}",
                description: "Goods error count");
            metrics.Meter.CreateObservableGauge(
                name: "goods_inflight",
                observeValue: () => Interlocked.Read(ref _inflight),
                unit: "{count}",
                description: "Goods inflight count");
        }

        public Histogram<double> RequestDuration { get; }
        public Counter<long> SuccessCount { get; }
        public Counter<long> ErrorCount { get; }

        public void BeginRequest() => Interlocked.Increment(ref _inflight);

        public void EndRequest() => Interlocked.Decrement(ref _inflight);

        public void RecordSuccess(double elapsedMs)
        {
            RequestDuration.Record(elapsedMs, new TagList
            {
                { "resource_name", "goods" }
            });

            SuccessCount.Add(1, new TagList
            {
                { "resource_name", "goods" }
            });
        }

        public void RecordError(double elapsedMs, string errorType)
        {
            RequestDuration.Record(elapsedMs, new TagList
            {
               { "resource_name", "goods" }
            });

            ErrorCount.Add(1, new TagList
        {
            { "resource_name", "goods" },
            { "error_type", errorType }
        });
        }
    }
}
