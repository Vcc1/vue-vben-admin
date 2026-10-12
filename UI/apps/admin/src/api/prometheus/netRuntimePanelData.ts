import type {
  PrometheusMatrixResult,
  PrometheusVectorResult,
} from './types';

import {
  PrometheusBaseUrl,
  PrometheusClient,
} from './PrometheusClient';
import {
  buildNetRuntimeRequestSet,
  type NetRuntimeRequestBuildResult,
  type PrometheusRequestDescriptor,
} from './netRuntimeRequestBuilder';

export type NetRuntimePanelRange = '15m' | '1h' | '6h' | '24h';
export type NetRuntimeQueryValue =
  | PrometheusMatrixResult[]
  | PrometheusVectorResult[];

export interface NetRuntimeChartSeries {
  data: Array<[number, number]>;
  name: string;
  showSymbol: false;
  smooth: true;
  type: 'line';
}

export interface NetRuntimeChartData {
  key:
    | 'fragmentation'
    | 'fragmentationRate'
    | 'gcCollections'
    | 'gcPause'
    | 'heapSize'
    | 'jit'
    | 'threadPool';
  series: NetRuntimeChartSeries[];
}

export interface NetRuntimeStatCard {
  error: string;
  key:
    | 'cpu'
    | 'gc'
    | 'lockContentions'
    | 'memory'
    | 'process'
    | 'thread'
    | 'threadPool'
    | 'timer';
  subtitle: string;
  title: string;
  value: string;
}

export interface NetRuntimeAlert {
  level: 'danger' | 'warning';
  text: string;
}

export interface NetRuntimePanelContext {
  end: number;
  rangeSeconds: number;
  start: number;
  step: number;
  targetLabels: Record<string, string>;
  window: NetRuntimePanelRange;
}

export interface FetchNetRuntimePanelDataOptions {
  baseUrl?: string;
  client?: PrometheusClient;
  includeProcessInstrumentation?: boolean;
  queryTime?: null | number;
  showProcessInstrumentation?: boolean;
  targetLabels: Record<string, string>;
  timeRange: NetRuntimePanelRange;
}

export interface NetRuntimePanelData {
  alerts: NetRuntimeAlert[];
  cards: NetRuntimeStatCard[];
  charts: Record<NetRuntimeChartData['key'], NetRuntimeChartData>;
  context: NetRuntimePanelContext;
  errors: Record<string, string>;
  raw: Record<string, NetRuntimeQueryValue>;
  requests: NetRuntimeRequestBuildResult;
}

const rangeSecondsMap: Record<NetRuntimePanelRange, number> = {
  '15m': 15 * 60,
  '1h': 60 * 60,
  '6h': 6 * 60 * 60,
  '24h': 24 * 60 * 60,
};

const rangeStepMap: Record<NetRuntimePanelRange, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

export function getNetRuntimeRangeParams(
  range: NetRuntimePanelRange,
  queryTime?: null | number,
) {
  const step = rangeStepMap[range];
  const baseTime =
    typeof queryTime === 'number' && Number.isFinite(queryTime)
      ? queryTime
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  const start = end - rangeSecondsMap[range];
  return { end, start, step };
}

export function buildNetRuntimePanelRequests(
  options: FetchNetRuntimePanelDataOptions,
) {
  const instanceName =
    options.targetLabels.exported_instance || options.targetLabels.instance || '';

  if (!instanceName) {
    throw new Error('Missing exported_instance target label.');
  }

  const jobName =
    options.targetLabels.exported_job || options.targetLabels.job || undefined;
  const { end, start, step } = getNetRuntimeRangeParams(
    options.timeRange,
    options.queryTime,
  );

  return buildNetRuntimeRequestSet({
    baseUrl: options.baseUrl || PrometheusBaseUrl,
    endTime: end,
    includeProcessInstrumentation:
      options.includeProcessInstrumentation ?? true,
    instanceName,
    jobName,
    startTime: start,
    step,
  });
}

async function fetchDescriptor(
  client: PrometheusClient,
  descriptor: PrometheusRequestDescriptor,
) {
  if (descriptor.kind === 'instant') {
    return client.query(descriptor.query, Number(descriptor.params.time));
  }

  return client.queryRange({
    end: Number(descriptor.params.end),
    query: descriptor.query,
    start: Number(descriptor.params.start),
    step: Number(descriptor.params.step),
  });
}

function parseNumber(value?: [number, string] | null) {
  if (!value) {
    return null;
  }
  const num = Number(value[1]);
  return Number.isFinite(num) ? num : null;
}

