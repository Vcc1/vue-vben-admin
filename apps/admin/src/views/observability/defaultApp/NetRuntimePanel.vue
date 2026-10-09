<script lang="ts" setup>
import type { PrometheusMatrixResult, PrometheusVectorResult } from '#/api/prometheus/types';

type EChartsOption = Record<string, any>;
type SeriesOption = Record<string, any>;

type RuntimeRange = '15m' | '1h' | '6h' | '24h';
type QueryValue = PrometheusMatrixResult[] | PrometheusVectorResult[];

interface QueryContext {
  baseLabels: Record<string, string>;
  end: number;
  processLabels: Record<string, string>;
  rangeSeconds: number;
  runtimeLabels: Record<string, string>;
  start: number;
  step: number;
  window: RuntimeRange;
}

interface QuerySpec {
  name: string;
  queryFn: (ctx: QueryContext) => Promise<QueryValue>;
}

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';
import { fetchNetRuntimePanelData } from '#/api/prometheus/netRuntimePanelData';

const props = defineProps<{
  queryTime?: null | number;
  targetLabels: Record<string, string>;
  timeRange: RuntimeRange;
}>();

const client = new PrometheusClient();

const showProcessInstrumentation = ref(false);
const loading = ref(false);
const hasLoadedOnce = ref(false);
const refreshing = ref(false);
const panelError = ref('');
const queryErrors = ref<Record<string, string>>({});
const queryData = ref<Record<string, QueryValue>>({});

const heapChartRef = ref<EchartsUIType>();
const fragChartRef = ref<EchartsUIType>();
const fragRateChartRef = ref<EchartsUIType>();
const gcCountChartRef = ref<EchartsUIType>();
const gcPauseChartRef = ref<EchartsUIType>();
const jitChartRef = ref<EchartsUIType>();
const tpChartRef = ref<EchartsUIType>();
const cpuTrendChartRef = ref<EchartsUIType>();
const memoryTrendChartRef = ref<EchartsUIType>();
const threadTrendChartRef = ref<EchartsUIType>();

const { renderEcharts: renderHeapChart } = useEcharts(heapChartRef);
const { renderEcharts: renderFragChart } = useEcharts(fragChartRef);
const { renderEcharts: renderFragRateChart } = useEcharts(fragRateChartRef);
const { renderEcharts: renderGcCountChart } = useEcharts(gcCountChartRef);
const { renderEcharts: renderGcPauseChart } = useEcharts(gcPauseChartRef);
const { renderEcharts: renderJitChart } = useEcharts(jitChartRef);
const { renderEcharts: renderTpChart } = useEcharts(tpChartRef);
const { renderEcharts: renderCpuTrendChart } = useEcharts(cpuTrendChartRef);
const { renderEcharts: renderMemoryTrendChart } = useEcharts(memoryTrendChartRef);
const { renderEcharts: renderThreadTrendChart } = useEcharts(threadTrendChartRef);

const chartOptionSignatures = ref<Record<string, string>>({});

let refreshTimer: null | number = null;

const rangeSecondsMap: Record<RuntimeRange, number> = {
  '15m': 15 * 60,
  '1h': 60 * 60,
  '6h': 6 * 60 * 60,
  '24h': 24 * 60 * 60,
};

const rangeStepMap: Record<RuntimeRange, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function buildSelector(labels: Record<string, string>) {
  const parts = Object.entries(labels)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `{${parts.join(',')}}`;
}

function buildMetricSelector(metricName: string, labels: Record<string, string>) {
  const selector = buildSelector(labels);
  return `${metricName}${selector}`;
}

function buildOrExpr(leftExpr: string, rightExpr: string) {
  return `(${leftExpr}) or (${rightExpr})`;
}

function buildGenerationLabel(metric: Record<string, string>) {
  return metric.gc_heap_generation || metric.generation || 'unknown';
}

function parseNumber(value?: [number, string] | null) {
  if (!value) {
    return null;
  }
  const num = Number(value[1]);
  return Number.isFinite(num) ? num : null;
}

function getVectorSum(results: PrometheusVectorResult[]) {
  const values = results
    .map((item) => parseNumber(item.value))
    .filter((item): item is number => item !== null);
  if (!values.length) {
    return null;
  }
  return values.reduce((acc, item) => acc + item, 0);
}

function getVectorMax(results: PrometheusVectorResult[]) {
  const values = results
    .map((item) => parseNumber(item.value))
    .filter((item): item is number => item !== null);
  if (!values.length) {
    return null;
  }
  return Math.max(...values);
}

function formatBytes(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '—';
  }
  const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB'];
  let size = value;
  let idx = 0;
  while (size >= 1024 && idx < units.length - 1) {
    size /= 1024;
    idx += 1;
  }
  return `${size.toFixed(size >= 100 ? 0 : 2)} ${units[idx]}`;
}

function formatDurationMs(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '—';
  }
  if (value >= 1000) {
    return `${(value / 1000).toFixed(2)} s`;
  }
  return `${value.toFixed(2)} ms`;
}

