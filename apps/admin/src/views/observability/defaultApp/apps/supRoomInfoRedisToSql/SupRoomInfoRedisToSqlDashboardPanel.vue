<script lang="ts" setup>
import type {
  PrometheusMatrixResult,
  PrometheusValue,
  PrometheusVectorResult,
} from '#/api/prometheus/types';
import type { RuntimeRange, AppLatestMetricValue, AppViewModel } from '../../types';

type EChartsOption = Record<string, any>;

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, ref, watch } from 'vue';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import {
  formatHeartbeat,
  formatSeconds,
  getRuntimeQueryTime,
  getRuntimeTargetLabels,
} from '../../appPanelUtils';

defineOptions({ name: 'SupRoomInfoRedisToSqlDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
}>();

interface HistogramSummaryRow {
  average: string;
  count: string;
  key: string;
  total: string;
  upperBound: string;
}

interface DurationQuantileRow {
  averageMs: string;
  description: string;
  p50Ms: string;
  p95Ms: string;
  p99Ms: string;
  samples: string;
  step: string;
}

interface DedupCacheRow {
  description: string;
  result: string;
  total: string;
}

interface LineSeries {
  color: string;
  data: Array<[number, number]>;
  key: string;
  name: string;
}

interface MetricCatalogRow {
  description: string;
  labels: string;
  metricName: string;
  type: string;
}

interface ParsedMetricRow {
  labels: Record<string, string>;
  labelsText: string;
  metricName: string;
  numericValue: null | number;
  valueText: string;
}

interface SourceSummaryRow {
  averageBatch: string;
  batchCount: string;
  batchUpperBound: string;
  emptyTotal: string;
  fetchTotal: string;
  source: string;
}

interface StepSummaryRow {
  description: string;
  errorTotal: string;
  step: string;
  throughputTotal: string;
}

interface SummaryCard {
  label: string;
  value: string;
}

interface TrendContext {
  end: number;
  start: number;
  step: number;
  window: string;
}

const METRIC_NAME_ALIASES = {
  batchSize: [
    'sup_room_info_ingestion_batch_size_rooms',
    'sup_room_info_ingestion_batch_size',
  ],
  completed: [
    'sup_room_info_ingestion_completed_rooms',
    'sup_room_info_ingestion_completed',
  ],
  dedupCacheCount: ['sup_room_info_ingestion_dedup_cache_count_items'],
  dedupCacheHitRate: ['sup_room_info_ingestion_dedup_cache_hit_rate_ratio'],
  dedupCacheTotal: ['sup_room_info_ingestion_dedup_cache_times_total'],
  duration: [
    'sup_room_info_ingestion_duration_milliseconds',
    'sup_room_info_ingestion_duration',
  ],
  errorTotal: ['sup_room_info_ingestion_error_total'],
  inProgress: [
    'sup_room_info_ingestion_in_progress_rooms',
    'sup_room_info_ingestion_in_progress',
  ],
  listPending: [
    'sup_room_info_ingestion_list_pending_rooms',
    'sup_room_info_ingestion_list_pending',
  ],
  pipelineStep: [
    'sup_room_info_ingestion_pipeline_step_rooms_total',
    'sup_room_info_ingestion_pipeline_step_total',
  ],
  queueRemaining: [
    'sup_room_info_ingestion_queue_remaining_rooms',
    'sup_room_info_ingestion_queue_remaining',
  ],
  setPending: [
    'sup_room_info_ingestion_set_pending_rooms',
    'sup_room_info_ingestion_set_pending',
  ],
  sourceEmpty: [
    'sup_room_info_ingestion_source_empty_times_total',
    'sup_room_info_ingestion_source_empty_total',
  ],
  sourceFetch: [
    'sup_room_info_ingestion_source_fetch_rooms_total',
    'sup_room_info_ingestion_source_fetch_total',
  ],
} as const;

const RANGE_SECONDS_MAP: Record<RuntimeRange, number> = {
  '15m': 15 * 60,
  '1h': 60 * 60,
  '6h': 6 * 60 * 60,
  '24h': 24 * 60 * 60,
};

const RANGE_STEP_MAP: Record<RuntimeRange, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

const CHART_COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#6366f1', '#14b8a6', '#f97316', '#8b5cf6'];

const SOURCE_OPTIONS = [
  { description: '从 Redis List 拉取', value: 'list' },
  { description: '从 Redis Set 拉取', value: 'set' },
] as const;

const PIPELINE_STEP_OPTIONS = [
  { description: '生产者成功写入队列', value: 'queue_enqueued' },
  { description: '消费者完成一批写库', value: 'queue_consumed' },
  { description: '成功批量写入临时表', value: 'temp_bulk_insert' },
  { description: '成功执行存储过程同步', value: 'sync_stored_procedure' },
  { description: '成功清空临时表', value: 'temp_table_cleanup' },
] as const;

const ERROR_STEP_OPTIONS = [
  { description: '生产主循环异常', value: 'produce_loop' },
  { description: '消费批次刷库异常', value: 'consume_flush' },
  { description: 'BulkInsert 失败', value: 'temp_bulk_insert' },
  { description: '执行同步存储过程失败', value: 'sync_stored_procedure' },
  { description: '执行清空临时表失败', value: 'temp_table_cleanup' },
  { description: '从 set key 解析 SupId 失败', value: 'set_sup_id_parse' },
  { description: 'Set 源数据解析失败', value: 'set_deserialize' },
] as const;

const DURATION_STEP_OPTIONS = [
  { description: '从 List 源读取耗时', value: 'list_read' },
  { description: '从 Set 源读取耗时', value: 'set_read' },
  { description: '源协调逻辑总耗时', value: 'source_coordinate' },
  { description: '标准化耗时', value: 'normalize' },
  { description: 'BulkInsertAsync 耗时', value: 'temp_bulk_insert_duration' },
  { description: '执行同步存储过程耗时', value: 'sync_stored_procedure_duration' },
  { description: '执行清空临时表耗时', value: 'temp_table_cleanup_duration' },
  { description: '整批写库耗时', value: 'write_batch' },
] as const;

const METRIC_CATALOG: MetricCatalogRow[] = [
  {
    description: '成功从源读取到的房型累计数量',
    labels: 'source',
    metricName: 'sup_room_info_ingestion_source_fetch_rooms_total',
    type: 'Counter',
  },
  {
    description: '源读取为空的累计次数',
    labels: 'source',
    metricName: 'sup_room_info_ingestion_source_empty_times_total',
    type: 'Counter',
  },
  {
    description: '管道各步骤累计处理房型数量',
    labels: 'step',
    metricName: 'sup_room_info_ingestion_pipeline_step_rooms_total',
    type: 'Counter',
  },
  {
    description: '管道各步骤累计错误次数',
    labels: 'step',
    metricName: 'sup_room_info_ingestion_error_total',
    type: 'Counter',
  },
  {
    description: '各步骤耗时分布，单位毫秒',
    labels: 'step',
    metricName: 'sup_room_info_ingestion_duration_milliseconds',
    type: 'Histogram',
  },
  {
    description: '源读取批大小分布',
    labels: 'source',
    metricName: 'sup_room_info_ingestion_batch_size_rooms',
    type: 'Histogram',
  },
  {
    description: '当前队列剩余数量',
    labels: '-',
    metricName: 'sup_room_info_ingestion_queue_remaining_rooms',
    type: 'Gauge',
  },
  {
    description: '当前 List 源剩余待处理数量',
    labels: '-',
    metricName: 'sup_room_info_ingestion_list_pending_rooms',
    type: 'Gauge',
  },
  {
    description: '当前 Set 源剩余待处理数量',
    labels: '-',
    metricName: 'sup_room_info_ingestion_set_pending_rooms',
    type: 'Gauge',
  },
  {
    description: '当前处理中数量',
    labels: '-',
    metricName: 'sup_room_info_ingestion_in_progress_rooms',
    type: 'Gauge',
  },
  {
    description: '当前进程累计完成数量',
    labels: '-',
    metricName: 'sup_room_info_ingestion_completed_rooms',
    type: 'Gauge',
  },
  {
    description: '当前去重缓存条数',
    labels: '-',
    metricName: 'sup_room_info_ingestion_dedup_cache_count_items',
    type: 'Gauge',
  },
  {
    description: '当前进程累计命中率，计算为 hit / (hit + miss)',
    labels: '-',
    metricName: 'sup_room_info_ingestion_dedup_cache_hit_rate_ratio',
    type: 'Gauge',
  },
  {
    description: '去重缓存访问次数，result 标签区分 hit / miss',
    labels: 'result',
    metricName: 'sup_room_info_ingestion_dedup_cache_times_total',
    type: 'Counter',
  },
];

const PIPELINE_STEP_DESCRIPTIONS: Record<string, string> = {
  consume_flush: '消费批次刷库异常',
  normalize: '标准化耗时阶段',
  normalize_in: '进入标准化前累计房型',
  normalize_out: '标准化输出累计房型',
  produce_loop: '生产主循环异常',
  queue_consumed: '消费者完成一批写库',
  queue_enqueued: '生产者成功写入队列',
  set_deserialize: 'Set 源数据解析失败',
  set_sup_id_parse: '从 set key 解析 SupId 失败',
  sync_stored_procedure: '执行同步存储过程',
  sync_stored_procedure_duration: '执行同步存储过程耗时',
  temp_bulk_insert: '批量写入临时表',
  temp_bulk_insert_duration: 'BulkInsertAsync 耗时',
  temp_table_cleanup: '清空临时表',
  temp_table_cleanup_duration: '清空临时表耗时',
  write_batch: '整批写库耗时',
};

const client = new PrometheusClient();

const selectedRange = ref<RuntimeRange>('15m');
const selectedDurationStep = ref('source_coordinate');
const analyticsLoading = ref(false);
const analyticsError = ref('');
const backlogTrendSeries = ref<LineSeries[]>([]);
const sourceTrendSeries = ref<LineSeries[]>([]);
const pipelineTrendSeries = ref<LineSeries[]>([]);
const errorTrendSeries = ref<LineSeries[]>([]);
const durationTrendSeries = ref<LineSeries[]>([]);
const durationQuantiles = ref<
  Record<string, { average: null | number; p50: null | number; p95: null | number; p99: null | number }>
>({});

const backlogChartRef = ref<EchartsUIType>();
const sourceChartRef = ref<EchartsUIType>();
const pipelineChartRef = ref<EchartsUIType>();
const errorChartRef = ref<EchartsUIType>();
const durationChartRef = ref<EchartsUIType>();

const { renderEcharts: renderBacklogChart } = useEcharts(backlogChartRef);
const { renderEcharts: renderSourceChart } = useEcharts(sourceChartRef);
const { renderEcharts: renderPipelineChart } = useEcharts(pipelineChartRef);
const { renderEcharts: renderErrorChart } = useEcharts(errorChartRef);
const { renderEcharts: renderDurationChart } = useEcharts(durationChartRef);

let analyticsRequestId = 0;

function parseLabels(labelsText: string) {
  const result: Record<string, string> = {};
  for (const part of labelsText.split(', ')) {
    if (!part) {
      continue;
    }
    const index = part.indexOf('=');
    if (index < 0) {
      continue;
    }
    const key = part.slice(0, index).trim();
    const value = part.slice(index + 1).trim();
    if (key) {
      result[key] = value;
    }
  }
  return result;
}

function parseMetricValue(item: AppLatestMetricValue): ParsedMetricRow {
  const numericValue = Number(item.valueText);
  return {
    labels: parseLabels(item.labelsText),
    labelsText: item.labelsText,
    metricName: item.metricName,
    numericValue: Number.isFinite(numericValue) ? numericValue : null,
    valueText: item.valueText,
  };
}

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function buildSelector(labels: Record<string, string>, extraMatchers: string[] = []) {
  const parts = Object.entries(labels)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `{${[...parts, ...extraMatchers].join(',')}}`;
}

function buildMetricSelector(metricName: string, labels: Record<string, string>, extraMatchers: string[] = []) {
  return `${metricName}${buildSelector(labels, extraMatchers)}`;
}

function parsePrometheusNumber(value?: null | PrometheusValue) {
  if (!value) {
    return null;
  }
  const numericValue = Number(value[1]);
  return Number.isFinite(numericValue) ? numericValue : null;
}

function formatCountValue(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  if (Math.abs(value) >= 1000) {
    return formatNumber(value, 0);
  }
  return formatNumber(value, 2);
}

function formatDurationValue(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  if (value >= 1000) {
    return `${formatNumber(value / 1000, 2)} s`;
  }
  return `${formatNumber(value, 2)} ms`;
}

function formatPercent(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return `${formatNumber(value * 100, 2)}%`;
}

function getRangeParams(range: RuntimeRange, queryTime?: null | number) {
  const step = RANGE_STEP_MAP[range];
  const baseTime =
    typeof queryTime === 'number' && Number.isFinite(queryTime)
      ? queryTime
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  const start = end - RANGE_SECONDS_MAP[range];
  return {
    end,
    start,
    step,
    window: `${Math.max(step * 4, 60)}s`,
  };
}

function buildTimeline(start: number, end: number, step: number) {
  const timeline: number[] = [];
  for (let ts = start; ts <= end; ts += step) {
    timeline.push(ts * 1000);
  }
  return timeline;
}

function sumMatrixSeries(results: PrometheusMatrixResult[]) {
  const bucket = new Map<number, number>();
  for (const row of results) {
    for (const [ts, rawValue] of row.values) {
      const numericValue = Number(rawValue);
      if (!Number.isFinite(numericValue)) {
        continue;
      }
      const millis = ts * 1000;
      bucket.set(millis, (bucket.get(millis) || 0) + numericValue);
    }
  }
  return [...bucket.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([ts, value]) => [ts, value] as [number, number]);
}

function hasSeriesData(series: LineSeries[]) {
  return series.some((item) => item.data.length > 0 && item.data.some(([, value]) => value !== 0));
}

function buildSingleSeries(
  key: string,
  name: string,
  color: string,
  results: PrometheusMatrixResult[],
  timeline: number[],
) {
  const valueMap = new Map(sumMatrixSeries(results));
  return {
    color,
    data: timeline.map((timestamp) => [timestamp, valueMap.get(timestamp) || 0]) as Array<
      [number, number]
    >,
    key,
    name,
  };
}

function buildGroupedSeries(
  results: PrometheusMatrixResult[],
  labelKey: string,
  orderedKeys: string[],
  descriptions: Record<string, string>,
  timeline: number[],
) {
  const groupedMap = new Map<string, Map<number, number>>();

  for (const row of results) {
    const labelValue = row.metric[labelKey];
    if (!labelValue) {
      continue;
    }
    const pointMap = groupedMap.get(labelValue) || new Map<number, number>();
    for (const [ts, rawValue] of row.values) {
      const numericValue = Number(rawValue);
      if (!Number.isFinite(numericValue)) {
        continue;
      }
      const millis = ts * 1000;
      pointMap.set(millis, (pointMap.get(millis) || 0) + numericValue);
    }
    groupedMap.set(labelValue, pointMap);
  }

  return orderedKeys
    .map((key, index) => ({
      color: CHART_COLORS[index % CHART_COLORS.length],
      data: timeline.map((timestamp) => [timestamp, groupedMap.get(key)?.get(timestamp) || 0]) as Array<
        [number, number]
      >,
      key,
      name: descriptions[key] ? `${key} / ${descriptions[key]}` : key,
    }))
    .filter((item) => item.data.some(([, value]) => value !== 0));
}

function buildLineOption(series: LineSeries[], formatter: (value: number) => string, yAxisName: string): EChartsOption {
  return {
    color: series.map((item) => item.color),
    grid: {
      bottom: 20,
      containLabel: true,
      left: 16,
      right: 20,
      top: 28,
    },
    legend: {
      icon: 'roundRect',
      itemHeight: 8,
      itemWidth: 12,
      type: 'scroll',
    },
    series: series.map((item) => ({
      data: item.data,
      lineStyle: { width: 2 },
      name: item.name,
      showSymbol: false,
      smooth: true,
      type: 'line',
    })),
    tooltip: {
      appendToBody: true,
      backgroundColor: 'rgba(18, 18, 18, 0.88)',
      borderWidth: 0,
      formatter: (params: any[]) => {
        const rows = Array.isArray(params) ? params : [];
        const timeText = rows[0]?.axisValue
          ? dayjs(rows[0].axisValue).format('YYYY-MM-DD HH:mm:ss')
          : '--';
        const lines = [timeText];
        for (const row of rows) {
          lines.push(`${row.marker}${row.seriesName}: ${formatter(Number(row.value?.[1] ?? 0))}`);
        }
        return lines.join('<br />');
      },
      textStyle: {
        color: '#f5f7fa',
      },
      trigger: 'axis',
    },
    xAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter: (value: number) => dayjs(value).format('HH:mm'),
        hideOverlap: true,
      },
      type: 'time',
    },
    yAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter: (value: number) => formatter(value),
      },
      name: yAxisName,
      type: 'value',
    },
  };
}

