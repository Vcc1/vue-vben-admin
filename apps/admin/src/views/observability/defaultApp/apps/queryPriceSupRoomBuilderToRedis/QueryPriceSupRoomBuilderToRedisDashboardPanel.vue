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

defineOptions({ name: 'QueryPriceSupRoomBuilderToRedisDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
  refreshKey?: number;
}>();

interface BreakdownRow {
  primary: string;
  secondary: string;
  total: string;
}

interface CatalogRow {
  labels: string;
  metricName: string;
  scope: string;
  type: string;
  unit: string;
  usage: string;
}

interface ErrorCodeRow {
  code: string;
  metricName: string;
  scopes: string;
  supId: string;
  total: string;
}

interface LineSeries {
  color: string;
  data: Array<[number, number]>;
  key: string;
  name: string;
}

interface LoopProgressCard {
  description: string;
  label: string;
  value: string;
}

interface ParsedMetricRow {
  labels: Record<string, string>;
  labelsText: string;
  metricName: string;
  numericValue: null | number;
  valueText: string;
}

interface StatusRow {
  status: string;
  total: string;
}

interface SummaryCard {
  label: string;
  value: string;
}

interface MetricNameRow {
  actualMetricName: string;
  description: string;
  key: string;
}

interface SupplierRow {
  avgDuration: string;
  error: string;
  filtered: string;
  hit: string;
  noPrice: string;
  p95Duration: string;
  priced: string;
  requests: string;
  supId: string;
  timeout: string;
}

interface TrendContext {
  end: number;
  start: number;
  step: number;
  window: string;
}

const METRIC_SCOPE_NAMES = [
  'QueryPriceSupRoomBuilderToRedis',
  'Infrastructure.HTTP.QueryPrice',
  'Infrastructure.Redis.TriggerQueue',
] as const;

const METRIC_NAME_ALIASES = {
  errorTotal: ['room_build_error_errors_total', 'room_build_error_total'],
  filteredTotal: ['price_filtered_rooms_total', 'price_filtered_total'],
  hitTotal: ['price_hit_rooms_total', 'price_hit_total'],
  loopProgressLegacy: ['room_build_progress_items'],
  loopProgressNew: ['room_build_loop_progress_items', 'room_build_loop_progress'],
  queryErrorCodeTotal: ['query_price_error_code_requests_total', 'query_price_error_code_total'],
  queryDuration: ['query_price_duration_ms_milliseconds', 'query_price_duration_ms'],
  queryRequestTotal: ['query_price_request_requests_total', 'query_price_request_total'],
  queryResultTotal: ['query_price_result_requests_total', 'query_price_result_total'],
  redisPushTotal: ['sup_room_redis_push_rooms_total', 'sup_room_redis_push_total'],
  taskEventTotal: ['room_build_task_tasks_total', 'room_build_task_total'],
  taskStatusCount: ['room_build_task_status_count_tasks', 'room_build_task_status_count'],
  triggerQueueOperationTotal: ['trigger_queue_operation_items_total', 'trigger_queue_operation_total'],
  triggerQueuePopTotal: ['trigger_queue_pop_items_total', 'trigger_queue_pop_total'],
  triggerTaskUpsertTotal: ['trigger_task_upsert_tasks_total', 'trigger_task_upsert_total'],
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

const CHART_COLORS = ['#2563eb', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#14b8a6'];

const TASK_STATUS_ORDER = ['Pending', 'RetryWaiting', 'Completed', 'Failed'] as const;

const METRIC_CATALOG: CatalogRow[] = [
  {
    labels: 'loop, state',
    metricName: 'room_build_loop_progress',
    scope: 'QueryPriceSupRoomBuilderToRedis',
    type: 'Gauge',
    unit: 'count',
    usage: '外层维度与内层酒店循环进度，loop=dimension|hotel，state=total|current|running',
  },
  {
    labels: 'queue',
    metricName: 'trigger_queue_pop_total',
    scope: 'Infrastructure.Redis.TriggerQueue',
    type: 'Counter',
    unit: 'items',
    usage: 'RoomBuild 触发队列 pop 出来的酒店数',
  },
  {
    labels: 'outcome',
    metricName: 'trigger_task_upsert_total',
    scope: 'QueryPriceSupRoomBuilderToRedis',
    type: 'Counter',
    unit: 'tasks',
    usage: '写入或更新任务表数量',
  },
  {
    labels: 'status',
    metricName: 'room_build_task_total',
    scope: 'QueryPriceSupRoomBuilderToRedis',
    type: 'Counter',
    unit: 'tasks',
    usage: '任务流转事件累计值，覆盖 worker、claimed、completed、retry 等状态',
  },
  {
    labels: 'status',
    metricName: 'room_build_task_status_count',
    scope: 'QueryPriceSupRoomBuilderToRedis',
    type: 'Gauge',
    unit: 'tasks',
    usage: '当前任务表各状态数量',
  },
  {
    labels: 'sup_id',
    metricName: 'price_hit_total',
    scope: 'Infrastructure.HTTP.QueryPrice',
    type: 'Counter',
    unit: 'rooms',
    usage: '查到并筛选通过的房型数',
  },
  {
    labels: 'sup_id, reason',
    metricName: 'price_filtered_total',
    scope: 'Infrastructure.HTTP.QueryPrice',
    type: 'Counter',
    unit: 'rooms',
    usage: '房型过滤原因统计',
  },
  {
    labels: 'outcome',
    metricName: 'sup_room_redis_push_total',
    scope: 'QueryPriceSupRoomBuilderToRedis',
    type: 'Counter',
    unit: 'rooms',
    usage: '推送到 NewSupRoom 的房型数',
  },
  {
    labels: 'stage, outcome',
    metricName: 'room_build_error_total',
    scope: 'QueryPriceSupRoomBuilderToRedis',
    type: 'Counter',
    unit: 'errors',
    usage: '建房各阶段异常数',
  },
  {
    labels: 'sup_id, outcome, hotel_count',
    metricName: 'query_price_request_total',
    scope: 'Infrastructure.HTTP.QueryPrice',
    type: 'Counter',
    unit: 'requests',
    usage: '查价请求启动数，当前 outcome 主要为 started',
  },
  {
    labels: 'sup_id, code',
    metricName: 'query_price_error_code_total',
    scope: 'Infrastructure.HTTP.QueryPrice',
    type: 'Counter',
    unit: 'requests',
    usage: '按错误码聚合的查价异常次数',
  },
  {
    labels: 'sup_id, outcome, hotel_count',
    metricName: 'query_price_result_total',
    scope: 'Infrastructure.HTTP.QueryPrice',
    type: 'Counter',
    unit: 'requests',
    usage: '查价结果数，outcome 包含 priced、no_price、error、timeout',
  },
  {
    labels: 'sup_id, outcome, hotel_count',
    metricName: 'query_price_duration_ms',
    scope: 'Infrastructure.HTTP.QueryPrice',
    type: 'Histogram',
    unit: 'ms',
    usage: '查价接口耗时分布',
  },
  {
    labels: 'queue, stage, outcome',
    metricName: 'trigger_queue_operation_total',
    scope: 'Infrastructure.Redis.TriggerQueue',
    type: 'Counter',
    unit: 'items',
    usage: 'Redis 触发队列底层操作计数',
  },
];

const client = new PrometheusClient();

const selectedRange = ref<RuntimeRange>('15m');
const analyticsLoading = ref(false);
const analyticsError = ref('');

const throughputSeries = ref<LineSeries[]>([]);
const taskStatusSeries = ref<LineSeries[]>([]);
const queueSeries = ref<LineSeries[]>([]);
const errorCodeRows = ref<ErrorCodeRow[]>([]);
const supplierRows = ref<SupplierRow[]>([]);

const throughputChartRef = ref<EchartsUIType>();
const taskStatusChartRef = ref<EchartsUIType>();
const queueChartRef = ref<EchartsUIType>();

const { renderEcharts: renderThroughputChart } = useEcharts(throughputChartRef);
const { renderEcharts: renderTaskStatusChart } = useEcharts(taskStatusChartRef);
const { renderEcharts: renderQueueChart } = useEcharts(queueChartRef);

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

function buildHistogramMetricSelector(
  metricName: string,
  suffix: 'bucket' | 'count' | 'sum',
  labels: Record<string, string>,
  extraMatchers: string[] = [],
) {
  return `${metricName}_${suffix}${buildSelector(labels, extraMatchers)}`;
}

function parsePrometheusNumber(value?: null | PrometheusValue) {
  if (!value) {
    return null;
  }
  const numericValue = Number(value[1]);
  return Number.isFinite(numericValue) ? numericValue : null;
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

function formatDuration(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  if (value >= 1000) {
    return `${formatNumber(value / 1000, 2)} s`;
  }
  return `${formatNumber(value, 2)} ms`;
}

function formatRatePerMinute(value: number) {
  return formatNumber(value, value >= 100 ? 0 : 2);
}

function getRangeParams(range: RuntimeRange, queryTime?: null | number): TrendContext {
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
  timeline: number[],
  descriptions?: Record<string, string>,
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
    .filter((key) => groupedMap.has(key))
    .map((key, index) => ({
      color: CHART_COLORS[index % CHART_COLORS.length],
      data: timeline.map((timestamp) => [timestamp, groupedMap.get(key)?.get(timestamp) || 0]) as Array<
        [number, number]
      >,
      key,
      name: descriptions?.[key] || key,
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

function hasSeriesData(series: LineSeries[]) {
  return series.some((item) => item.data.some(([, value]) => value !== 0));
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

const latestMetricRows = computed(() => (props.app?.latestMetricValues || []).map(parseMetricValue));
const availableMetricNames = computed(() =>
  new Set([
    ...(props.app?.availableMetrics || []),
    ...latestMetricRows.value.map((row) => row.metricName),
  ]),
);

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));
const trendContext = computed(() => getRangeParams(selectedRange.value, runtimeQueryTime.value));

function resolveMetricName(metricNames: readonly string[]) {
  return metricNames.find((item) => availableMetricNames.value.has(item)) || metricNames[0];
}

const resolvedMetricNameRows = computed<MetricNameRow[]>(() => [
  {
    actualMetricName: resolveMetricName(METRIC_NAME_ALIASES.queryRequestTotal),
    description: '查价启动请求数',
    key: 'queryRequestTotal',
  },
  {
    actualMetricName: resolveMetricName(METRIC_NAME_ALIASES.queryErrorCodeTotal),
    description: '查价错误码累计值',
    key: 'queryErrorCodeTotal',
  },
  {
    actualMetricName: resolveMetricName(METRIC_NAME_ALIASES.queryResultTotal),
    description: '查价结果累计值',
    key: 'queryResultTotal',
  },
  {
    actualMetricName: resolveMetricName(METRIC_NAME_ALIASES.errorTotal),
    description: '建房错误累计值',
    key: 'errorTotal',
  },
  {
    actualMetricName: resolveMetricName(METRIC_NAME_ALIASES.taskStatusCount),
    description: '任务状态 Gauge',
    key: 'taskStatusCount',
  },
  {
    actualMetricName: resolveMetricName(METRIC_NAME_ALIASES.loopProgressNew),
    description: '循环进度 Gauge',
    key: 'loopProgressNew',
  },
]);

function getMetricRows(metricName: readonly string[] | string, labels: Record<string, string> = {}) {
  const metricNames = Array.isArray(metricName) ? metricName : [metricName];
  return latestMetricRows.value.filter((row) => {
    if (!metricNames.includes(row.metricName)) {
      return false;
    }
    return Object.entries(labels).every(([key, value]) => row.labels[key] === value);
  });
}

function sumMetricValues(metricName: readonly string[] | string, labels: Record<string, string> = {}) {
  const total = getMetricRows(metricName, labels).reduce((sum, row) => sum + (row.numericValue || 0), 0);
  return Number.isFinite(total) ? total : null;
}

function getMetricNumber(metricName: readonly string[] | string, labels: Record<string, string> = {}) {
  const row = getMetricRows(metricName, labels)[0];
  return row?.numericValue ?? null;
}

function aggregateMetricRows(
  metricName: readonly string[] | string,
  primaryLabel: string,
  secondaryLabel?: string,
) {
  const grouped = new Map<string, number>();

  for (const row of getMetricRows(metricName)) {
    const primary = row.labels[primaryLabel] || '--';
    const secondary = secondaryLabel ? row.labels[secondaryLabel] || '--' : '--';
    const key = `${primary}@@${secondary}`;
    grouped.set(key, (grouped.get(key) || 0) + (row.numericValue || 0));
  }

  return [...grouped.entries()]
    .map(([key, total]) => {
      const [primary, secondary] = key.split('@@');
      return {
        primary,
        secondary,
        total: formatInteger(total),
      };
    })
    .sort((a, b) => Number.parseFloat(b.total.replaceAll(',', '')) - Number.parseFloat(a.total.replaceAll(',', '')));
}

const availableBusinessScopes = computed(() =>
  [...new Set(
    latestMetricRows.value
      .map((row) => row.labels.otel_scope_name)
      .filter((value): value is string => Boolean(value) && METRIC_SCOPE_NAMES.includes(value as (typeof METRIC_SCOPE_NAMES)[number])),
  )].sort((a, b) => a.localeCompare(b)),
);

const supplierCount = computed(() =>
  new Set(
    getMetricRows(METRIC_NAME_ALIASES.queryRequestTotal)
      .map((row) => row.labels.sup_id)
      .filter((value): value is string => Boolean(value)),
  ).size,
);

const overviewCards = computed<SummaryCard[]>(() => [
  {
    label: '待处理任务数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.taskStatusCount, { status: 'Pending' })),
  },
  {
    label: '重试等待数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.taskStatusCount, { status: 'RetryWaiting' })),
  },
  {
    label: '已完成数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.taskStatusCount, { status: 'Completed' })),
  },
  {
    label: '失败数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.taskStatusCount, { status: 'Failed' })),
  },
  {
    label: '查价请求数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.queryRequestTotal)),
  },
  {
    label: '命中房型数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.hitTotal)),
  },
  {
    label: '推送 Redis 房型数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.redisPushTotal)),
  },
  {
    label: '错误数',
    value: formatInteger(sumMetricValues(METRIC_NAME_ALIASES.errorTotal)),
  },
]);

function getLoopProgressValue(loop: 'dimension' | 'hotel', state: 'current' | 'running' | 'total') {
  const nextValue = getMetricNumber(METRIC_NAME_ALIASES.loopProgressNew, { loop, state });
  if (nextValue !== null) {
    return nextValue;
  }

  const legacyScope = loop === 'dimension' ? 'outer' : 'inner';
  const legacyState =
    state === 'total'
      ? 'total'
      : state === 'current'
        ? 'completed'
        : null;

  if (!legacyState) {
    return null;
  }

  return getMetricNumber(METRIC_NAME_ALIASES.loopProgressLegacy, {
    scope: legacyScope,
    state: legacyState,
  });
}

const loopProgressCards = computed<LoopProgressCard[]>(() => [
  {
    description: '总维度数',
    label: '维度总数',
    value: formatInteger(getLoopProgressValue('dimension', 'total')),
  },
  {
    description: '外层维度循环到第几个，1-based',
    label: '当前维度序号',
    value: formatInteger(getLoopProgressValue('dimension', 'current')),
  },
  {
    description: '当前是否有维度在执行，执行中为 1，空闲为 0',
    label: '维度执行中',
    value: formatInteger(getLoopProgressValue('dimension', 'running')),
  },
  {
    description: '当前维度下要查的酒店数',
    label: '酒店总数',
    value: formatInteger(getLoopProgressValue('hotel', 'total')),
  },
  {
    description: '当前维度已完成的酒店数',
    label: '已完成酒店数',
    value: formatInteger(getLoopProgressValue('hotel', 'current')),
  },
  {
    description: '当前正在跑的查价 batch 数',
    label: '运行中 Batch',
    value: formatInteger(getLoopProgressValue('hotel', 'running')),
  },
]);

const currentStatusRows = computed<StatusRow[]>(() => {
  const statusMap = new Map<string, number>();
  for (const row of getMetricRows(METRIC_NAME_ALIASES.taskStatusCount)) {
    const status = row.labels.status || '--';
    statusMap.set(status, (statusMap.get(status) || 0) + (row.numericValue || 0));
  }

  return [...statusMap.entries()]
    .sort((a, b) => {
      const left = TASK_STATUS_ORDER.indexOf(a[0] as (typeof TASK_STATUS_ORDER)[number]);
      const right = TASK_STATUS_ORDER.indexOf(b[0] as (typeof TASK_STATUS_ORDER)[number]);
      if (left === -1 && right === -1) {
        return a[0].localeCompare(b[0]);
      }
      if (left === -1) {
        return 1;
      }
      if (right === -1) {
        return -1;
      }
      return left - right;
    })
    .map(([status, total]) => ({
      status,
      total: formatInteger(total),
    }));
});

const taskEventRows = computed<StatusRow[]>(() => {
  const statusMap = new Map<string, number>();
  for (const row of getMetricRows(METRIC_NAME_ALIASES.taskEventTotal)) {
    const status = row.labels.status || '--';
    statusMap.set(status, (statusMap.get(status) || 0) + (row.numericValue || 0));
  }

  return [...statusMap.entries()]
    .sort((a, b) => b[1] - a[1])
    .map(([status, total]) => ({
      status,
      total: formatInteger(total),
    }));
});

const filterReasonRows = computed<BreakdownRow[]>(() =>
  aggregateMetricRows(METRIC_NAME_ALIASES.filteredTotal, 'reason', 'sup_id').slice(0, 20),
);

const errorBreakdownRows = computed<BreakdownRow[]>(() =>
  aggregateMetricRows(METRIC_NAME_ALIASES.errorTotal, 'stage', 'outcome').slice(0, 20),
);

const hasThroughputTrend = computed(() => hasSeriesData(throughputSeries.value));
const hasTaskStatusTrend = computed(() => hasSeriesData(taskStatusSeries.value));
const hasQueueTrend = computed(() => hasSeriesData(queueSeries.value));

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

async function renderCharts() {
  await nextTick();

  if (hasThroughputTrend.value) {
    renderThroughputChart(
      buildLineOption(throughputSeries.value, formatRatePerMinute, 'count / min'),
    );
  }

  if (hasTaskStatusTrend.value) {
    renderTaskStatusChart(
      buildLineOption(taskStatusSeries.value, (value) => formatNumber(value, 0), 'tasks'),
    );
  }

  if (hasQueueTrend.value) {
    renderQueueChart(
      buildLineOption(queueSeries.value, formatRatePerMinute, 'count / min'),
    );
  }
}

async function loadAnalytics() {
  if (!props.app) {
    throughputSeries.value = [];
    taskStatusSeries.value = [];
    queueSeries.value = [];
    errorCodeRows.value = [];
    supplierRows.value = [];
    return;
  }

  const requestId = ++analyticsRequestId;
  analyticsLoading.value = true;
  analyticsError.value = '';

  try {
    const labels = targetLabels.value;
    const timeline = buildTimeline(trendContext.value.start, trendContext.value.end, trendContext.value.step);
    const throughputWindow = trendContext.value.window;
    const queryDurationMetricName = resolveMetricName(METRIC_NAME_ALIASES.queryDuration);
    const queryErrorCodeMetricName = resolveMetricName(METRIC_NAME_ALIASES.queryErrorCodeTotal);
    const queryRequestMetricName = resolveMetricName(METRIC_NAME_ALIASES.queryRequestTotal);
    const queryResultMetricName = resolveMetricName(METRIC_NAME_ALIASES.queryResultTotal);
    const taskStatusMetricName = resolveMetricName(METRIC_NAME_ALIASES.taskStatusCount);
    const triggerQueuePopMetricName = resolveMetricName(METRIC_NAME_ALIASES.triggerQueuePopTotal);
    const triggerTaskUpsertMetricName = resolveMetricName(METRIC_NAME_ALIASES.triggerTaskUpsertTotal);
    const taskEventMetricName = resolveMetricName(METRIC_NAME_ALIASES.taskEventTotal);
    const errorMetricName = resolveMetricName(METRIC_NAME_ALIASES.errorTotal);
    const hitMetricName = resolveMetricName(METRIC_NAME_ALIASES.hitTotal);
    const filteredMetricName = resolveMetricName(METRIC_NAME_ALIASES.filteredTotal);
    const redisPushMetricName = resolveMetricName(METRIC_NAME_ALIASES.redisPushTotal);
    const durationBucketSelector = buildHistogramMetricSelector(queryDurationMetricName, 'bucket', labels);
    const durationSumSelector = buildHistogramMetricSelector(queryDurationMetricName, 'sum', labels);
    const durationCountSelector = buildHistogramMetricSelector(queryDurationMetricName, 'count', labels);

    const [
      requestTrendResults,
      hitTrendResults,
      pushTrendResults,
      errorTrendResults,
      pendingTrendResults,
      retryTrendResults,
      completedTrendResults,
      failedTrendResults,
      queuePopTrendResults,
      taskUpsertTrendResults,
      taskEventTrendResults,
      requestBySupResults,
      errorCodeBySupResults,
      pricedBySupResults,
      noPriceBySupResults,
      timeoutBySupResults,
      errorBySupResults,
      hitBySupResults,
      filteredBySupResults,
      avgDurationBySupResults,
      p95DurationBySupResults,
    ] = await Promise.all([
      queryRange(`sum(rate(${buildMetricSelector(queryRequestMetricName, labels)}[${throughputWindow}])) * 60`),
      queryRange(`sum(rate(${buildMetricSelector(hitMetricName, labels)}[${throughputWindow}])) * 60`),
      queryRange(`sum(rate(${buildMetricSelector(redisPushMetricName, labels)}[${throughputWindow}])) * 60`),
      queryRange(`sum(rate(${buildMetricSelector(errorMetricName, labels)}[${throughputWindow}])) * 60`),
      queryRange(buildMetricSelector(taskStatusMetricName, labels, ['status="Pending"'])),
      queryRange(buildMetricSelector(taskStatusMetricName, labels, ['status="RetryWaiting"'])),
      queryRange(buildMetricSelector(taskStatusMetricName, labels, ['status="Completed"'])),
      queryRange(buildMetricSelector(taskStatusMetricName, labels, ['status="Failed"'])),
      queryRange(`sum(rate(${buildMetricSelector(triggerQueuePopMetricName, labels)}[${throughputWindow}])) * 60`),
      queryRange(`sum(rate(${buildMetricSelector(triggerTaskUpsertMetricName, labels)}[${throughputWindow}])) * 60`),
      queryRange(`sum(rate(${buildMetricSelector(taskEventMetricName, labels)}[${throughputWindow}])) * 60`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(queryRequestMetricName, labels)})`),
      queryInstant(`sum by (otel_scope_name, code, sup_id) (${buildMetricSelector(queryErrorCodeMetricName, labels)})`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(queryResultMetricName, labels, ['outcome="priced"'])})`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(queryResultMetricName, labels, ['outcome="no_price"'])})`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(queryResultMetricName, labels, ['outcome="timeout"'])})`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(queryResultMetricName, labels, ['outcome="error"'])})`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(hitMetricName, labels)})`),
      queryInstant(`sum by (sup_id) (${buildMetricSelector(filteredMetricName, labels)})`),
      queryInstant(
        `sum by (sup_id) (rate(${durationSumSelector}[5m])) / sum by (sup_id) (rate(${durationCountSelector}[5m]))`,
      ),
      queryInstant(
        `histogram_quantile(0.95, sum by (le, sup_id) (rate(${durationBucketSelector}[5m])))`,
      ),
    ]);

    if (requestId !== analyticsRequestId) {
      return;
    }

    throughputSeries.value = [
      buildSingleSeries('requests', '查价请求', CHART_COLORS[0], requestTrendResults, timeline),
      buildSingleSeries('hit', '命中房型', CHART_COLORS[1], hitTrendResults, timeline),
      buildSingleSeries('push', 'Redis 推送', CHART_COLORS[2], pushTrendResults, timeline),
      buildSingleSeries('error', '错误', CHART_COLORS[3], errorTrendResults, timeline),
    ].filter((item) => item.data.some(([, value]) => value !== 0));

    taskStatusSeries.value = [
      buildSingleSeries('pending', 'Pending', CHART_COLORS[0], pendingTrendResults, timeline),
      buildSingleSeries('retryWaiting', 'RetryWaiting', CHART_COLORS[1], retryTrendResults, timeline),
      buildSingleSeries('completed', 'Completed', CHART_COLORS[2], completedTrendResults, timeline),
      buildSingleSeries('failed', 'Failed', CHART_COLORS[3], failedTrendResults, timeline),
    ].filter((item) => item.data.some(([, value]) => value !== 0));

    queueSeries.value = [
      buildSingleSeries('queuePop', '队列 Pop', CHART_COLORS[0], queuePopTrendResults, timeline),
      buildSingleSeries('taskUpsert', '任务 Upsert', CHART_COLORS[1], taskUpsertTrendResults, timeline),
      buildSingleSeries('taskEvent', '任务流转', CHART_COLORS[2], taskEventTrendResults, timeline),
    ].filter((item) => item.data.some(([, value]) => value !== 0));

    const requestBySup = mapVectorByLabel(requestBySupResults, 'sup_id');
    const errorCodeMap = new Map<
      string,
      { code: string; scopes: Set<string>; supId: string; total: number }
    >();
    for (const row of errorCodeBySupResults) {
      const code = row.metric.code || '--';
      const supId = row.metric.sup_id || '--';
      const scope = row.metric.otel_scope_name || '--';
      const total = parsePrometheusNumber(row.value);
      if (total === null) {
        continue;
      }
      const key = `${supId}@@${code}`;
      const current = errorCodeMap.get(key) || {
        code,
        scopes: new Set<string>(),
        supId,
        total,
      };
      current.scopes.add(scope);
      current.total = Math.max(current.total, total);
      errorCodeMap.set(key, current);
    }
    const pricedBySup = mapVectorByLabel(pricedBySupResults, 'sup_id');
    const noPriceBySup = mapVectorByLabel(noPriceBySupResults, 'sup_id');
    const timeoutBySup = mapVectorByLabel(timeoutBySupResults, 'sup_id');
    const errorBySup = mapVectorByLabel(errorBySupResults, 'sup_id');
    const hitBySup = mapVectorByLabel(hitBySupResults, 'sup_id');
    const filteredBySup = mapVectorByLabel(filteredBySupResults, 'sup_id');
    const avgDurationBySup = mapVectorByLabel(avgDurationBySupResults, 'sup_id');
    const p95DurationBySup = mapVectorByLabel(p95DurationBySupResults, 'sup_id');

    const supplierIds = [
      ...new Set([
        ...requestBySup.keys(),
        ...pricedBySup.keys(),
        ...noPriceBySup.keys(),
        ...timeoutBySup.keys(),
        ...errorBySup.keys(),
        ...hitBySup.keys(),
        ...filteredBySup.keys(),
      ]),
    ].sort((a, b) => (requestBySup.get(b) || 0) - (requestBySup.get(a) || 0));

    supplierRows.value = supplierIds.map((supId) => ({
      avgDuration: formatDuration(avgDurationBySup.get(supId) ?? null),
      error: formatInteger(errorBySup.get(supId) ?? null),
      filtered: formatInteger(filteredBySup.get(supId) ?? null),
      hit: formatInteger(hitBySup.get(supId) ?? null),
      noPrice: formatInteger(noPriceBySup.get(supId) ?? null),
      p95Duration: formatDuration(p95DurationBySup.get(supId) ?? null),
      priced: formatInteger(pricedBySup.get(supId) ?? null),
      requests: formatInteger(requestBySup.get(supId) ?? null),
      supId,
      timeout: formatInteger(timeoutBySup.get(supId) ?? null),
    }));

    errorCodeRows.value = [...errorCodeMap.values()]
      .sort((a, b) => b.total - a.total || a.supId.localeCompare(b.supId) || a.code.localeCompare(b.code))
      .map((item) => ({
        code: item.code,
        metricName: queryErrorCodeMetricName,
        scopes: [...item.scopes].sort((a, b) => a.localeCompare(b)).join(', '),
        supId: item.supId,
        total: formatInteger(item.total),
      }));

    await renderCharts();
  } catch (error) {
    if (requestId !== analyticsRequestId) {
      return;
    }
    analyticsError.value = error instanceof Error ? error.message : 'Prometheus 查询失败';
    throughputSeries.value = [];
    taskStatusSeries.value = [];
    queueSeries.value = [];
    errorCodeRows.value = [];
    supplierRows.value = [];
  } finally {
    if (requestId === analyticsRequestId) {
      analyticsLoading.value = false;
    }
  }
}

watch(
  () => [selectedRange.value, runtimeQueryTime.value, JSON.stringify(targetLabels.value), props.refreshKey || 0],
  () => {
    void loadAnalytics();
  },
  { immediate: true },
);
</script>

<template>
  <div class="query-price-dashboard-panel">
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
        <div class="metric-label">活跃供应商数</div>
        <div class="metric-value">{{ formatInteger(supplierCount) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">固定业务 Scope</div>
        <div class="metric-value">{{ formatInteger(METRIC_SCOPE_NAMES.length) }}</div>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>核心看板指标</template>
      <div class="summary-grid">
        <div v-for="item in overviewCards" :key="item.label" class="summary-item">
          <div class="summary-label">{{ item.label }}</div>
          <div class="summary-value">{{ item.value }}</div>
        </div>
      </div>
      <div class="tag-list mt-3">
        <el-tag
          v-for="scope in METRIC_SCOPE_NAMES"
          :key="scope"
          :type="availableBusinessScopes.includes(scope) ? 'success' : 'info'"
          effect="plain"
          round
        >
          {{ scope }}
        </el-tag>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>循环进度</template>
      <div class="summary-grid loop-grid">
        <div v-for="item in loopProgressCards" :key="item.label" class="summary-item">
          <div class="summary-label">{{ item.label }}</div>
          <div class="summary-value">{{ item.value }}</div>
          <div class="summary-hint">{{ item.description }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>当前实际指标名</template>
      <el-table :data="resolvedMetricNameRows" stripe>
        <el-table-column label="用途" min-width="220" prop="description" />
        <el-table-column label="实际指标名" min-width="320" prop="actualMetricName" />
      </el-table>
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
          <div class="chart-title">每分钟请求、命中、推送、错误</div>
          <el-empty v-if="!hasThroughputTrend" description="暂无趋势数据" :image-size="72" />
          <EchartsUI v-else ref="throughputChartRef" class="chart" />
        </div>

        <div class="chart-panel">
          <div class="chart-title">任务状态曲线</div>
          <el-empty v-if="!hasTaskStatusTrend" description="暂无任务状态数据" :image-size="72" />
          <EchartsUI v-else ref="taskStatusChartRef" class="chart" />
        </div>

        <div class="chart-panel chart-panel-wide">
          <div class="chart-title">队列操作与任务流转速率</div>
          <el-empty v-if="!hasQueueTrend" description="暂无队列趋势数据" :image-size="72" />
          <EchartsUI v-else ref="queueChartRef" class="chart" />
        </div>
      </div>
    </el-card>

    <div class="tables-grid mt-3">
      <el-card shadow="never">
        <template #header>当前任务状态</template>
        <el-table :data="currentStatusRows" stripe>
          <el-table-column label="状态" min-width="160" prop="status" />
          <el-table-column align="right" label="当前数量" min-width="120" prop="total" />
        </el-table>
      </el-card>

      <el-card shadow="never">
        <template #header>任务流转事件</template>
        <el-table :data="taskEventRows" stripe>
          <el-table-column label="事件" min-width="160" prop="status" />
          <el-table-column align="right" label="累计值" min-width="120" prop="total" />
        </el-table>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>供应商维度</template>
      <el-table :data="supplierRows" stripe>
        <el-table-column fixed label="sup_id" min-width="140" prop="supId" />
        <el-table-column align="right" label="请求数" min-width="120" prop="requests" />
        <el-table-column align="right" label="成功有价" min-width="120" prop="priced" />
        <el-table-column align="right" label="无价" min-width="120" prop="noPrice" />
        <el-table-column align="right" label="超时" min-width="120" prop="timeout" />
        <el-table-column align="right" label="错误" min-width="120" prop="error" />
        <el-table-column align="right" label="命中房型" min-width="120" prop="hit" />
        <el-table-column align="right" label="过滤房型" min-width="120" prop="filtered" />
        <el-table-column align="right" label="平均耗时" min-width="140" prop="avgDuration" />
        <el-table-column align="right" label="P95" min-width="140" prop="p95Duration" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>查价错误码明细</template>
      <el-alert
        :closable="false"
        class="mb-3"
        title="query_price_request 表示请求启动次数，当前常见 outcome=started；具体错误码来自独立指标 query_price_error_code_requests_total。"
        type="info"
      />
      <el-table :data="errorCodeRows" stripe>
        <el-table-column label="sup_id" min-width="140" prop="supId" />
        <el-table-column label="code" min-width="120" prop="code" />
        <el-table-column align="right" label="累计值" min-width="120" prop="total" />
        <el-table-column label="实际指标名" min-width="280" prop="metricName" />
        <el-table-column label="命中 Scope" min-width="260" prop="scopes" />
      </el-table>
    </el-card>

    <div class="tables-grid mt-3">
      <el-card shadow="never">
        <template #header>过滤原因 Top 20</template>
        <el-table :data="filterReasonRows" stripe>
          <el-table-column label="reason" min-width="180" prop="primary" />
          <el-table-column label="sup_id" min-width="140" prop="secondary" />
          <el-table-column align="right" label="累计数" min-width="120" prop="total" />
        </el-table>
      </el-card>

      <el-card shadow="never">
        <template #header>错误拆分 Top 20</template>
        <el-table :data="errorBreakdownRows" stripe>
          <el-table-column label="stage" min-width="180" prop="primary" />
          <el-table-column label="outcome" min-width="140" prop="secondary" />
          <el-table-column align="right" label="累计数" min-width="120" prop="total" />
        </el-table>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>指标清单</template>
      <el-table :data="METRIC_CATALOG" stripe>
        <el-table-column label="指标名" min-width="280" prop="metricName" />
        <el-table-column label="Scope" min-width="220" prop="scope" />
        <el-table-column label="类型" min-width="120" prop="type" />
        <el-table-column label="单位" min-width="100" prop="unit" />
        <el-table-column label="标签" min-width="180" prop="labels" />
        <el-table-column label="用途" min-width="320" prop="usage" />
      </el-table>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>日志字段提示</template>
      <div class="field-list">
        <el-tag v-for="field in ['DimensionIndex', 'TotalDimensions', 'TotalHotels', 'CompletedHotels', 'HitHotels', 'RemainingHotels', 'RunningBatches', 'SupId', 'AdultNumber', 'StayNights', 'CheckIn', 'CheckOut']" :key="field" effect="plain" round>
          {{ field }}
        </el-tag>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.query-price-dashboard-panel {
  width: 100%;
}

.section-header {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.metrics-grid,
.summary-grid,
.tables-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.summary-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
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

.chart-panel-wide {
  grid-column: span 2;
}

.chart-title {
  margin-bottom: 10px;
  font-size: 14px;
  font-weight: 600;
}

.chart {
  height: 320px;
}

.metric-label,
.summary-label,
.summary-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.summary-hint {
  margin-top: 6px;
  line-height: 1.5;
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

.loop-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.field-list,
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1280px) {
  .summary-grid,
  .charts-grid,
  .metrics-grid,
  .tables-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .loop-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .chart-panel-wide {
    grid-column: auto;
  }
}

@media (max-width: 768px) {
  .summary-grid,
  .charts-grid,
  .metrics-grid,
  .tables-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .loop-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