function getVectorData(
  raw: Record<string, NetRuntimeQueryValue>,
  key: string,
) {
  return (raw[key] || []) as PrometheusVectorResult[];
}

function getMatrixData(
  raw: Record<string, NetRuntimeQueryValue>,
  key: string,
) {
  return (raw[key] || []) as PrometheusMatrixResult[];
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

function buildGenerationLabel(metric: Record<string, string>) {
  return metric.gc_heap_generation || metric.generation || 'unknown';
}

function formatBytes(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
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
    return '--';
  }
  if (value >= 1000) {
    return `${(value / 1000).toFixed(2)} s`;
  }
  return `${value.toFixed(2)} ms`;
}

function formatNumber(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
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
    return '--';
  }
  return `${(value / 1024 / 1024).toFixed(2)} MiB`;
}

function toSeriesByLabel(
  results: PrometheusMatrixResult[],
  nameResolver: (metric: Record<string, string>) => string,
  valueMapper?: (value: number) => number,
): NetRuntimeChartSeries[] {
  return results.map((item) => ({
    data: item.values.map(([ts, raw]) => {
      const num = Number(raw);
      const value = Number.isFinite(num) ? num : 0;
      return [
        ts * 1000,
        valueMapper ? valueMapper(value) : value,
      ] as [number, number];
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

function buildFragRateSeries(raw: Record<string, NetRuntimeQueryValue>) {
  const fragRows = getMatrixData(raw, 'fragSizeRange');
  const heapRows = getMatrixData(raw, 'heapSizeRange');
  const heapByGen = new Map(
    heapRows.map((item) => [buildGenerationLabel(item.metric), item]),
  );

  const series: NetRuntimeChartSeries[] = [];
  for (const fragItem of fragRows) {
    const generation = buildGenerationLabel(fragItem.metric);
    const heapItem = heapByGen.get(generation);
    if (!heapItem) {
      continue;
    }

    const heapMap = new Map(
      heapItem.values.map(([ts, value]) => [ts, Number(value)]),
    );
    series.push({
      data: fragItem.values.map(([ts, rawValue]) => {
        const frag = Number(rawValue);
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

function buildCards(
  raw: Record<string, NetRuntimeQueryValue>,
  errors: Record<string, string>,
  context: NetRuntimePanelContext,
  showProcessInstrumentation: boolean,
): NetRuntimeStatCard[] {
  const gcGen0Inc = getVectorSum(getVectorData(raw, 'gcGen0Inc'));
  const gcGen1Inc = getVectorSum(getVectorData(raw, 'gcGen1Inc'));
  const gcGen2Inc = getVectorSum(getVectorData(raw, 'gcGen2Inc'));
  const gcPauseSeconds = getVectorSum(getVectorData(raw, 'gcPauseInc'));
  const gcPauseIncMs =
    gcPauseSeconds === null ? null : gcPauseSeconds * 1000;
  const gcAllocatedInc = getVectorSum(getVectorData(raw, 'gcAllocatedInc'));
  const lockContentionsInc = getVectorSum(
    getVectorData(raw, 'lockContentionsInc'),
  );
  const tpQueueLength = getVectorMax(getVectorData(raw, 'tpQueueLength'));
  const tpWorkItemInc = getVectorSum(getVectorData(raw, 'tpWorkItemInc'));
  const timerCount = getVectorMax(getVectorData(raw, 'timerCount'));
  const dotnetCpuCount = getVectorMax(getVectorData(raw, 'cpuCountDotnet'));
  const processCpuCount = getVectorMax(getVectorData(raw, 'processCpuCount'));
  const cpuCount = dotnetCpuCount ?? processCpuCount;
  const cpuTimeRows = getVectorData(raw, 'cpuTimeIncDotnet');
  const totalCpuDelta = cpuTimeRows
    .map((item) => parseNumber(item.value))
    .filter((value): value is number => value !== null)
    .reduce((acc, value) => acc + value, 0);
  const avgCores =
    cpuCount && Number.isFinite(totalCpuDelta)
      ? totalCpuDelta / context.rangeSeconds
      : null;
  const cpuPercent =
    avgCores !== null && cpuCount ? (avgCores / cpuCount) * 100 : null;
  const workingSet = getVectorMax(getVectorData(raw, 'workingSet'));
  const processMemoryUsage = getVectorMax(
    getVectorData(raw, 'processMemoryUsage'),
  );
  const virtualMemory = getVectorMax(getVectorData(raw, 'virtualMemory'));
  const processThreadCount = getVectorMax(
    getVectorData(raw, 'processThreadCount'),
  );
  const threadPoolThreadCount = getVectorMax(
    getVectorData(raw, 'tpThreadCount'),
  );

  const cards: NetRuntimeStatCard[] = [
    {
      error: errors.cpuCountDotnet || errors.cpuTimeIncDotnet || '',
      key: 'cpu',
      subtitle: `平均占用核数 ${formatNumber(avgCores)}`,
      title: 'CPU',
      value:
        cpuPercent === null
          ? '--'
          : `${cpuPercent.toFixed(2)}% / ${formatNumber(cpuCount)} 核`,
    },
    {
      error: errors.workingSet || errors.virtualMemory || '',
      key: 'memory',
      subtitle: `虚拟内存 ${formatBytes(virtualMemory)}`,
      title: '内存',
      value: formatBytes(workingSet ?? processMemoryUsage),
    },
    {
      error: errors.processThreadCount || errors.tpThreadCount || '',
      key: 'thread',
      subtitle:
        processThreadCount === null && threadPoolThreadCount !== null
          ? '线程池线程数'
          : '进程线程数',
      title: '线程',
      value: formatNumber(processThreadCount ?? threadPoolThreadCount),
    },
    {
      error:
        errors.gcGen0Inc ||
        errors.gcGen1Inc ||
        errors.gcGen2Inc ||
        errors.gcPauseInc ||
        errors.gcAllocatedInc ||
        '',
      key: 'gc',
      subtitle: `Pause ${formatDurationMs(gcPauseIncMs)} / 分配 ${formatMiB(gcAllocatedInc)}`,
      title: 'GC',
      value: `gen0 ${formatNumber(gcGen0Inc)} | gen1 ${formatNumber(gcGen1Inc)} | gen2 ${formatNumber(gcGen2Inc)}`,
    },
    {
      error: errors.lockContentionsInc || '',
      key: 'lockContentions',
      subtitle: `窗口 ${context.window}`,
      title: '锁竞争',
      value: `${formatNumber(lockContentionsInc)} 次`,
    },
    {
      error: errors.tpQueueLength || errors.tpWorkItemInc || '',
      key: 'threadPool',
      subtitle: `work_item 增量 ${formatNumber(tpWorkItemInc)}`,
      title: '线程池积压',
      value: `queue ${formatNumber(tpQueueLength)}`,
    },
    {
      error: errors.timerCount || '',
      key: 'timer',
      subtitle: '当前值',
      title: 'Timer 数',
      value: formatNumber(timerCount),
    },
  ];

  if (showProcessInstrumentation) {
    cards.push({
      error: errors.processMemoryUsage || errors.processCpuTimeInc || '',
      key: 'process',
      subtitle: `CPU 累积增量 ${formatNumber(getVectorSum(getVectorData(raw, 'processCpuTimeInc')))} s`,
      title: 'Process 补充',
      value: `内存 ${formatBytes(processMemoryUsage)}`,
    });
  }

  return cards;
}

function buildAlerts(
  raw: Record<string, NetRuntimeQueryValue>,
  context: NetRuntimePanelContext,
) {
  const alerts: NetRuntimeAlert[] = [];
  const gen2 = getVectorSum(getVectorData(raw, 'gcGen2Inc'));
  const gcPauseSeconds = getVectorSum(getVectorData(raw, 'gcPauseInc'));
  const pauseMs = gcPauseSeconds === null ? null : gcPauseSeconds * 1000;
  const locks = getVectorSum(getVectorData(raw, 'lockContentionsInc'));
  const queue = getVectorMax(getVectorData(raw, 'tpQueueLength'));
  const lohSeries = getMatrixData(raw, 'heapSizeRange').find(
    (item) => buildGenerationLabel(item.metric) === 'loh',
  );

  if (gen2 !== null && gen2 > 10) {
    alerts.push({
      level: 'warning',
      text: 'gen2 增量较高，可能内存压力/晋升增多',
    });
  }

  if (lohSeries && lohSeries.values.length >= 2) {
    const firstPoint = lohSeries.values[0];
    const first = Number(firstPoint?.[1]);
    const last = Number(lohSeries.values.at(-1)?.[1]);
    if (
      Number.isFinite(first) &&
      Number.isFinite(last) &&
      last > first * 1.2
    ) {
      alerts.push({
        level: 'warning',
        text: 'LOH 堆持续上升，可能存在大对象/缓存未释放风险',
      });
    }
  }

  if (pauseMs !== null) {
    const perMin = pauseMs / Math.max(1, context.rangeSeconds / 60);
    if (perMin > 100) {
      alerts.push({ level: 'danger', text: 'GC 暂停偏高' });
    }
  }

  if (locks !== null && locks > 100) {
    alerts.push({ level: 'warning', text: '锁竞争升高' });
  }

  if (queue !== null && queue > 0) {
    alerts.push({
      level: 'warning',
      text: '线程池积压，queue_length 持续 > 0',
    });
  }

  return alerts;
}

function buildCharts(
  raw: Record<string, NetRuntimeQueryValue>,
): NetRuntimePanelData['charts'] {
  return {
    fragmentation: {
      key: 'fragmentation',
      series: toSeriesByLabel(
        getMatrixData(raw, 'fragSizeRange'),
        (metric) => buildGenerationLabel(metric).toUpperCase(),
        (value) => value / 1024 / 1024,
      ),
    },
    fragmentationRate: {
      key: 'fragmentationRate',
      series: buildFragRateSeries(raw),
    },
    gcCollections: {
      key: 'gcCollections',
      series: toSeriesByLabel(getMatrixData(raw, 'gcCollectionsIncRange'), (metric) =>
        buildGenerationLabel(metric).toUpperCase(),
      ),
    },
    gcPause: {
      key: 'gcPause',
      series: [
        {
          data: sumRangeSeries(getMatrixData(raw, 'gcPauseIncRange')).map(
            ([ts, value]) => [ts, value * 1000] as [number, number],
          ),
          name: 'Pause',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
      ],
    },
    heapSize: {
      key: 'heapSize',
      series: toSeriesByLabel(
        getMatrixData(raw, 'heapSizeRange'),
        (metric) => buildGenerationLabel(metric).toUpperCase(),
        (value) => value / 1024 / 1024,
      ),
    },
    jit: {
      key: 'jit',
      series: [
        {
          data: sumRangeSeries(getMatrixData(raw, 'jitMethodsIncRange')),
          name: 'compiled_methods increase',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
        {
          data: sumRangeSeries(getMatrixData(raw, 'jitIlBytesIncRange')).map(
            ([ts, value]) => [ts, value / 1024] as [number, number],
          ),
          name: 'compiled_il_size increase(KiB)',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
        {
          data: sumRangeSeries(getMatrixData(raw, 'jitTimeIncRange')).map(
            ([ts, value]) => [ts, value * 1000] as [number, number],
          ),
          name: 'jit_time increase(ms)',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
      ],
    },
    threadPool: {
      key: 'threadPool',
      series: [
        {
          data: sumRangeSeries(getMatrixData(raw, 'tpQueueRange')),
          name: 'queue_length',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
        {
          data: sumRangeSeries(getMatrixData(raw, 'tpThreadRange')),
          name: 'thread_count',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
        {
          data: sumRangeSeries(getMatrixData(raw, 'tpWorkIncRange')),
          name: 'work_item_count increase',
          showSymbol: false,
          smooth: true,
          type: 'line',
        },
      ],
    },
  };
}

export async function fetchNetRuntimePanelData(
  options: FetchNetRuntimePanelDataOptions,
): Promise<NetRuntimePanelData> {
  const client = options.client || new PrometheusClient(options.baseUrl);
  const requests = buildNetRuntimePanelRequests(options);
  const context: NetRuntimePanelContext = {
    end: requests.end,
    rangeSeconds: Math.max(1, requests.end - requests.start),
    start: requests.start,
    step: requests.step,
    targetLabels: requests.targetLabels,
    window: options.timeRange,
  };
  const descriptors = [
    ...requests.instantRequests,
    ...requests.rangeRequests,
  ];
  const settled = await Promise.all(
    descriptors.map(async (descriptor) => {
      try {
        const data = await fetchDescriptor(client, descriptor);
        return {
          data,
          error: '',
          key: descriptor.key,
        };
      } catch (error) {
        return {
          data: [] as NetRuntimeQueryValue,
          error: error instanceof Error ? error.message : String(error),
          key: descriptor.key,
        };
      }
    }),
  );

  const raw: Record<string, NetRuntimeQueryValue> = {};
  const errors: Record<string, string> = {};

  for (const row of settled) {
    raw[row.key] = row.data;
    if (row.error) {
      errors[row.key] = row.error;
    }
  }

  return {
    alerts: buildAlerts(raw, context),
    cards: buildCards(
      raw,
      errors,
      context,
      options.showProcessInstrumentation ?? false,
    ),
    charts: buildCharts(raw),
    context,
    errors,
    raw,
    requests,
  };
}