function formatInteger(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 0 }).format(value);
}

function formatNumber(value: null | number, digits = 2) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: digits,
    minimumFractionDigits: 0,
  }).format(value);
}

function formatUpperBound(value: null | number, suffix: string) {
  if (value === null) {
    return '--';
  }
  if (!Number.isFinite(value)) {
    return '无限桶';
  }
  return `<= ${formatNumber(value, 0)} ${suffix}`;
}

function buildHistogramUpperBound(buckets: ParsedMetricRow[], totalCount: null | number) {
  if (!buckets.length || totalCount === null || totalCount <= 0) {
    return null;
  }

  const sorted = [...buckets].sort((a, b) => {
    const left = a.labels.le === '+Inf' ? Number.POSITIVE_INFINITY : Number(a.labels.le);
    const right = b.labels.le === '+Inf' ? Number.POSITIVE_INFINITY : Number(b.labels.le);
    return left - right;
  });

  const matched = sorted.find((row) => (row.numericValue ?? -1) >= totalCount);
  if (!matched) {
    return null;
  }

  if (matched.labels.le === '+Inf') {
    return Number.POSITIVE_INFINITY;
  }

  const upper = Number(matched.labels.le);
  return Number.isFinite(upper) ? upper : null;
}

const appMetricsRows = computed(() =>
  (props.app?.latestMetricValues || [])
    .filter((item) => item.labelsText.includes('otel_scope_name=AppMetrics'))
    .map(parseMetricValue),
);

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));
const trendContext = computed<TrendContext>(() =>
  getRangeParams(selectedRange.value, runtimeQueryTime.value),
);