function formatNumber(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '—';
  }
  if (Math.abs(value) >= 1000_000) {
    return `${(value / 1000_000).toFixed(2)}M`;
  }
  if (Math.abs(value) >= 1000) {
    return `${(value / 1000).toFixed(2)}k`;
  }
  return value.toFixed(2);
}

function formatMiB(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '—';
  }
  return `${(value / 1024 / 1024).toFixed(2)} MiB`;
}

function getRangeParams(range: RuntimeRange, queryTime?: null | number) {
  const step = rangeStepMap[range];
  const baseTime =
    typeof queryTime === 'number' && Number.isFinite(queryTime)
      ? queryTime
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  const start = end - rangeSecondsMap[range];
  return { end, start, step };
}

function queryInstant(ctx: QueryContext, query: string) {
  return client.query(query, ctx.end);
}

function buildSeriesSignature(series: SeriesOption[]) {
  return JSON.stringify(
    series.map((item) => {
      const data = Array.isArray(item?.data) ? item.data : [];
      const first = data[0] || null;
      const last = data.at(-1) || null;
      return {
        first,
        last,
        len: data.length,
        name: item?.name || '',
      };
    }),
  );
}

function renderChartWithDiff(
  chartKey: string,
  series: SeriesOption[],
  formatter: (value: number) => string,
  yAxisName: string,
  renderFn: (option: EChartsOption) => void,
) {
  const signature = buildSeriesSignature(series);
  if (chartOptionSignatures.value[chartKey] === signature) {
    return;
  }
  chartOptionSignatures.value[chartKey] = signature;
  renderFn(buildLineOption(series, formatter, yAxisName));
}

function toSeriesByLabel(
  results: PrometheusMatrixResult[],
  nameResolver: (metric: Record<string, string>) => string,
  valueMapper?: (value: number) => number,
) {
  return results.map((item) => ({
    data: item.values.map(([ts, raw]) => {
      const num = Number(raw);
      const value = Number.isFinite(num) ? num : 0;
      return [ts * 1000, valueMapper ? valueMapper(value) : value] as [number, number];
    }),
    name: nameResolver(item.metric),
    showSymbol: false,
    smooth: true,
    type: 'line',
  }));
}

function sumRangeSeries(results: PrometheusMatrixResult[]) {
  const bucket = new Map<number, number>();
  for (const row of results) {
    for (const [ts, raw] of row.values) {
      const value = Number(raw);
      if (!Number.isFinite(value)) {
        continue;
      }
      bucket.set(ts, (bucket.get(ts) || 0) + value);
    }
  }

  return [...bucket.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([ts, value]) => [ts * 1000, value] as [number, number]);
}

function hasPointData(series: SeriesOption[]) {
  return series.some((item) => Array.isArray(item?.data) && item.data.length > 0);
}

function buildLineOption(
  series: SeriesOption[],
  formatter: (value: number) => string,
  yAxisName: string,
): EChartsOption {
  return {
    grid: {
      bottom: 20,
      containLabel: true,
      left: 20,
      right: 20,
      top: 30,
    },
    legend: {
      type: 'scroll',
    },
    tooltip: {
      trigger: 'axis',
      formatter: (params: any) => {
        const list = Array.isArray(params) ? params : [params];
        if (!list.length) {
          return '';
        }

        const time = list[0]?.value?.[0];
        const rows = list
          .map((item: any) => {
            const value = Number(item?.value?.[1]);
            return `${item.marker}${item.seriesName}: ${formatter(value)}`;
          })
          .join('<br/>');

        return `${dayjs(time).format('YYYY-MM-DD HH:mm:ss')}<br/>${rows}`;
      },
    },
    xAxis: { type: 'time' },
    yAxis: {
      axisLabel: {
        formatter: (value: number) => formatter(value),
      },
      name: yAxisName,
      type: 'value',
    },
    series,
  };
}

const context = computed<QueryContext>(() => {
  const { end, start, step } = getRangeParams(props.timeRange, props.queryTime);
  const baseLabels = props.targetLabels || {};
  const runtimeLabels = {
    ...baseLabels,
  };
  const processLabels = {
    ...baseLabels,
  };
  return {
    baseLabels,
    end,
    processLabels,
    rangeSeconds: Math.max(1, end - start),
    start,
    step,
    window: props.timeRange,
    runtimeLabels,
  };
});