function getMetricRows(metricName: string | string[], labels: Record<string, string> = {}) {
  const metricNames = Array.isArray(metricName) ? metricName : [metricName];
  return appMetricsRows.value.filter((row) => {
    if (!metricNames.includes(row.metricName)) {
      return false;
    }
    return Object.entries(labels).every(([key, value]) => row.labels[key] === value);
  });
}

function getMetricNumber(metricName: string | string[], labels: Record<string, string> = {}) {
  const row = getMetricRows(metricName, labels)[0];
  return row?.numericValue ?? null;
}

function getHistogramMetricNames(metricNames: string | readonly string[], suffix: 'bucket' | 'count' | 'sum') {
  const list = Array.isArray(metricNames) ? metricNames : [metricNames];
  return list.map((item) => `${item}_${suffix}`);
}

function extractLabelValues(metricNames: string | string[], labelKey: string) {
  return [...new Set(
    getMetricRows(metricNames)
      .map((row) => row.labels[labelKey])
      .filter((value): value is string => Boolean(value)),
  )].sort((a, b) => a.localeCompare(b));
}

function buildHistogramSummary(metricName: readonly string[] | string, labels: Record<string, string>, unitSuffix: string) {
  const count = getMetricNumber(getHistogramMetricNames(metricName, 'count'), labels);
  const sum = getMetricNumber(getHistogramMetricNames(metricName, 'sum'), labels);
  const upperBound = buildHistogramUpperBound(
    getMetricRows(getHistogramMetricNames(metricName, 'bucket'), labels),
    count,
  );

  return {
    average: count && sum !== null ? formatNumber(sum / count, 2) : '--',
    count: formatInteger(count),
    total: formatNumber(sum, 2),
    upperBound: formatUpperBound(upperBound, unitSuffix),
  };
}

const sourceOptions = computed(() => {
  const actualValues = new Set([
    ...SOURCE_OPTIONS.map((item) => item.value),
    ...extractLabelValues(METRIC_NAME_ALIASES.sourceFetch, 'source'),
    ...extractLabelValues(METRIC_NAME_ALIASES.sourceEmpty, 'source'),
    ...extractLabelValues(getHistogramMetricNames(METRIC_NAME_ALIASES.batchSize, 'count'), 'source'),
  ]);

  return [...actualValues]
    .sort((a, b) => a.localeCompare(b))
    .map((value) => ({
      description: SOURCE_OPTIONS.find((item) => item.value === value)?.description || '实际源标签',
      value,
    }));
});

const durationStepOptions = computed(() => {
  const actualValues = new Set([
    ...DURATION_STEP_OPTIONS.map((item) => item.value),
    ...extractLabelValues(getHistogramMetricNames(METRIC_NAME_ALIASES.duration, 'count'), 'step'),
  ]);

  return [...actualValues]
    .sort((a, b) => a.localeCompare(b))
    .map((value) => ({
      description:
        DURATION_STEP_OPTIONS.find((item) => item.value === value)?.description ||
        PIPELINE_STEP_DESCRIPTIONS[value] ||
        '实际耗时步骤',
      value,
    }));
});

const pipelineStepOptions = computed(() => {
  const actualValues = new Set([
    ...PIPELINE_STEP_OPTIONS.map((item) => item.value),
    ...ERROR_STEP_OPTIONS.map((item) => item.value),
    ...extractLabelValues(METRIC_NAME_ALIASES.pipelineStep, 'step'),
    ...extractLabelValues(METRIC_NAME_ALIASES.errorTotal, 'step'),
  ]);

  return [...actualValues]
    .sort((a, b) => a.localeCompare(b))
    .map((value) => ({
      description:
        PIPELINE_STEP_OPTIONS.find((item) => item.value === value)?.description ||
        ERROR_STEP_OPTIONS.find((item) => item.value === value)?.description ||
        PIPELINE_STEP_DESCRIPTIONS[value] ||
        '实际步骤标签',
      value,
    }));
});