const queries: Record<string, QuerySpec> = {
  cpuCountDotnet: {
    name: 'dotnet_process_cpu_count',
    queryFn: async (ctx) => queryInstant(ctx, buildMetricSelector('dotnet_process_cpu_count', ctx.runtimeLabels)),
  },
  cpuTimeIncDotnet: {
    name: 'dotnet_process_cpu_time_seconds_total increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        `increase(${buildMetricSelector('dotnet_process_cpu_time_seconds_total', ctx.runtimeLabels)}[${ctx.window}])`,
      ),
  },
  workingSet: {
    name: 'dotnet_process_memory_working_set_bytes',
    queryFn: async (ctx) => queryInstant(ctx, buildMetricSelector('dotnet_process_memory_working_set_bytes', ctx.runtimeLabels)),
  },
  virtualMemory: {
    name: 'process_memory_virtual_bytes',
    queryFn: async (ctx) => queryInstant(ctx, buildMetricSelector('process_memory_virtual_bytes', ctx.processLabels)),
  },
  processThreadCount: {
    name: 'process_thread_count',
    queryFn: async (ctx) => queryInstant(ctx, buildMetricSelector('process_thread_count', ctx.processLabels)),
  },
  gcGen0Inc: {
    name: 'dotnet_gc_collections_total gen0 increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_collections_total', { ...ctx.runtimeLabels, gc_heap_generation: 'gen0' })}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', { ...ctx.runtimeLabels, generation: 'gen0' })}[${ctx.window}])`,
        ),
      ),
  },
  gcGen1Inc: {
    name: 'dotnet_gc_collections_total gen1 increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_collections_total', { ...ctx.runtimeLabels, gc_heap_generation: 'gen1' })}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', { ...ctx.runtimeLabels, generation: 'gen1' })}[${ctx.window}])`,
        ),
      ),
  },
  gcGen2Inc: {
    name: 'dotnet_gc_collections_total gen2 increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_collections_total', { ...ctx.runtimeLabels, gc_heap_generation: 'gen2' })}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', { ...ctx.runtimeLabels, generation: 'gen2' })}[${ctx.window}])`,
        ),
      ),
  },
  gcPauseInc: {
    name: 'dotnet_gc_pause_time_seconds_total increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_pause_time_seconds_total', ctx.runtimeLabels)}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_duration_nanoseconds_total', ctx.runtimeLabels)}[${ctx.window}]) / 1000000000`,
        ),
      ),
  },
  gcAllocatedInc: {
    name: 'dotnet_gc_heap_allocated_bytes_total increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_heap_allocated_bytes_total', ctx.runtimeLabels)}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_allocations_size_bytes_total', ctx.runtimeLabels)}[${ctx.window}])`,
        ),
      ),
  },
  lockContentionsInc: {
    name: 'dotnet_monitor_lock_contentions_total increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_monitor_lock_contentions_total', ctx.runtimeLabels)}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_monitor_lock_contention_count_total', ctx.runtimeLabels)}[${ctx.window}])`,
        ),
      ),
  },
  tpQueueLength: {
    name: 'dotnet_thread_pool_queue_length_total',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          buildMetricSelector('dotnet_thread_pool_queue_length_total', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_thread_pool_queue_length', ctx.runtimeLabels),
        ),
      ),
  },
  tpThreadCount: {
    name: 'dotnet_thread_pool_thread_count_total',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          buildMetricSelector('dotnet_thread_pool_thread_count_total', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_thread_pool_threads_count', ctx.runtimeLabels),
        ),
      ),
  },
  tpWorkItemInc: {
    name: 'dotnet_thread_pool_work_item_count_total increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          `increase(${buildMetricSelector('dotnet_thread_pool_work_item_count_total', ctx.runtimeLabels)}[${ctx.window}])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_thread_pool_completed_items_count_total', ctx.runtimeLabels)}[${ctx.window}])`,
        ),
      ),
  },
  timerCount: {
    name: 'dotnet_timer_count',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        buildOrExpr(
          buildMetricSelector('dotnet_timer_count', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_timer_count', ctx.runtimeLabels),
        ),
      ),
  },
  heapSizeRange: {
    name: 'dotnet_gc_last_collection_heap_size_bytes range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          buildMetricSelector('dotnet_gc_last_collection_heap_size_bytes', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_gc_heap_size_bytes', ctx.runtimeLabels),
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  fragSizeRange: {
    name: 'dotnet_gc_last_collection_heap_fragmentation_size_bytes range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          buildMetricSelector('dotnet_gc_last_collection_heap_fragmentation_size_bytes', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_gc_heap_fragmentation_size_bytes', ctx.runtimeLabels),
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  gcCollectionsIncRange: {
    name: 'dotnet_gc_collections_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_collections_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', ctx.runtimeLabels)}[1m])`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  gcPauseIncRange: {
    name: 'dotnet_gc_pause_time_seconds_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_gc_pause_time_seconds_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_gc_duration_nanoseconds_total', ctx.runtimeLabels)}[1m]) / 1000000000`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  jitMethodsIncRange: {
    name: 'dotnet_jit_compiled_methods_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_jit_compiled_methods_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_jit_methods_compiled_count_total', ctx.runtimeLabels)}[1m])`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  jitIlBytesIncRange: {
    name: 'dotnet_jit_compiled_il_size_bytes_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_jit_compiled_il_size_bytes_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_jit_il_compiled_size_bytes_total', ctx.runtimeLabels)}[1m])`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  jitTimeIncRange: {
    name: 'dotnet_jit_compilation_time_seconds_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_jit_compilation_time_seconds_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_jit_compilation_time_nanoseconds_total', ctx.runtimeLabels)}[1m]) / 1000000000`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  tpQueueRange: {
    name: 'dotnet_thread_pool_queue_length_total range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          buildMetricSelector('dotnet_thread_pool_queue_length_total', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_thread_pool_queue_length', ctx.runtimeLabels),
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  tpThreadRange: {
    name: 'dotnet_thread_pool_thread_count_total range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          buildMetricSelector('dotnet_thread_pool_thread_count_total', ctx.runtimeLabels),
          buildMetricSelector('process_runtime_dotnet_thread_pool_threads_count', ctx.runtimeLabels),
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  tpWorkIncRange: {
    name: 'dotnet_thread_pool_work_item_count_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_thread_pool_work_item_count_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_runtime_dotnet_thread_pool_completed_items_count_total', ctx.runtimeLabels)}[1m])`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  processCpuCount: {
    name: 'process_cpu_count',
    queryFn: async (ctx) => queryInstant(ctx, buildMetricSelector('process_cpu_count', ctx.processLabels)),
  },
  processCpuTimeInc: {
    name: 'process_cpu_time_seconds_total increase',
    queryFn: async (ctx) =>
      queryInstant(ctx,
        `increase(${buildMetricSelector('process_cpu_time_seconds_total', ctx.processLabels)}[${ctx.window}])`,
      ),
  },
  cpuTimeIncRangeDotnet: {
    name: 'dotnet_process_cpu_time_seconds_total increase range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          `increase(${buildMetricSelector('dotnet_process_cpu_time_seconds_total', ctx.runtimeLabels)}[1m])`,
          `increase(${buildMetricSelector('process_cpu_time_seconds_total', ctx.processLabels)}[1m])`,
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  processMemoryUsage: {
    name: 'process_memory_usage_bytes',
    queryFn: async (ctx) => queryInstant(ctx, buildMetricSelector('process_memory_usage_bytes', ctx.processLabels)),
  },
  workingSetRange: {
    name: 'dotnet_process_memory_working_set_bytes range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildOrExpr(
          buildMetricSelector('dotnet_process_memory_working_set_bytes', ctx.runtimeLabels),
          buildMetricSelector('process_memory_usage_bytes', ctx.processLabels),
        ),
        start: ctx.start,
        step: ctx.step,
      }),
  },
  processThreadCountRange: {
    name: 'process_thread_count range',
    queryFn: async (ctx) =>
      client.queryRange({
        end: ctx.end,
        query: buildMetricSelector('process_thread_count', ctx.processLabels),
        start: ctx.start,
        step: ctx.step,
      }),
  },
};

void queries;

function getVectorData(key: string) {
  return (queryData.value[key] || []) as PrometheusVectorResult[];
}

function getMatrixData(key: string) {
  return (queryData.value[key] || []) as PrometheusMatrixResult[];
}

function getQueryError(key: string) {
  return queryErrors.value[key] || '';
}

const gcGen0Inc = computed(() => getVectorSum(getVectorData('gcGen0Inc')));
const gcGen1Inc = computed(() => getVectorSum(getVectorData('gcGen1Inc')));
const gcGen2Inc = computed(() => getVectorSum(getVectorData('gcGen2Inc')));
const gcPauseIncMs = computed(() => {
  const seconds = getVectorSum(getVectorData('gcPauseInc'));
  return seconds === null ? null : seconds * 1000;
});
const gcAllocatedInc = computed(() => getVectorSum(getVectorData('gcAllocatedInc')));
const lockContentionsInc = computed(() => getVectorSum(getVectorData('lockContentionsInc')));
const tpQueueLength = computed(() => getVectorMax(getVectorData('tpQueueLength')));
const tpWorkItemInc = computed(() => getVectorSum(getVectorData('tpWorkItemInc')));
const timerCount = computed(() => getVectorMax(getVectorData('timerCount')));
const dotnetCpuCount = computed(() => getVectorMax(getVectorData('cpuCountDotnet')));
const processCpuCount = computed(() => getVectorMax(getVectorData('processCpuCount')));

const cpuDerived = computed(() => {
  const cpuCount = dotnetCpuCount.value ?? processCpuCount.value;
  const cpuTimeRows = getVectorData('cpuTimeIncDotnet').length
    ? getVectorData('cpuTimeIncDotnet')
    : getVectorData('processCpuTimeInc');
  const totalDelta = cpuTimeRows
    .map((item) => parseNumber(item.value))
    .filter((value): value is number => value !== null)
    .reduce((acc, value) => acc + value, 0);

  if (!cpuCount || !Number.isFinite(totalDelta)) {
    return { avgCores: null as null | number, percent: null as null | number };
  }

  const avgCores = totalDelta / context.value.rangeSeconds;
  const percent = (avgCores / cpuCount) * 100;
  return { avgCores, percent };
});

const workingSet = computed(() => getVectorMax(getVectorData('workingSet')));
const processMemoryUsage = computed(() => getVectorMax(getVectorData('processMemoryUsage')));
const virtualMemory = computed(() => getVectorMax(getVectorData('virtualMemory')));

const processThreadCount = computed(() => getVectorMax(getVectorData('processThreadCount')));
const threadPoolThreadCount = computed(() => getVectorMax(getVectorData('tpThreadCount')));

const runtimeAlerts = computed(() => {
  const alerts: Array<{ level: 'danger' | 'warning'; text: string }> = [];
  const gen2 = gcGen2Inc.value;
  const pauseMs = gcPauseIncMs.value;
  const locks = lockContentionsInc.value;
  const queue = tpQueueLength.value;
  const lohSeries = getMatrixData('heapSizeRange').find((item) => buildGenerationLabel(item.metric) === 'loh');

  if (gen2 !== null && gen2 > 10) {
    alerts.push({ level: 'warning', text: 'gen2 增量较高，可能内存压力/晋升增多' });
  }

  if (lohSeries && lohSeries.values.length >= 2) {
    const firstPoint = lohSeries.values[0];
    if (!firstPoint) {
      return alerts;
    }
    const first = Number(firstPoint[1]);
    const last = Number(lohSeries.values.at(-1)?.[1]);
    if (Number.isFinite(first) && Number.isFinite(last) && last > first * 1.2) {
      alerts.push({ level: 'warning', text: 'LOH 堆持续上升，可能存在大对象/缓存未释放风险' });
    }
  }

  if (pauseMs !== null) {
    const perMin = pauseMs / Math.max(1, context.value.rangeSeconds / 60);
    if (perMin > 100) {
      alerts.push({ level: 'danger', text: 'GC 暂停偏高' });
    }
  }

  if (locks !== null && locks > 100) {
    alerts.push({ level: 'warning', text: '锁竞争升高' });
  }

  if (queue !== null && queue > 0) {
    alerts.push({ level: 'warning', text: '线程池积压（queue_length 持续 > 0）' });
  }

  return alerts;
});

const statCards = computed(() => {
  const cards = [
    {
      error: getQueryError('cpuCountDotnet') || getQueryError('cpuTimeIncDotnet'),
      subtitle: `平均占用核数 ${formatNumber(cpuDerived.value.avgCores)}`,
      title: 'CPU',
      value:
        cpuDerived.value.percent === null
          ? '—'
          : `${cpuDerived.value.percent.toFixed(2)}% / ${formatNumber(dotnetCpuCount.value ?? processCpuCount.value)} 核`,
    },
    {
      error: getQueryError('workingSet') || getQueryError('virtualMemory'),
      subtitle: `虚拟内存 ${formatBytes(virtualMemory.value)}`,
      title: '内存',
      value: `${formatBytes(workingSet.value ?? processMemoryUsage.value)}`,
    },
    {
      error: getQueryError('processThreadCount') || getQueryError('tpThreadCount'),
      subtitle:
        processThreadCount.value === null && threadPoolThreadCount.value !== null
          ? '线程池线程数'
          : '进程线程数',
      title: '线程',
      value: `${formatNumber(processThreadCount.value ?? threadPoolThreadCount.value)}`,
    },
    {
      error: getQueryError('gcGen0Inc') || getQueryError('gcGen1Inc') || getQueryError('gcGen2Inc') || getQueryError('gcPauseInc') || getQueryError('gcAllocatedInc'),
      subtitle: `Pause ${formatDurationMs(gcPauseIncMs.value)} / 分配 ${formatMiB(gcAllocatedInc.value)}`,
      title: 'GC',
      value: `gen0 ${formatNumber(gcGen0Inc.value)} | gen1 ${formatNumber(gcGen1Inc.value)} | gen2 ${formatNumber(gcGen2Inc.value)}`,
    },
    {
      error: getQueryError('lockContentionsInc'),
      subtitle: `窗口 ${props.timeRange}`,
      title: '锁竞争',
      value: `${formatNumber(lockContentionsInc.value)} 次`,
    },
    {
      error: getQueryError('tpQueueLength') || getQueryError('tpWorkItemInc'),
      subtitle: `work_item 增量 ${formatNumber(tpWorkItemInc.value)}`,
      title: '线程池积压',
      value: `queue ${formatNumber(tpQueueLength.value)}`,
    },
    {
      error: getQueryError('timerCount'),
      subtitle: '当前值',
      title: 'Timer 数',
      value: `${formatNumber(timerCount.value)}`,
    },
  ];

  if (showProcessInstrumentation.value) {
    cards.push({
      error: getQueryError('processMemoryUsage') || getQueryError('processCpuTimeInc'),
      subtitle: `CPU 累积增量 ${formatNumber(getVectorSum(getVectorData('processCpuTimeInc')))} s`,
      title: 'Process 补充',
      value: `内存 ${formatBytes(processMemoryUsage.value)}`,
    });
  }

  return cards;
});

function buildFragRateSeries() {
  const fragRows = getMatrixData('fragSizeRange');
  const heapRows = getMatrixData('heapSizeRange');

  const heapByGen = new Map(heapRows.map((item) => [buildGenerationLabel(item.metric), item]));

  const series: SeriesOption[] = [];
  for (const fragItem of fragRows) {
      const generation = buildGenerationLabel(fragItem.metric);
      const heapItem = heapByGen.get(generation);
      if (!heapItem) {
        continue;
      }

      const heapMap = new Map(heapItem.values.map(([ts, value]) => [ts, Number(value)]));
      series.push({
        data: fragItem.values.map(([ts, raw]) => {
          const frag = Number(raw);
          const heap = heapMap.get(ts) || 0;
          const ratio = heap > 0 ? (frag / heap) * 100 : 0;
          return [ts * 1000, ratio] as [number, number];
        }),
        name: generation.toUpperCase(),
        showSymbol: false,
        smooth: true,
        type: 'line',
      });
  }

  return series;
}

const heapSeriesData = computed(() =>
  toSeriesByLabel(
    getMatrixData('heapSizeRange'),
    (metric) => buildGenerationLabel(metric).toUpperCase(),
    (value) => value / 1024 / 1024,
  ),
);

const fragSeriesData = computed(() =>
  toSeriesByLabel(
    getMatrixData('fragSizeRange'),
    (metric) => buildGenerationLabel(metric).toUpperCase(),
    (value) => value / 1024 / 1024,
  ),
);

const fragRateSeriesData = computed(() => buildFragRateSeries());

const gcCountSeriesData = computed(() =>
  toSeriesByLabel(
    getMatrixData('gcCollectionsIncRange'),
    (metric) => buildGenerationLabel(metric).toUpperCase(),
  ),
);

const gcPauseSeriesData = computed<SeriesOption[]>(() => [
  {
    data: sumRangeSeries(getMatrixData('gcPauseIncRange')).map(([ts, value]) => [ts, value * 1000] as [number, number]),
    name: 'Pause',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
]);

const jitSeriesData = computed<SeriesOption[]>(() => [
  {
    data: sumRangeSeries(getMatrixData('jitMethodsIncRange')),
    name: 'compiled_methods 澧為噺',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
  {
    data: sumRangeSeries(getMatrixData('jitIlBytesIncRange')).map(([ts, value]) => [ts, value / 1024] as [number, number]),
    name: 'compiled_il_size 澧為噺(KiB)',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
  {
    data: sumRangeSeries(getMatrixData('jitTimeIncRange')).map(([ts, value]) => [ts, value * 1000] as [number, number]),
    name: 'jit_time 澧為噺(ms)',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
]);

const tpSeriesData = computed<SeriesOption[]>(() => [
  {
    data: sumRangeSeries(getMatrixData('tpQueueRange')),
    name: 'queue_length',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
  {
    data: sumRangeSeries(getMatrixData('tpThreadRange')),
    name: 'thread_count',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
  {
    data: sumRangeSeries(getMatrixData('tpWorkIncRange')),
    name: 'work_item_count 澧為噺',
    showSymbol: false,
    smooth: true,
    type: 'line',
  },
]);

const cpuTrendSeriesData = computed<SeriesOption[]>(() => {
  const cpuCount = dotnetCpuCount.value ?? processCpuCount.value;
  if (!cpuCount || !Number.isFinite(cpuCount) || cpuCount <= 0) {
    return [];
  }

  const data = sumRangeSeries(getMatrixData('cpuTimeIncRangeDotnet')).map(([ts, value]) => [
    ts,
    (value / 60 / cpuCount) * 100,
  ] as [number, number]);

  return data.length
    ? [{
        data,
        name: 'CPU %',
        showSymbol: false,
        smooth: true,
        type: 'line',
      }]
    : [];
});

const memoryTrendSeriesData = computed<SeriesOption[]>(() =>
  toSeriesByLabel(
    getMatrixData('workingSetRange'),
    () => 'working_set',
    (value) => value / 1024 / 1024,
  ),
);

const threadTrendSeriesData = computed<SeriesOption[]>(() => {
  const processSeries = toSeriesByLabel(
    getMatrixData('processThreadCountRange'),
    () => 'process_threads',
  );

  if (processSeries.length > 0) {
    return processSeries;
  }

  return toSeriesByLabel(
    getMatrixData('tpThreadRange'),
    () => 'thread_pool_threads',
  );
});

const showHeapChart = computed(() => hasPointData(heapSeriesData.value));
const showFragChart = computed(() => hasPointData(fragSeriesData.value));
const showFragRateChart = computed(() => hasPointData(fragRateSeriesData.value));
const showGcCountChart = computed(() => hasPointData(gcCountSeriesData.value));
const showGcPauseChart = computed(() => hasPointData(gcPauseSeriesData.value));
const showJitChart = computed(() => hasPointData(jitSeriesData.value));
const showTpChart = computed(() => hasPointData(tpSeriesData.value));
const showCpuTrendChart = computed(() => hasPointData(cpuTrendSeriesData.value));
const showMemoryTrendChart = computed(() => hasPointData(memoryTrendSeriesData.value));
const showThreadTrendChart = computed(() => hasPointData(threadTrendSeriesData.value));

function renderCharts() {
  const heapSeries = toSeriesByLabel(
    getMatrixData('heapSizeRange'),
    (metric) => buildGenerationLabel(metric).toUpperCase(),
    (value) => value / 1024 / 1024,
  );

  const fragSeries = toSeriesByLabel(
    getMatrixData('fragSizeRange'),
    (metric) => buildGenerationLabel(metric).toUpperCase(),
    (value) => value / 1024 / 1024,
  );

  const fragRateSeries = buildFragRateSeries();

  const gcCountSeries = toSeriesByLabel(
    getMatrixData('gcCollectionsIncRange'),
    (metric) => buildGenerationLabel(metric).toUpperCase(),
  );

  const gcPauseSeries: SeriesOption[] = [
    {
      data: sumRangeSeries(getMatrixData('gcPauseIncRange')).map(([ts, value]) => [ts, value * 1000] as [number, number]),
      name: 'Pause',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
  ];

  const jitSeries: SeriesOption[] = [
    {
      data: sumRangeSeries(getMatrixData('jitMethodsIncRange')),
      name: 'compiled_methods 增量',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: sumRangeSeries(getMatrixData('jitIlBytesIncRange')).map(([ts, value]) => [ts, value / 1024] as [number, number]),
      name: 'compiled_il_size 增量(KiB)',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: sumRangeSeries(getMatrixData('jitTimeIncRange')).map(([ts, value]) => [ts, value * 1000] as [number, number]),
      name: 'jit_time 增量(ms)',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
  ];

  const tpSeries: SeriesOption[] = [
    {
      data: sumRangeSeries(getMatrixData('tpQueueRange')),
      name: 'queue_length',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: sumRangeSeries(getMatrixData('tpThreadRange')),
      name: 'thread_count',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: sumRangeSeries(getMatrixData('tpWorkIncRange')),
      name: 'work_item_count 增量',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
  ];

  nextTick(() => {
    if (showHeapChart.value) {
      renderChartWithDiff('heap', heapSeries, (value) => `${value.toFixed(2)} MiB`, 'MiB', renderHeapChart);
    }
    if (showFragChart.value) {
      renderChartWithDiff('frag', fragSeries, (value) => `${value.toFixed(2)} MiB`, 'MiB', renderFragChart);
    }
    if (showFragRateChart.value) {
      renderChartWithDiff('fragRate', fragRateSeries, (value) => `${value.toFixed(2)}%`, '%', renderFragRateChart);
    }
    if (showGcCountChart.value) {
      renderChartWithDiff('gcCount', gcCountSeries, (value) => `${value.toFixed(2)}`, 'count', renderGcCountChart);
    }
    if (showGcPauseChart.value) {
      renderChartWithDiff('gcPause', gcPauseSeries, (value) => `${value.toFixed(2)} ms`, 'ms', renderGcPauseChart);
    }
    if (showJitChart.value) {
      renderChartWithDiff('jit', jitSeries, (value) => `${value.toFixed(2)}`, 'value', renderJitChart);
    }
    if (showTpChart.value) {
      renderChartWithDiff('threadPool', tpSeries, (value) => `${value.toFixed(2)}`, 'value', renderTpChart);
    }
    if (showCpuTrendChart.value) {
      renderChartWithDiff('cpuTrend', cpuTrendSeriesData.value, (value) => `${value.toFixed(2)}%`, '%', renderCpuTrendChart);
    }
    if (showMemoryTrendChart.value) {
      renderChartWithDiff('memoryTrend', memoryTrendSeriesData.value, (value) => `${value.toFixed(2)} MiB`, 'MiB', renderMemoryTrendChart);
    }
    if (showThreadTrendChart.value) {
      renderChartWithDiff('threadTrend', threadTrendSeriesData.value, (value) => `${value.toFixed(2)}`, 'count', renderThreadTrendChart);
    }
  });
}

async function refresh() {
  const selectedLabels = context.value.baseLabels;
  if (!selectedLabels || !Object.keys(selectedLabels).length) {
    panelError.value = '缺少 targetLabels，无法查询 .NET Runtime 指标';
    queryData.value = {};
    queryErrors.value = {};
    return;
  }

  const isFirstLoad = !hasLoadedOnce.value;
  loading.value = isFirstLoad;
  refreshing.value = !isFirstLoad;
  panelError.value = '';

  try {
    const panelData = await fetchNetRuntimePanelData({
      includeProcessInstrumentation: true,
      queryTime: props.queryTime,
      showProcessInstrumentation: showProcessInstrumentation.value,
      targetLabels: selectedLabels,
      timeRange: props.timeRange,
    });

    queryData.value = panelData.raw;
    queryErrors.value = panelData.errors;
    renderCharts();
  } catch (error) {
    panelError.value = error instanceof Error ? error.message : String(error);
    queryData.value = {};
    queryErrors.value = {};
  } finally {
    loading.value = false;
    refreshing.value = false;
    hasLoadedOnce.value = true;
  }
}

function scheduleRefresh() {
  if (refreshTimer) {
    clearTimeout(refreshTimer);
  }
  refreshTimer = window.setTimeout(async () => {
    await refresh();
  }, 300);
}

watch(
  () => [props.timeRange, props.queryTime, JSON.stringify(props.targetLabels), showProcessInstrumentation.value],
  () => {
    scheduleRefresh();
  },
  { immediate: true },
);

onUnmounted(() => {
  if (refreshTimer) {
    clearTimeout(refreshTimer);
  }
});

defineExpose({ refresh });
</script>

<template>
  <div class="net-runtime-panel" v-loading="loading">
    <div class="panel-toolbar">
      <div class="panel-title">.NET Runtime / Process</div>
      <div class="toolbar-right">
        <span v-if="refreshing" class="refreshing-text">刷新中...</span>
        <el-switch
          v-model="showProcessInstrumentation"
          active-text="显示 Process instrumentation（可能重复）"
        />
      </div>
    </div>

    <el-alert
      v-if="panelError"
      :closable="false"
      :title="panelError"
      class="mt-2"
      type="error"
    />

    <div class="stat-grid mt-3">
      <el-card v-for="card in statCards" :key="card.title" shadow="never">
        <div class="stat-head">
          <div class="stat-title">{{ card.title }}</div>
          <el-tooltip v-if="card.error" :content="card.error" placement="top">
            <span class="stat-error">!</span>
          </el-tooltip>
        </div>
        <div class="stat-value">{{ card.value }}</div>
        <div class="stat-sub">{{ card.subtitle }}</div>
      </el-card>
    </div>

    <div v-if="runtimeAlerts.length" class="alert-badges mt-3">
      <el-tag
        v-for="(alert, index) in runtimeAlerts"
        :key="`${alert.text}-${index}`"
        :type="alert.level === 'danger' ? 'danger' : 'warning'"
        effect="light"
      >
        {{ alert.text }}
      </el-tag>
    </div>

    <div class="charts-wrap mt-3">
      <el-card v-if="showCpuTrendChart" shadow="never">
        <template #header>CPU 趋势（%）</template>
        <EchartsUI ref="cpuTrendChartRef" class="chart" />
      </el-card>

      <el-card v-if="showMemoryTrendChart" class="mt-3" shadow="never">
        <template #header>内存趋势（MiB）</template>
        <EchartsUI ref="memoryTrendChartRef" class="chart" />
      </el-card>

      <el-card v-if="showThreadTrendChart" class="mt-3" shadow="never">
        <template #header>线程趋势</template>
        <EchartsUI ref="threadTrendChartRef" class="chart" />
      </el-card>

      <el-card v-if="showHeapChart" shadow="never">
        <template #header>GC 堆大小（MiB）</template>
        <EchartsUI ref="heapChartRef" class="chart" />
      </el-card>

      <el-card v-if="showFragChart" class="mt-3" shadow="never">
        <template #header>GC 碎片（MiB）</template>
        <EchartsUI ref="fragChartRef" class="chart" />
      </el-card>

      <el-card v-if="showFragRateChart" class="mt-3" shadow="never">
        <template #header>GC 碎片率（%）</template>
        <EchartsUI ref="fragRateChartRef" class="chart" />
      </el-card>

      <el-card v-if="showGcCountChart" class="mt-3" shadow="never">
        <template #header>GC 次数增量（每分钟）</template>
        <EchartsUI ref="gcCountChartRef" class="chart" />
      </el-card>

      <el-card v-if="showGcPauseChart" class="mt-3" shadow="never">
        <template #header>GC Pause 增量（ms）</template>
        <EchartsUI ref="gcPauseChartRef" class="chart" />
      </el-card>

      <el-card v-if="showJitChart" class="mt-3" shadow="never">
        <template #header>JIT 增量趋势</template>
        <EchartsUI ref="jitChartRef" class="chart" />
      </el-card>

      <el-card v-if="showTpChart" class="mt-3" shadow="never">
        <template #header>线程池趋势</template>
        <EchartsUI ref="tpChartRef" class="chart" />
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.net-runtime-panel {
  width: 100%;
}

.panel-toolbar {
  align-items: center;
  display: flex;
  justify-content: space-between;
}

.toolbar-right {
  align-items: center;
  display: flex;
  gap: 10px;
}

.refreshing-text {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.panel-title {
  font-size: 16px;
  font-weight: 700;
}

.stat-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.stat-head {
  align-items: center;
  display: flex;
  justify-content: space-between;
}

.stat-title {
  color: var(--el-text-color-regular);
  font-size: 12px;
}

.stat-value {
  color: var(--el-text-color-primary);
  font-size: 16px;
  font-weight: 700;
  margin-top: 8px;
  word-break: break-word;
}

.stat-sub {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  margin-top: 6px;
}

.stat-error {
  align-items: center;
  background: var(--el-color-danger);
  border-radius: 999px;
  color: #fff;
  display: inline-flex;
  font-size: 11px;
  font-weight: 700;
  height: 16px;
  justify-content: center;
  width: 16px;
}

.alert-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.chart {
  height: 260px;
}

@media (max-width: 1280px) {
  .stat-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