watch(
  durationStepOptions,
  (options) => {
    if (!options.some((item) => item.value === selectedDurationStep.value)) {
      selectedDurationStep.value =
        options.find((item) => item.value === 'source_coordinate')?.value ||
        options[0]?.value ||
        'source_coordinate';
    }
  },
  { immediate: true },
);

const overviewCards = computed<SummaryCard[]>(() => [
  {
    label: '队列积压',
    value: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.queueRemaining)),
  },
  {
    label: '当前处理中',
    value: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.inProgress)),
  },
  {
    label: '本次运行完成量',
    value: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.completed)),
  },
  {
    label: 'List 待处理',
    value: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.listPending)),
  },
  {
    label: 'Set 待处理',
    value: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.setPending)),
  },
  {
    label: '累计拉取总量',
    value: formatInteger(
      sourceOptions.value.reduce(
        (sum, source) => sum + (getMetricNumber(METRIC_NAME_ALIASES.sourceFetch, { source: source.value }) || 0),
        0,
      ),
    ),
  },
  {
    label: '去重缓存条数',
    value: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.dedupCacheCount)),
  },
  {
    label: '去重缓存命中率',
    value: formatPercent(getMetricNumber(METRIC_NAME_ALIASES.dedupCacheHitRate)),
  },
]);

const dedupCacheRows = computed<DedupCacheRow[]>(() => [
  {
    description: '缓存命中次数',
    result: 'hit',
    total: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.dedupCacheTotal, { result: 'hit' })),
  },
  {
    description: '缓存未命中次数',
    result: 'miss',
    total: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.dedupCacheTotal, { result: 'miss' })),
  },
]);

const sourceSummaryRows = computed<SourceSummaryRow[]>(() =>
  sourceOptions.value.map((source) => {
    const batchSummary = buildHistogramSummary(
      METRIC_NAME_ALIASES.batchSize,
      { source: source.value },
      'rooms',
    );

    return {
      averageBatch: batchSummary.average,
      batchCount: batchSummary.count,
      batchUpperBound: batchSummary.upperBound,
      emptyTotal: formatInteger(
        getMetricNumber(METRIC_NAME_ALIASES.sourceEmpty, { source: source.value }),
      ),
      fetchTotal: formatInteger(
        getMetricNumber(METRIC_NAME_ALIASES.sourceFetch, { source: source.value }),
      ),
      source: source.value,
    };
  }),
);

const stepDescriptionMap = new Map<string, string>(
  Object.entries(PIPELINE_STEP_DESCRIPTIONS),
);

const pipelineStepRows = computed<StepSummaryRow[]>(() => {
  return pipelineStepOptions.value
    .map((step) => ({
      description: step.description,
      errorTotal: formatInteger(getMetricNumber(METRIC_NAME_ALIASES.errorTotal, { step: step.value })),
      step: step.value,
      throughputTotal: formatInteger(
        getMetricNumber(METRIC_NAME_ALIASES.pipelineStep, { step: step.value }),
      ),
    }))
    .sort((a, b) => a.step.localeCompare(b.step));
});

const durationRows = computed<HistogramSummaryRow[]>(() =>
  durationStepOptions.value.map((item) => {
    const summary = buildHistogramSummary(
      METRIC_NAME_ALIASES.duration,
      { step: item.value },
      'ms',
    );

    return {
      average: summary.average,
      count: summary.count,
      key: item.value,
      total: summary.total,
      upperBound: summary.upperBound,
    };
  }),
);

const batchSizeRows = computed<HistogramSummaryRow[]>(() =>
  sourceOptions.value.map((item) => {
    const summary = buildHistogramSummary(
      METRIC_NAME_ALIASES.batchSize,
      { source: item.value },
      'rooms',
    );

    return {
      average: summary.average,
      count: summary.count,
      key: item.value,
      total: summary.total,
      upperBound: summary.upperBound,
    };
  }),
);

const durationQuantileRows = computed<DurationQuantileRow[]>(() =>
  durationStepOptions.value.map((item) => ({
    averageMs: formatDurationValue(durationQuantiles.value[item.value]?.average ?? null),
    description: item.description,
    p50Ms: formatDurationValue(durationQuantiles.value[item.value]?.p50 ?? null),
    p95Ms: formatDurationValue(durationQuantiles.value[item.value]?.p95 ?? null),
    p99Ms: formatDurationValue(durationQuantiles.value[item.value]?.p99 ?? null),
    samples: durationRows.value.find((row) => row.key === item.value)?.count || '--',
    step: item.value,
  })),
);

const hasBacklogTrend = computed(() => hasSeriesData(backlogTrendSeries.value));
const hasSourceTrend = computed(() => hasSeriesData(sourceTrendSeries.value));
const hasPipelineTrend = computed(() => hasSeriesData(pipelineTrendSeries.value));
const hasErrorTrend = computed(() => hasSeriesData(errorTrendSeries.value));
const hasDurationTrend = computed(() => hasSeriesData(durationTrendSeries.value));

async function queryInstant(query: string) {
  return client.query(query, trendContext.value.end);
}

async function queryRange(query: string) {
  return client.queryRange({
    end: trendContext.value.end,
    query,
    start: trendContext.value.start,
    step: trendContext.value.step,
  });
}

function mapVectorByLabel(results: PrometheusVectorResult[], labelKey: string) {
  const result = new Map<string, number>();
  for (const row of results) {
    const labelValue = row.metric[labelKey];
    const value = parsePrometheusNumber(row.value);
    if (!labelValue || value === null) {
      continue;
    }
    result.set(labelValue, value);
  }
  return result;
}

async function renderCharts() {
  await nextTick();

  if (hasBacklogTrend.value) {
    renderBacklogChart(
      buildLineOption(backlogTrendSeries.value, (value) => formatCountValue(value), 'rooms'),
    );
  }

  if (hasSourceTrend.value) {
    renderSourceChart(
      buildLineOption(sourceTrendSeries.value, (value) => formatCountValue(value), 'rooms / 窗口增量'),
    );
  }

  if (hasPipelineTrend.value) {
    renderPipelineChart(
      buildLineOption(pipelineTrendSeries.value, (value) => formatCountValue(value), 'rooms / 窗口增量'),
    );
  }

  if (hasErrorTrend.value) {
    renderErrorChart(
      buildLineOption(errorTrendSeries.value, (value) => formatCountValue(value), 'errors / 窗口增量'),
    );
  }

  if (hasDurationTrend.value) {
    renderDurationChart(
      buildLineOption(durationTrendSeries.value, (value) => formatDurationValue(value), 'ms'),
    );
  }
}

async function loadAnalytics() {
  if (!props.app) {
    backlogTrendSeries.value = [];
    sourceTrendSeries.value = [];
    pipelineTrendSeries.value = [];
    errorTrendSeries.value = [];
    durationTrendSeries.value = [];
    durationQuantiles.value = {};
    return;
  }

  const requestId = ++analyticsRequestId;
  analyticsLoading.value = true;
  analyticsError.value = '';

  try {
    const labels = targetLabels.value;
    const timeline = buildTimeline(trendContext.value.start, trendContext.value.end, trendContext.value.step);
    const durationBucketSelector = buildMetricSelector(
      `${METRIC_NAME_ALIASES.duration[0]}_bucket`,
      labels,
    );
    const durationCountSelector = buildMetricSelector(
      `${METRIC_NAME_ALIASES.duration[0]}_count`,
      labels,
    );
    const durationSumSelector = buildMetricSelector(
      `${METRIC_NAME_ALIASES.duration[0]}_sum`,
      labels,
    );
    const focusedDurationBucketSelector = buildMetricSelector(
      `${METRIC_NAME_ALIASES.duration[0]}_bucket`,
      labels,
      [`step="${escapeLabelValue(selectedDurationStep.value)}"`],
    );
    const focusedDurationCountSelector = buildMetricSelector(
      `${METRIC_NAME_ALIASES.duration[0]}_count`,
      labels,
      [`step="${escapeLabelValue(selectedDurationStep.value)}"`],
    );
    const focusedDurationSumSelector = buildMetricSelector(
      `${METRIC_NAME_ALIASES.duration[0]}_sum`,
      labels,
      [`step="${escapeLabelValue(selectedDurationStep.value)}"`],
    );

    const [
      queueRemainingResults,
      listPendingResults,
      setPendingResults,
      inProgressResults,
      sourceFetchTrendResults,
      pipelineTrendResultsRaw,
      errorTrendResultsRaw,
      durationAverageResults,
      durationP50Results,
      durationP95Results,
      durationP99Results,
      focusedAverageResults,
      focusedP50Results,
      focusedP95Results,
      focusedP99Results,
    ] = await Promise.all([
      queryRange(buildMetricSelector(METRIC_NAME_ALIASES.queueRemaining[0], labels)),
      queryRange(buildMetricSelector(METRIC_NAME_ALIASES.listPending[0], labels)),
      queryRange(buildMetricSelector(METRIC_NAME_ALIASES.setPending[0], labels)),
      queryRange(buildMetricSelector(METRIC_NAME_ALIASES.inProgress[0], labels)),
      queryRange(
        `sum by (source) (increase(${buildMetricSelector(METRIC_NAME_ALIASES.sourceFetch[0], labels)}[${trendContext.value.window}]))`,
      ),
      queryRange(
        `sum by (step) (increase(${buildMetricSelector(METRIC_NAME_ALIASES.pipelineStep[0], labels)}[${trendContext.value.window}]))`,
      ),
      queryRange(
        `sum by (step) (increase(${buildMetricSelector(METRIC_NAME_ALIASES.errorTotal[0], labels)}[${trendContext.value.window}]))`,
      ),
      queryInstant(
        `sum by (step) (rate(${durationSumSelector}[5m])) / sum by (step) (rate(${durationCountSelector}[5m]))`,
      ),
      queryInstant(
        `histogram_quantile(0.5, sum by (le, step) (rate(${durationBucketSelector}[5m])))`,
      ),
      queryInstant(
        `histogram_quantile(0.95, sum by (le, step) (rate(${durationBucketSelector}[5m])))`,
      ),
      queryInstant(
        `histogram_quantile(0.99, sum by (le, step) (rate(${durationBucketSelector}[5m])))`,
      ),
      queryRange(
        `sum(rate(${focusedDurationSumSelector}[5m])) / sum(rate(${focusedDurationCountSelector}[5m]))`,
      ),
      queryRange(
        `histogram_quantile(0.5, sum by (le) (rate(${focusedDurationBucketSelector}[5m])))`,
      ),
      queryRange(
        `histogram_quantile(0.95, sum by (le) (rate(${focusedDurationBucketSelector}[5m])))`,
      ),
      queryRange(
        `histogram_quantile(0.99, sum by (le) (rate(${focusedDurationBucketSelector}[5m])))`,
      ),
    ]);

    if (requestId !== analyticsRequestId) {
      return;
    }

    backlogTrendSeries.value = [
      buildSingleSeries('queue_remaining', '队列积压', CHART_COLORS[0], queueRemainingResults, timeline),
      buildSingleSeries('list_pending', 'List 待处理', CHART_COLORS[1], listPendingResults, timeline),
      buildSingleSeries('set_pending', 'Set 待处理', CHART_COLORS[2], setPendingResults, timeline),
      buildSingleSeries('in_progress', '当前处理中', CHART_COLORS[3], inProgressResults, timeline),
    ].filter((item) => item.data.some(([, value]) => value !== 0));

    sourceTrendSeries.value = buildGroupedSeries(
      sourceFetchTrendResults,
      'source',
      sourceOptions.value.map((item) => item.value),
      Object.fromEntries(sourceOptions.value.map((item) => [item.value, item.description])),
      timeline,
    );

    pipelineTrendSeries.value = buildGroupedSeries(
      pipelineTrendResultsRaw,
      'step',
      pipelineStepOptions.value.map((item) => item.value),
      Object.fromEntries(pipelineStepOptions.value.map((item) => [item.value, item.description])),
      timeline,
    );

    errorTrendSeries.value = buildGroupedSeries(
      errorTrendResultsRaw,
      'step',
      pipelineStepOptions.value.map((item) => item.value),
      Object.fromEntries(pipelineStepOptions.value.map((item) => [item.value, item.description])),
      timeline,
    );

    const averageMap = mapVectorByLabel(durationAverageResults, 'step');
    const p50Map = mapVectorByLabel(durationP50Results, 'step');
    const p95Map = mapVectorByLabel(durationP95Results, 'step');
    const p99Map = mapVectorByLabel(durationP99Results, 'step');

    durationQuantiles.value = Object.fromEntries(
      durationStepOptions.value.map((item) => [
        item.value,
        {
          average: averageMap.get(item.value) ?? null,
          p50: p50Map.get(item.value) ?? null,
          p95: p95Map.get(item.value) ?? null,
          p99: p99Map.get(item.value) ?? null,
        },
      ]),
    );

    durationTrendSeries.value = [
      buildSingleSeries('avg', '平均值', CHART_COLORS[0], focusedAverageResults, timeline),
      buildSingleSeries('p50', 'P50', CHART_COLORS[1], focusedP50Results, timeline),
      buildSingleSeries('p95', 'P95', CHART_COLORS[2], focusedP95Results, timeline),
      buildSingleSeries('p99', 'P99', CHART_COLORS[3], focusedP99Results, timeline),
    ].filter((item) => item.data.some(([, value]) => value !== 0));

    await renderCharts();
  } catch (error) {
    if (requestId !== analyticsRequestId) {
      return;
    }
    analyticsError.value = error instanceof Error ? error.message : 'Prometheus 查询失败';
    backlogTrendSeries.value = [];
    sourceTrendSeries.value = [];
    pipelineTrendSeries.value = [];
    errorTrendSeries.value = [];
    durationTrendSeries.value = [];
  } finally {
    if (requestId === analyticsRequestId) {
      analyticsLoading.value = false;
    }
  }
}

watch(
  () => [
    selectedRange.value,
    selectedDurationStep.value,
    runtimeQueryTime.value,
    JSON.stringify(targetLabels.value),
  ],
  () => {
    void loadAnalytics();
  },
  { immediate: true },
);
</script>

<template>
  <div class="sup-room-dashboard-panel">
    <div class="metrics-grid">
      <el-card shadow="never">
        <div class="metric-label">最近心跳</div>
        <div class="metric-value">{{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">运行时长</div>
        <div class="metric-value">{{ formatSeconds(props.app?.uptimeSeconds ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">当前 Inflight</div>
        <div class="metric-value">{{ formatInteger(props.app?.inflight ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">AppMetrics 指标数</div>
        <div class="metric-value">{{ formatInteger(appMetricsRows.length) }}</div>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>总览卡片</template>
      <div class="summary-grid">
        <div v-for="item in overviewCards" :key="item.label" class="summary-item">
          <div class="summary-label">{{ item.label }}</div>
          <div class="summary-value">{{ item.value }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>
        <div class="section-header">
          <span>趋势分析</span>
          <el-radio-group v-model="selectedRange" size="small">
            <el-radio-button label="15m">15m</el-radio-button>
            <el-radio-button label="1h">1h</el-radio-button>
            <el-radio-button label="6h">6h</el-radio-button>
            <el-radio-button label="24h">24h</el-radio-button>
          </el-radio-group>
        </div>
      </template>

      <el-alert
        v-if="analyticsError"
        :closable="false"
        :title="analyticsError"
        class="mb-3"
        type="error"
      />

      <div v-loading="analyticsLoading" class="charts-grid">
        <div class="chart-panel">
          <div class="chart-title">积压与处理中趋势</div>
          <el-empty v-if="!hasBacklogTrend" description="暂无趋势数据" :image-size="72" />
          <EchartsUI v-else ref="backlogChartRef" class="chart" />
        </div>

        <div class="chart-panel">
          <div class="chart-title">来源吞吐趋势</div>
          <el-empty v-if="!hasSourceTrend" description="暂无来源趋势" :image-size="72" />
          <EchartsUI v-else ref="sourceChartRef" class="chart" />
        </div>

        <div class="chart-panel">
          <div class="chart-title">管道吞吐趋势</div>
          <el-empty v-if="!hasPipelineTrend" description="暂无吞吐趋势" :image-size="72" />
          <EchartsUI v-else ref="pipelineChartRef" class="chart" />
        </div>

        <div class="chart-panel">
          <div class="chart-title">错误趋势</div>
          <el-empty v-if="!hasErrorTrend" description="暂无错误趋势" :image-size="72" />
          <EchartsUI v-else ref="errorChartRef" class="chart" />
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>
        <div class="section-header">
          <span>耗时分位数</span>
          <div class="header-actions">
            <span class="header-hint">分位数按最近 5 分钟速率计算</span>
            <el-select v-model="selectedDurationStep" size="small" style="width: 260px;">
              <el-option
                v-for="item in durationStepOptions"
                :key="item.value"
                :label="`${item.value} / ${item.description}`"
                :value="item.value"
              />
            </el-select>
          </div>
        </div>
      </template>

      <el-table :data="durationQuantileRows" stripe>
        <el-table-column label="步骤" min-width="220" prop="step" />
        <el-table-column label="含义" min-width="220" prop="description" />
        <el-table-column align="right" label="累计样本" min-width="120" prop="samples" />
        <el-table-column align="right" label="平均值" min-width="140" prop="averageMs" />
        <el-table-column align="right" label="P50" min-width="140" prop="p50Ms" />
        <el-table-column align="right" label="P95" min-width="140" prop="p95Ms" />
        <el-table-column align="right" label="P99" min-width="140" prop="p99Ms" />
      </el-table>

      <div v-loading="analyticsLoading" class="chart-panel mt-3">
        <div class="chart-title">{{ selectedDurationStep }} 分位数趋势</div>
        <el-empty v-if="!hasDurationTrend" description="暂无分位数趋势" :image-size="72" />
        <EchartsUI v-else ref="durationChartRef" class="chart" />
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>源读取统计</template>
      <el-table :data="sourceSummaryRows" stripe>
        <el-table-column label="来源" min-width="120" prop="source" />
        <el-table-column align="right" label="累计拉取量" min-width="140" prop="fetchTotal" />
        <el-table-column align="right" label="空轮询次数" min-width="140" prop="emptyTotal" />
        <el-table-column align="right" label="批次样本数" min-width="140" prop="batchCount" />
        <el-table-column align="right" label="平均批大小" min-width="140" prop="averageBatch" />
        <el-table-column label="分布落桶上界" min-width="160" prop="batchUpperBound" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>去重缓存统计</template>
      <el-table :data="dedupCacheRows" stripe>
        <el-table-column label="结果" min-width="120" prop="result" />
        <el-table-column label="说明" min-width="220" prop="description" />
        <el-table-column align="right" label="累计次数" min-width="140" prop="total" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>管道步骤吞吐与错误</template>
      <el-table :data="pipelineStepRows" stripe>
        <el-table-column label="步骤" min-width="220" prop="step" />
        <el-table-column label="含义" min-width="280" prop="description" />
        <el-table-column align="right" label="吞吐累计" min-width="140" prop="throughputTotal" />
        <el-table-column align="right" label="错误累计" min-width="140" prop="errorTotal" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>耗时分布快照</template>
      <el-table :data="durationRows" stripe>
        <el-table-column label="步骤" min-width="240" prop="key" />
        <el-table-column align="right" label="样本数" min-width="120" prop="count" />
        <el-table-column align="right" label="总耗时(ms)" min-width="140" prop="total" />
        <el-table-column align="right" label="平均(ms)" min-width="120" prop="average" />
        <el-table-column label="分布落桶上界" min-width="160" prop="upperBound" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>批量大小分布快照</template>
      <el-table :data="batchSizeRows" stripe>
        <el-table-column label="来源" min-width="120" prop="key" />
        <el-table-column align="right" label="样本数" min-width="120" prop="count" />
        <el-table-column align="right" label="总量(rooms)" min-width="140" prop="total" />
        <el-table-column align="right" label="平均批大小" min-width="140" prop="average" />
        <el-table-column label="分布落桶上界" min-width="160" prop="upperBound" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>枚举范围</template>
      <div class="enum-block">
        <div class="enum-title">source</div>
        <div class="tag-list">
          <el-tag v-for="item in sourceOptions" :key="item.value" effect="plain" round>
            {{ item.value }} / {{ item.description }}
          </el-tag>
        </div>
      </div>
      <div class="enum-block">
        <div class="enum-title">step</div>
        <div class="tag-list">
          <el-tag v-for="item in durationStepOptions" :key="`duration-${item.value}`" effect="plain" round>
            {{ item.value }} / {{ item.description }}
          </el-tag>
          <el-tag v-for="item in pipelineStepOptions" :key="`pipeline-${item.value}`" effect="plain" round>
            {{ item.value }} / {{ item.description }}
          </el-tag>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>指标说明</template>
      <el-table :data="METRIC_CATALOG" stripe>
        <el-table-column label="指标名" min-width="320" prop="metricName" />
        <el-table-column label="类型" min-width="120" prop="type" />
        <el-table-column label="标签" min-width="120" prop="labels" />
        <el-table-column label="说明" min-width="320" prop="description" />
      </el-table>
    </el-card>

  </div>
</template>

<style scoped>
.sup-room-dashboard-panel {
  width: 100%;
}

.section-header {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.header-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
}

.header-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metrics-grid,
.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.charts-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.chart-panel {
  padding: 12px;
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
}

.chart-title {
  margin-bottom: 10px;
  font-size: 14px;
  font-weight: 600;
}

.chart {
  height: 320px;
}

.mb-3 {
  margin-bottom: 12px;
}

.metric-label,
.summary-label,
.enum-title {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-value,
.summary-value {
  margin-top: 6px;
  font-size: 18px;
  font-weight: 700;
  word-break: break-all;
}

.summary-item {
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.enum-block + .enum-block {
  margin-top: 16px;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

@media (max-width: 1280px) {
  .metrics-grid,
  .summary-grid,
  .charts-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .metrics-grid,
  .summary-grid,
  .charts-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .section-header,
  .header-actions {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
