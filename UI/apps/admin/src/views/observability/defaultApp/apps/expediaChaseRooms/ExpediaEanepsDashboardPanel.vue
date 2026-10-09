<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { AppViewModel } from '../../types';

import type {
  PrometheusMatrixResult,
  PrometheusVectorResult,
} from '#/api/prometheus/types';

import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { ElMessage } from 'element-plus';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import { buildQueryStepSeconds, formatDateTime } from './formatters';
import { EXPEDIA_TIME_RANGE_OPTIONS } from './metrics';

type EChartsOption = Record<string, any>;
type SeriesOption = Record<string, any>;

defineOptions({ name: 'ExpediaEanepsDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
}>();

interface MetricScope {
  exportedInstance: string;
  exportedJob: string;
  otelScopeName: string;
}

interface TaskMetricCard {
  key: string;
  label: string;
  tone?: 'danger' | 'info' | 'primary' | 'success' | 'warning';
  value: null | number;
}

interface StageRow {
  extraMetrics: Array<{ label: string; value: null | number }>;
  failed: null | number;
  processed: null | number;
  remaining: null | number;
  running: null | number;
  stage: string;
}

interface StageTrendPoint {
  failed: number;
  processed: number;
  processedPerSecond: null | number;
  remaining: number;
  running: number;
  timeLabel: string;
  timestamp: number;
}

interface HttpQueryRow {
  avgMs: null | number;
  bucketCounts: Map<number, number>;
  completed: number;
  durationCount: number;
  durationSum: number;
  error: number;
  executions: number;
  hotelCount: string;
  maxMs: null | number;
  noPrice: number;
  operation: string;
  p50Ms: null | number;
  p80Ms: null | number;
  p95Ms: null | number;
  p99Ms: null | number;
  priced: number;
  supId: string;
  timeout: number;
  unfinished: number;
}

interface HttpSummary {
  avgMs: null | number;
  bucketCounts: Map<number, number>;
  completed: number;
  error: number;
  errorRate: null | number;
  executions: number;
  maxMs: null | number;
  noPrice: number;
  noPriceRate: null | number;
  p50Ms: null | number;
  p80Ms: null | number;
  p95Ms: null | number;
  p99Ms: null | number;
  priced: number;
  pricedRate: null | number;
  timeout: number;
  timeoutRate: null | number;
  unfinished: number;
}

interface TableSummaryColumn {
  label?: string;
  property?: string;
}

interface HttpTrendPoint {
  avgMs: null | number;
  completed: number;
  error: number;
  executions: number;
  noPrice: number;
  priced: number;
  timeLabel: string;
  timeout: number;
  timestamp: number;
}

interface DashboardData {
  httpRows: HttpQueryRow[];
  httpTrendPoints: HttpTrendPoint[];
  queueMetrics: Array<{ label: string; value: null | number }>;
  stageRows: StageRow[];
  stageTrendMap: Record<string, StageTrendPoint[]>;
  taskCards: TaskMetricCard[];
  taskTimeline: Array<{ label: string; value: null | number }>;
}

interface HttpMetricNames {
  durationBucket: string;
  durationCount: string;
  durationSum: string;
  executions: null | string;
  requests: string;
}

const client = new PrometheusClient();

const loading = ref(false);
const errorMessage = ref('');
const selectedRange = ref<'1h' | '6h' | '15m' | '24h' | 'custom'>('1h');
const customRange = ref<[] | [Date, Date]>([]);
const autoRefreshEnabled = ref(false);
const lastLoadedAt = ref(0);
const dashboardData = ref<DashboardData | null>(null);
const selectedOperations = ref<string[]>([]);
const selectedSupIds = ref<string[]>([]);
const selectedHotelCounts = ref<string[]>([]);
const selectedOutcomes = ref<string[]>([]);
const refreshEpoch = ref(0);

let refreshTimer: number | undefined;
let requestId = 0;
let httpMetricNamesCache: HttpMetricNames | null = null;
let httpMetricNamesPromise: null | Promise<HttpMetricNames> = null;

const httpCountTrendRef = ref<EchartsUIType>();
const httpLatencyTrendRef = ref<EchartsUIType>();
const stageTrendRef = ref<EchartsUIType>();
const selectedStageTrendKey = ref<string>('query');

const { renderEcharts: renderHttpCountTrend } = useEcharts(httpCountTrendRef);
const { renderEcharts: renderHttpLatencyTrend } =
  useEcharts(httpLatencyTrendRef);
const { renderEcharts: renderStageTrend } = useEcharts(stageTrendRef);

const TASK_METRIC_DEFS = [
  {
    key: 'runSequence',
    label: '运行序号',
    metric: 'eaneps_price_comparison_run_sequence',
    reducer: 'max',
  },
  {
    key: 'running',
    label: '当前是否运行中',
    metric: 'eaneps_price_comparison_running',
    reducer: 'max',
    tone: 'primary',
  },
  {
    key: 'lastRunStatus',
    label: '最近一次运行状态',
    metric: 'eaneps_price_comparison_last_run_status',
    reducer: 'max',
  },
  {
    key: 'skippedRunTotal',
    label: '跳过运行次数',
    metric: 'eaneps_price_comparison_skipped_run_total',
    reducer: 'sum',
    tone: 'warning',
  },
  {
    key: 'querySupplierRequests',
    label: '业务查价开始数',
    metric: 'eaneps_price_comparison_query_supplier_requests',
    reducer: 'sum',
  },
  {
    key: 'querySupplierRequestCompleted',
    label: '业务查价完成数',
    metric: 'eaneps_price_comparison_query_supplier_request_completed',
    reducer: 'sum',
  },
  {
    key: 'querySupplierRequestPriced',
    label: '业务有价请求数',
    metric: 'eaneps_price_comparison_query_supplier_request_priced',
    reducer: 'sum',
    tone: 'success',
  },
  {
    key: 'querySupplierRequestNoPrice',
    label: '业务无价请求数',
    metric: 'eaneps_price_comparison_query_supplier_request_no_price',
    reducer: 'sum',
  },
  {
    key: 'priceComparableGroups',
    label: '比价可比组数',
    metric: 'eaneps_price_comparison_price_comparable_groups',
    reducer: 'sum',
  },
  {
    key: 'accountComparableGroups',
    label: '账号可比组数',
    metric: 'eaneps_price_comparison_account_comparable_groups',
    reducer: 'sum',
  },
  {
    key: 'standardPackageMissingGroups',
    label: '标准价缺套餐价',
    metric: 'eaneps_price_comparison_standard_package_missing_groups',
    reducer: 'sum',
    tone: 'warning',
  },
  {
    key: 'packageStandardMissingGroups',
    label: '套餐价缺标准价',
    metric: 'eaneps_price_comparison_package_standard_missing_groups',
    reducer: 'sum',
    tone: 'warning',
  },
  {
    key: 'eanepsAccountMissingGroups',
    label: 'EANEPS 缺账号价',
    metric: 'eaneps_price_comparison_eaneps_account_missing_groups',
    reducer: 'sum',
    tone: 'danger',
  },
  {
    key: 'eanbizAccountMissingGroups',
    label: 'EANBIZ 缺账号价',
    metric: 'eaneps_price_comparison_eanbiz_account_missing_groups',
    reducer: 'sum',
    tone: 'danger',
  },
] as const;

const TASK_TIME_DEFS = [
  {
    key: 'currentRunStartedUnix',
    label: '当前运行开始时间',
    metric: 'eaneps_price_comparison_current_run_started_unix_seconds',
  },
  {
    key: 'lastCompletedUnix',
    label: '最近完成时间',
    metric: 'eaneps_price_comparison_last_completed_unix_seconds',
  },
  {
    key: 'lastUpdatedUnix',
    label: '最近更新时间',
    metric: 'eaneps_price_comparison_last_updated_unix_seconds',
  },
] as const;

const QUEUE_METRICS = [
  {
    label: '酒店编码队列长度',
    metric: 'eaneps_price_comparison_hotel_code_queue_length',
  },
  {
    label: '酒店价格队列长度',
    metric: 'eaneps_price_comparison_hotel_price_queue_length',
  },
  {
    label: '比价队列长度',
    metric: 'eaneps_price_comparison_comparison_queue_length',
  },
] as const;

const STAGE_DEFS = [
  {
    extras: [],
    key: 'enqueue',
    label: 'enqueue',
  },
  {
    extras: [
      { label: '批次数', metric: 'eaneps_price_comparison_query_batches' },
      {
        label: '业务开始数',
        metric: 'eaneps_price_comparison_query_supplier_requests',
      },
      {
        label: '业务完成数',
        metric: 'eaneps_price_comparison_query_supplier_request_completed',
      },
      {
        label: '业务有价请求数',
        metric: 'eaneps_price_comparison_query_supplier_request_priced',
      },
      {
        label: '业务无价请求数',
        metric: 'eaneps_price_comparison_query_supplier_request_no_price',
      },
    ],
    key: 'query',
    label: 'query',
  },
  {
    extras: [
      {
        label: '价格可比组数',
        metric: 'eaneps_price_comparison_price_comparable_groups',
      },
      {
        label: '账号可比组数',
        metric: 'eaneps_price_comparison_account_comparable_groups',
      },
      {
        label: '标准价缺套餐价',
        metric: 'eaneps_price_comparison_standard_package_missing_groups',
      },
      {
        label: '套餐价缺标准价',
        metric: 'eaneps_price_comparison_package_standard_missing_groups',
      },
      {
        label: 'EANEPS 缺账号价',
        metric: 'eaneps_price_comparison_eaneps_account_missing_groups',
      },
      {
        label: 'EANBIZ 缺账号价',
        metric: 'eaneps_price_comparison_eanbiz_account_missing_groups',
      },
    ],
    key: 'compare',
    label: 'compare',
  },
  {
    extras: [
      { label: '批次数', metric: 'eaneps_price_comparison_insert_batches' },
    ],
    key: 'insert',
    label: 'insert',
  },
] as const;

const HTTP_OPERATION_OPTIONS = [
  'query_many_sup_hotel_price',
  'query_cache_hotel_price',
] as const;

const HTTP_OUTCOME_OPTIONS = [
  'priced',
  'no_price',
  'error',
  'timeout',
] as const;
const APP_METRICS_SCOPE_NAME = 'AppMetrics';
const TASK_TIME_LOOKBACK = '30d';
const HTTP_GROUP_BY = ['operation', 'sup_id', 'hotel_count'] as const;
const HTTP_OUTCOME_GROUP_BY = [
  'operation',
  'sup_id',
  'hotel_count',
  'outcome',
] as const;

const HTTP_REQUEST_MATCHERS = {
  completed: ['state="completed"'],
  error: ['state="completed"', 'outcome="error"'],
  executions: ['state="started"'],
  noPrice: ['state="completed"', 'outcome="no_price"'],
  priced: ['state="completed"', 'outcome="priced"'],
  timeout: ['state="completed"', 'outcome="timeout"'],
} as const;

const HTTP_METRIC_DISCOVERY_MATCHER =
  '{__name__=~"infrastructures_http_query_price.*"}';

const HTTP_METRIC_ALIASES = {
  durationBucket: [
    'infrastructures_http_query_price_duration_bucket',
    'infrastructures_http_query_price_duration_milliseconds_bucket',
    'infrastructures_http_query_price_duration_ms_bucket',
  ],
  durationCount: [
    'infrastructures_http_query_price_duration_count',
    'infrastructures_http_query_price_duration_milliseconds_count',
    'infrastructures_http_query_price_duration_ms_count',
  ],
  durationSum: [
    'infrastructures_http_query_price_duration_sum',
    'infrastructures_http_query_price_duration_milliseconds_sum',
    'infrastructures_http_query_price_duration_ms_sum',
  ],
  executions: ['infrastructures_http_query_price_executions_total'],
  requests: [
    'infrastructures_http_query_price_requests',
    'infrastructures_http_query_price_requests_total',
  ],
} as const;

const HTTP_METRIC_PATTERNS = {
  durationBucket: [
    /^infrastructures_http_query_price_duration(?:_(?:milliseconds|ms))?_bucket$/,
  ],
  durationCount: [
    /^infrastructures_http_query_price_duration(?:_(?:milliseconds|ms))?_count$/,
  ],
  durationSum: [
    /^infrastructures_http_query_price_duration(?:_(?:milliseconds|ms))?_sum$/,
  ],
  executions: [/^infrastructures_http_query_price_executions_total$/],
  requests: [/^infrastructures_http_query_price_requests(?:_total)?$/],
} as const;

function buildDefaultCustomRange(): [Date, Date] {
  return [new Date(Date.now() - 60 * 60 * 1000), new Date()];
}

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', String.raw`\"`);
}

function escapeRegexValue(value: string) {
  return value.replaceAll(/[|\\{}()[\]^$+*?.-]/g, String.raw`\$&`);
}

function buildScopeMatchers(scope: MetricScope) {
  return [
    `exported_instance="${escapeLabelValue(scope.exportedInstance)}"`,
    `exported_job="${escapeLabelValue(scope.exportedJob)}"`,
    `otel_scope_name="${escapeLabelValue(scope.otelScopeName)}"`,
  ];
}

function buildSelector(
  scope: MetricScope,
  metricName: string,
  extraMatchers: string[] = [],
) {
  return `${metricName}{${[...buildScopeMatchers(scope), ...extraMatchers].join(',')}}`;
}

function buildGroupExpr(expr: string, groupBy: string[] = []) {
  if (groupBy.length === 0) {
    return `sum(${expr})`;
  }

  return `sum by (${groupBy.join(', ')}) (${expr})`;
}

function buildGaugeQuery(
  scope: MetricScope,
  metricName: string,
  reducer: 'max' | 'sum' = 'sum',
) {
  const selector = buildSelector(scope, metricName);
  return `${reducer}(${selector})`;
}

function buildLastGaugeQuery(
  scope: MetricScope,
  metricName: string,
  reducer: 'max' | 'sum' = 'max',
  lookback = TASK_TIME_LOOKBACK,
) {
  const selector = buildSelector(scope, metricName);
  return `${reducer}(last_over_time(${selector}[${lookback}]))`;
}

function buildGroupedCurrentQuery(
  scope: MetricScope,
  metricName: string,
  groupBy: string[] = [],
  extraMatchers: string[] = [],
) {
  const selector = buildSelector(scope, metricName, extraMatchers);
  return buildGroupExpr(selector, groupBy);
}

function alignTimestampToStep(timestamp: number, step: number) {
  const safeStep = Math.max(step, 1);
  return Math.floor(timestamp / safeStep) * safeStep;
}

function buildRangeIncreaseQuery(
  scope: MetricScope,
  metricName: string,
  window: string,
  extraMatchers: string[] = [],
) {
  const selector = buildSelector(scope, metricName, extraMatchers);
  return `sum(increase(${selector}[${window}]))`;
}

function getVectorNumber(results: PrometheusVectorResult[]) {
  const raw = Number(results[0]?.value?.[1]);
  return Number.isFinite(raw) ? raw : null;
}

function getSampleNumber(result?: PrometheusVectorResult) {
  const raw = Number(result?.value?.[1]);
  return Number.isFinite(raw) ? raw : 0;
}

function toMetricKey(labels: Record<string, string>, keys: string[]) {
  return keys.map((key) => labels[key] || '').join('||');
}

function clampFilterValues(values: string[], options: string[]) {
  return values.filter((item) => options.includes(item));
}

function formatInteger(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }

  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 0 }).format(
    value,
  );
}

function formatDecimal(value: null | number, digits = 2) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }

  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: digits,
    minimumFractionDigits: 0,
  }).format(value);
}

function formatPercent(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }

  return `${(value * 100).toFixed(2)}%`;
}

function formatDuration(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }

  if (value >= 1000) {
    return `${formatDecimal(value / 1000, 2)} s`;
  }

  return `${formatDecimal(value, value >= 100 ? 0 : 2)} ms`;
}

function getLargestFiniteBucketUpperBound(bucketCounts: Map<number, number>) {
  const finiteBounds = [...bucketCounts.keys()].filter((item) =>
    Number.isFinite(item),
  );

  if (finiteBounds.length === 0) {
    return null;
  }

  return Math.max(...finiteBounds);
}

function formatBucketDuration(
  value: null | number,
  bucketCounts: Map<number, number>,
) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }

  if (value === Number.POSITIVE_INFINITY) {
    const upperBound = getLargestFiniteBucketUpperBound(bucketCounts);

    if (upperBound === null) {
      return '> 已配置最大桶';
    }

    return `> ${formatDuration(upperBound)}`;
  }

  return formatDuration(value);
}

function formatUnixSeconds(value: null | number) {
  if (value === null || value <= 0) {
    return '--';
  }

  return formatDateTime(value * 1000);
}

function formatTrendTime(value: number) {
  return formatDateTime(value);
}

function getCompletedCount(
  row: Pick<HttpQueryRow, 'error' | 'noPrice' | 'priced' | 'timeout'>,
) {
  return row.priced + row.noPrice + row.error + row.timeout;
}

function getUnfinishedCount(executions: number, completed: number) {
  return Math.max(executions - completed, 0);
}

function getConsistentRate(numerator: number, denominator: number) {
  if (denominator <= 0) {
    return null;
  }

  if (numerator > denominator) {
    return null;
  }

  return numerator / denominator;
}

function computeQuantile(
  bucketCounts: Map<number, number>,
  count: number,
  quantile: number,
) {
  if (
    !count ||
    count <= 0 ||
    bucketCounts.size === 0 ||
    quantile < 0 ||
    quantile > 1
  ) {
    return null;
  }

  const sorted = [...bucketCounts.entries()].sort((a, b) => a[0] - b[0]);
  const target = count * quantile;
  const matched = sorted.find(([, bucketValue]) => bucketValue >= target);

  if (!matched) {
    return null;
  }

  if (matched[0] === Number.POSITIVE_INFINITY) {
    return Number.POSITIVE_INFINITY;
  }

  return Number.isFinite(matched[0]) ? matched[0] : null;
}

function computeMaxUpperBound(
  bucketCounts: Map<number, number>,
  count: number,
) {
  if (!count || count <= 0 || bucketCounts.size === 0) {
    return null;
  }

  const sorted = [...bucketCounts.entries()].sort((a, b) => a[0] - b[0]);
  const matched = sorted.find(([, bucketValue]) => bucketValue >= count);

  if (!matched) {
    return null;
  }

  if (matched[0] === Number.POSITIVE_INFINITY) {
    return Number.POSITIVE_INFINITY;
  }

  return Number.isFinite(matched[0]) ? matched[0] : null;
}

function summarizeHttpRows(rows: HttpQueryRow[]): HttpSummary {
  const bucketCounts = new Map<number, number>();
  let executions = 0;
  let completed = 0;
  let priced = 0;
  let noPrice = 0;
  let error = 0;
  let timeout = 0;
  let durationCount = 0;
  let durationSum = 0;

  for (const row of rows) {
    executions += row.executions;
    completed += row.completed;
    priced += row.priced;
    noPrice += row.noPrice;
    error += row.error;
    timeout += row.timeout;
    durationCount += row.durationCount;
    durationSum += row.durationSum;

    for (const [upper, count] of row.bucketCounts.entries()) {
      bucketCounts.set(upper, (bucketCounts.get(upper) || 0) + count);
    }
  }

  const unfinished = getUnfinishedCount(executions, completed);

  return {
    avgMs: durationCount > 0 ? durationSum / durationCount : null,
    bucketCounts,
    completed,
    error,
    errorRate: getConsistentRate(error, completed),
    executions,
    maxMs: computeMaxUpperBound(bucketCounts, durationCount),
    noPrice,
    noPriceRate: getConsistentRate(noPrice, completed),
    p50Ms: computeQuantile(bucketCounts, durationCount, 0.5),
    p80Ms: computeQuantile(bucketCounts, durationCount, 0.8),
    p95Ms: computeQuantile(bucketCounts, durationCount, 0.95),
    p99Ms: computeQuantile(bucketCounts, durationCount, 0.99),
    priced,
    pricedRate: getConsistentRate(priced, completed),
    timeout,
    timeoutRate: getConsistentRate(timeout, completed),
    unfinished,
  };
}

function getSeriesPoints(
  result: PrometheusMatrixResult | PrometheusVectorResult,
) {
  if (Array.isArray((result as PrometheusMatrixResult).values)) {
    return (result as PrometheusMatrixResult).values;
  }

  if (Array.isArray((result as PrometheusVectorResult).value)) {
    return [(result as PrometheusVectorResult).value];
  }

  return [] as Array<[number, string]>;
}

function sumMatrixByTimestamp(results: PrometheusMatrixResult[]) {
  const bucket = new Map<number, number>();

  for (const result of results) {
    for (const [timestamp, rawValue] of getSeriesPoints(result)) {
      const time = Number(timestamp);
      const value = Number(rawValue);

      if (!Number.isFinite(time) || !Number.isFinite(value)) {
        continue;
      }

      bucket.set(time, (bucket.get(time) || 0) + value);
    }
  }

  return bucket;
}

function diffMatrixByTimestamp(
  results: PrometheusMatrixResult[],
  baselineTimestamp: number,
) {
  const deltaMap = new Map<number, number>();

  for (const result of results) {
    const points = getSeriesPoints(result)
      .map(
        ([timestamp, rawValue]) =>
          [Number(timestamp), Number(rawValue)] as const,
      )
      .filter(
        ([timestamp, value]) =>
          Number.isFinite(timestamp) && Number.isFinite(value),
      )
      .sort((a, b) => a[0] - b[0]);

    let previousValue: null | number = null;

    for (const [timestamp, currentValue] of points) {
      let delta = 0;

      if (previousValue === null) {
        delta = timestamp > baselineTimestamp ? Math.max(currentValue, 0) : 0;
      } else {
        delta = Math.max(currentValue - previousValue, 0);
      }

      deltaMap.set(timestamp, (deltaMap.get(timestamp) || 0) + delta);
      previousValue = currentValue;
    }
  }

  return deltaMap;
}

function buildTrendOption(
  series: SeriesOption[],
  formatter: (value: null | number) => string,
  yAxisName: string,
): EChartsOption {
  return {
    grid: {
      bottom: 20,
      containLabel: true,
      left: 20,
      right: 20,
      top: 36,
    },
    legend: {
      top: 0,
      type: 'scroll',
    },
    tooltip: {
      trigger: 'axis',
      formatter: (params: any) => {
        const list = Array.isArray(params) ? params : [params];
        if (list.length === 0) {
          return '';
        }

        const time = Number(list[0]?.value?.[0] || 0);
        const rows = list
          .map((item: any) => {
            const point = Array.isArray(item?.value) ? item.value : [];
            const raw = Number(point[1]);
            return `${item.marker}${item.seriesName}: ${formatter(Number.isFinite(raw) ? raw : null)}`;
          })
          .join('<br/>');

        return `${formatTrendTime(time)}<br/>${rows}`;
      },
    },
    xAxis: {
      type: 'time',
    },
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

function renderHttpTrendCharts() {
  const trendPoints = dashboardData.value?.httpTrendPoints || [];
  const latencyPointCount = trendPoints.filter(
    (item) => item.avgMs !== null,
  ).length;

  const countSeries: SeriesOption[] = [
    {
      data: trendPoints.map((item) => [item.timestamp, item.executions]),
      name: '执行数',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: trendPoints.map((item) => [item.timestamp, item.completed]),
      name: '完成数',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: trendPoints.map((item) => [item.timestamp, item.error]),
      name: '错误数',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: trendPoints.map((item) => [item.timestamp, item.timeout]),
      name: '超时数',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
  ];

  const latencySeries: SeriesOption[] = [
    {
      data: trendPoints.map((item) => [item.timestamp, item.avgMs]),
      name: '平均耗时',
      showSymbol: latencyPointCount <= 1,
      symbolSize: latencyPointCount <= 1 ? 8 : 4,
      smooth: true,
      type: 'line',
    },
  ];

  renderHttpCountTrend(
    buildTrendOption(countSeries, (value) => formatInteger(value), '次数'),
  );
  renderHttpLatencyTrend(
    buildTrendOption(latencySeries, (value) => formatDuration(value), 'ms'),
  );
}

function renderStageTrendCharts() {
  const trendPoints =
    dashboardData.value?.stageTrendMap[selectedStageTrendKey.value] || [];

  const stageSeries: SeriesOption[] = [
    {
      data: trendPoints.map((item) => [item.timestamp, item.processed]),
      name: '窗口完成酒店数',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: trendPoints.map((item) => [
        item.timestamp,
        item.processedPerSecond,
      ]),
      name: '每秒完成酒店数',
      showSymbol: false,
      smooth: true,
      type: 'line',
      yAxisIndex: 1,
    },
    {
      data: trendPoints.map((item) => [item.timestamp, item.running]),
      name: 'running',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: trendPoints.map((item) => [item.timestamp, item.remaining]),
      name: 'remaining',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
    {
      data: trendPoints.map((item) => [item.timestamp, item.failed]),
      name: 'failed',
      showSymbol: false,
      smooth: true,
      type: 'line',
    },
  ];

  renderStageTrend({
    grid: {
      bottom: 20,
      containLabel: true,
      left: 20,
      right: 20,
      top: 36,
    },
    legend: {
      top: 0,
      type: 'scroll',
    },
    tooltip: {
      trigger: 'axis',
      formatter: (params: any) => {
        const list = Array.isArray(params) ? params : [params];
        if (list.length === 0) {
          return '';
        }

        const time = Number(list[0]?.value?.[0] || 0);
        const rows = list
          .map((item: any) => {
            const point = Array.isArray(item?.value) ? item.value : [];
            const raw = Number(point[1]);
            const value = Number.isFinite(raw) ? raw : null;
            const text =
              item.seriesName === '每秒完成酒店数'
                ? formatDecimal(value, 2)
                : formatInteger(value);
            return `${item.marker}${item.seriesName}: ${text}`;
          })
          .join('<br/>');

        return `${formatTrendTime(time)}<br/>${rows}`;
      },
    },
    xAxis: {
      type: 'time',
    },
    yAxis: [
      {
        axisLabel: {
          formatter: (value: number) => formatInteger(value),
        },
        name: '酒店数',
        type: 'value',
      },
      {
        axisLabel: {
          formatter: (value: number) => formatDecimal(value, 2),
        },
        name: '个/秒',
        type: 'value',
      },
    ],
    series: stageSeries,
  });
}

function buildStageTrendPoint(
  timestamp: number,
  failed: number,
  processed: number,
  remaining: number,
  running: number,
  previousProcessed: null | number,
  previousTimestamp: null | number,
): StageTrendPoint {
  const timestampMs = timestamp * 1000;
  const elapsedSeconds =
    previousTimestamp === null ? 0 : Math.max(timestamp - previousTimestamp, 0);
  const processedDelta =
    previousProcessed === null ? 0 : Math.max(processed - previousProcessed, 0);

  return {
    failed,
    processed: processedDelta,
    processedPerSecond:
      elapsedSeconds > 0 ? processedDelta / elapsedSeconds : null,
    remaining,
    running,
    timeLabel: formatTrendTime(timestampMs),
    timestamp: timestampMs,
  };
}

function resolveMetricName(
  metricNames: string[],
  aliases: readonly string[],
  patterns: readonly RegExp[],
  metricKey: keyof HttpMetricNames,
) {
  const exact = aliases.find((name) => metricNames.includes(name));
  if (exact) {
    return exact;
  }

  const matched = metricNames.find((name) =>
    patterns.some((pattern) => pattern.test(name)),
  );
  if (matched) {
    return matched;
  }

  throw new Error(`Prometheus metric "${metricKey}" was not found.`);
}

function resolveOptionalMetricName(
  metricNames: string[],
  aliases: readonly string[],
  patterns: readonly RegExp[],
) {
  const exact = aliases.find((name) => metricNames.includes(name));
  if (exact) {
    return exact;
  }

  const matched = metricNames.find((name) =>
    patterns.some((pattern) => pattern.test(name)),
  );

  return matched || null;
}

async function discoverHttpMetricNames(forceRefresh = false) {
  if (httpMetricNamesCache && !forceRefresh) {
    return httpMetricNamesCache;
  }

  if (httpMetricNamesPromise && !forceRefresh) {
    return httpMetricNamesPromise;
  }

  httpMetricNamesPromise = (async () => {
    let metricNames: string[];

    try {
      metricNames = await client.labelValues('__name__', [
        HTTP_METRIC_DISCOVERY_MATCHER,
      ]);
    } catch {
      const allMetricNames = await client.labelValues('__name__');
      metricNames = allMetricNames.filter((name) =>
        name.startsWith('infrastructures_http_query_price'),
      );
    }

    const resolved: HttpMetricNames = {
      durationBucket: resolveMetricName(
        metricNames,
        HTTP_METRIC_ALIASES.durationBucket,
        HTTP_METRIC_PATTERNS.durationBucket,
        'durationBucket',
      ),
      durationCount: resolveMetricName(
        metricNames,
        HTTP_METRIC_ALIASES.durationCount,
        HTTP_METRIC_PATTERNS.durationCount,
        'durationCount',
      ),
      durationSum: resolveMetricName(
        metricNames,
        HTTP_METRIC_ALIASES.durationSum,
        HTTP_METRIC_PATTERNS.durationSum,
        'durationSum',
      ),
      executions: resolveOptionalMetricName(
        metricNames,
        HTTP_METRIC_ALIASES.executions,
        HTTP_METRIC_PATTERNS.executions,
      ),
      requests: resolveMetricName(
        metricNames,
        HTTP_METRIC_ALIASES.requests,
        HTTP_METRIC_PATTERNS.requests,
        'requests',
      ),
    };

    httpMetricNamesCache = resolved;
    return resolved;
  })();

  try {
    return await httpMetricNamesPromise;
  } finally {
    httpMetricNamesPromise = null;
  }
}

function buildHttpRow(
  rowKey: string,
  baseRows: Map<
    string,
    { hotelCount: string; operation: string; supId: string }
  >,
  executionsMap: Map<string, number>,
  outcomeMap: Map<string, Record<string, number>>,
  durationCountMap: Map<string, number>,
  durationSumMap: Map<string, number>,
  bucketMap: Map<string, Map<number, number>>,
): HttpQueryRow {
  const base = baseRows.get(rowKey) || {
    hotelCount: '--',
    operation: '--',
    supId: '--',
  };
  const outcomes = outcomeMap.get(rowKey) || {};
  const executions = executionsMap.get(rowKey) || 0;
  const priced = outcomes.priced || 0;
  const noPrice = outcomes.no_price || 0;
  const error = outcomes.error || 0;
  const timeout = outcomes.timeout || 0;
  const completed = getCompletedCount({ error, noPrice, priced, timeout });
  const unfinished = getUnfinishedCount(executions, completed);
  const durationCount = durationCountMap.get(rowKey) || 0;
  const durationSum = durationSumMap.get(rowKey) || 0;
  const bucketCounts = bucketMap.get(rowKey) || new Map<number, number>();

  return {
    avgMs: durationCount > 0 ? durationSum / durationCount : null,
    bucketCounts,
    completed,
    durationCount,
    durationSum,
    error,
    executions,
    hotelCount: base.hotelCount,
    maxMs: computeMaxUpperBound(bucketCounts, durationCount),
    noPrice,
    operation: base.operation,
    p50Ms: computeQuantile(bucketCounts, durationCount, 0.5),
    p80Ms: computeQuantile(bucketCounts, durationCount, 0.8),
    p95Ms: computeQuantile(bucketCounts, durationCount, 0.95),
    p99Ms: computeQuantile(bucketCounts, durationCount, 0.99),
    priced,
    supId: base.supId,
    timeout,
    unfinished,
  };
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}

function withExtraMatchers(
  baseMatchers: string[],
  extraMatchers: readonly string[],
) {
  return [...baseMatchers, ...extraMatchers];
}

const currentScope = computed<MetricScope | null>(() => {
  const exportedInstance = props.app?.instances[0] || props.app?.instance || '';
  const exportedJob = props.app?.jobs[0] || '';

  if (!exportedInstance || !exportedJob) {
    return null;
  }

  return {
    exportedInstance,
    exportedJob,
    otelScopeName: APP_METRICS_SCOPE_NAME,
  };
});

const scopeKey = computed(() => {
  if (!currentScope.value) {
    return '';
  }

  return `${currentScope.value.exportedInstance}|${currentScope.value.exportedJob}|${currentScope.value.otelScopeName}`;
});

const effectiveTimeRange = computed(() => {
  const nowSeconds = Math.floor((Date.now() + refreshEpoch.value * 0) / 1000);

  if (selectedRange.value === 'custom' && customRange.value.length === 2) {
    const rawStart = Math.floor(customRange.value[0].getTime() / 1000);
    const rawEnd = Math.floor(customRange.value[1].getTime() / 1000);
    const step = buildQueryStepSeconds(rawStart, rawEnd);
    const start = alignTimestampToStep(rawStart, step);
    const end = alignTimestampToStep(rawEnd, step);

    return {
      end,
      start,
      step,
      window: `${Math.max(end - start, 60)}s`,
    };
  }

  const durationMap = {
    '15m': 15 * 60,
    '1h': 60 * 60,
    '6h': 6 * 60 * 60,
    '24h': 24 * 60 * 60,
  } as const;
  const duration =
    durationMap[selectedRange.value as '1h' | '6h' | '15m' | '24h'];
  const step = buildQueryStepSeconds(nowSeconds - duration, nowSeconds);
  const end = alignTimestampToStep(nowSeconds, step);
  const start = end - duration;

  return {
    end,
    start,
    step,
    window: `${duration}s`,
  };
});

const httpTrendStepSeconds = computed(() => {
  if (selectedRange.value === 'custom') {
    const range = effectiveTimeRange.value.end - effectiveTimeRange.value.start;

    return range <= 24 * 60 * 60 ? 120 : 300;
  }

  if (selectedRange.value === '24h') {
    return 300;
  }

  return 120;
});

const operationOptions = computed(() => {
  const values = new Set<string>(HTTP_OPERATION_OPTIONS);

  for (const row of dashboardData.value?.httpRows || []) {
    if (row.operation && row.operation !== '--') {
      values.add(row.operation);
    }
  }

  return [...values];
});

const supIdOptions = computed(() => {
  const values = new Set<string>();

  for (const row of dashboardData.value?.httpRows || []) {
    if (row.supId && row.supId !== '--') {
      values.add(row.supId);
    }
  }

  return [...values].sort((a, b) =>
    a.localeCompare(b, 'zh-CN', { numeric: true }),
  );
});

const hotelCountOptions = computed(() => {
  const values = new Set<string>();

  for (const row of dashboardData.value?.httpRows || []) {
    if (row.hotelCount && row.hotelCount !== '--') {
      values.add(row.hotelCount);
    }
  }

  return [...values].sort((a, b) => Number(a) - Number(b));
});

function buildHttpDimensionMatchers() {
  const matchers: string[] = [];

  if (selectedOperations.value.length > 0) {
    matchers.push(
      `operation=~"${selectedOperations.value.map((item) => escapeLabelValue(escapeRegexValue(item))).join('|')}"`,
    );
  }

  if (selectedSupIds.value.length > 0) {
    matchers.push(
      `sup_id=~"${selectedSupIds.value.map((item) => escapeLabelValue(escapeRegexValue(item))).join('|')}"`,
    );
  }

  if (selectedHotelCounts.value.length > 0) {
    matchers.push(
      `hotel_count=~"${selectedHotelCounts.value.map((item) => escapeLabelValue(escapeRegexValue(item))).join('|')}"`,
    );
  }

  return matchers;
}

const dimensionFilteredHttpRows = computed(() => {
  return (dashboardData.value?.httpRows || []).filter((row) => {
    if (
      selectedOperations.value.length > 0 &&
      !selectedOperations.value.includes(row.operation)
    ) {
      return false;
    }

    if (
      selectedSupIds.value.length > 0 &&
      !selectedSupIds.value.includes(row.supId)
    ) {
      return false;
    }

    if (
      selectedHotelCounts.value.length > 0 &&
      !selectedHotelCounts.value.includes(row.hotelCount)
    ) {
      return false;
    }

    return true;
  });
});

const filteredHttpRows = computed(() => {
  return dimensionFilteredHttpRows.value.filter((row) => {
    if (selectedOutcomes.value.length === 0) {
      return true;
    }

    return selectedOutcomes.value.some((outcome) => {
      if (outcome === 'priced') {
        return row.priced > 0;
      }
      if (outcome === 'no_price') {
        return row.noPrice > 0;
      }
      if (outcome === 'error') {
        return row.error > 0;
      }
      if (outcome === 'timeout') {
        return row.timeout > 0;
      }
      return false;
    });
  });
});

const httpSummary = computed<HttpSummary>(() => {
  return summarizeHttpRows(dimensionFilteredHttpRows.value);
});

const filteredHttpSummary = computed<HttpSummary>(() => {
  return summarizeHttpRows(filteredHttpRows.value);
});

function getHttpTableSummary({ columns }: { columns: TableSummaryColumn[] }) {
  const summary = filteredHttpSummary.value;

  return columns.map((column, index) => {
    if (index === 0) {
      return '汇总';
    }

    if (column.label === 'completed') {
      return formatInteger(summary.completed);
    }

    if (column.label === 'error') {
      return formatInteger(summary.error);
    }

    if (column.label === 'executions') {
      return formatInteger(summary.executions);
    }

    if (column.label === 'no_price') {
      return formatInteger(summary.noPrice);
    }

    if (column.label === 'priced') {
      return formatInteger(summary.priced);
    }

    if (column.label === 'timeout') {
      return formatInteger(summary.timeout);
    }

    if (column.label === 'unfinished') {
      return formatInteger(summary.unfinished);
    }

    if (column.label === '无价率') {
      return formatPercent(summary.noPriceRate);
    }

    if (column.label === '平均耗时') {
      return formatDuration(summary.avgMs);
    }

    if (column.label === '超时率') {
      return formatPercent(summary.timeoutRate);
    }

    if (column.label === '错误率') {
      return formatPercent(summary.errorRate);
    }

    if (column.label === '有价率') {
      return formatPercent(summary.pricedRate);
    }

    if (column.label === 'P50') {
      return formatBucketDuration(summary.p50Ms, summary.bucketCounts);
    }

    if (column.label === 'P80') {
      return formatBucketDuration(summary.p80Ms, summary.bucketCounts);
    }

    if (column.label === 'P95') {
      return formatBucketDuration(summary.p95Ms, summary.bucketCounts);
    }

    if (column.label === 'P99') {
      return formatBucketDuration(summary.p99Ms, summary.bucketCounts);
    }

    if (column.label === '最大耗时') {
      return formatBucketDuration(summary.maxMs, summary.bucketCounts);
    }

    return '--';
  });
}

const selectedFilterSummary = computed(() => {
  const parts = [
    `operation ${selectedOperations.value.length > 0 ? selectedOperations.value.length : operationOptions.value.length}`,
    `sup_id ${selectedSupIds.value.length > 0 ? selectedSupIds.value.length : supIdOptions.value.length}`,
    `hotel_count ${selectedHotelCounts.value.length > 0 ? selectedHotelCounts.value.length : hotelCountOptions.value.length}`,
    `outcome ${selectedOutcomes.value.length > 0 ? selectedOutcomes.value.length : HTTP_OUTCOME_OPTIONS.length}`,
  ];

  return parts.join(' / ');
});

const hasUnfinished = computed(() => httpSummary.value.unfinished > 0);
const hasInconsistentHttpSummary = computed(
  () => httpSummary.value.completed > httpSummary.value.executions,
);
const stageTrendTableRows = computed(() => {
  return [
    ...(dashboardData.value?.stageTrendMap[selectedStageTrendKey.value] || []),
  ].reverse();
});
const httpTrendTableRows = computed(() => {
  return [...(dashboardData.value?.httpTrendPoints || [])].reverse();
});

async function loadDashboard(silent = false) {
  if (!currentScope.value) {
    dashboardData.value = null;
    return;
  }

  refreshEpoch.value += 1;

  const currentRequestId = ++requestId;

  if (!silent) {
    loading.value = true;
  }

  try {
    const scope = currentScope.value;
    const stageTrendWindow = `${Math.max(Number(effectiveTimeRange.value.step) || 60, 60)}s`;
    const httpTrendStep = httpTrendStepSeconds.value;
    const httpTrendVisibleStart = effectiveTimeRange.value.start;
    const httpTrendQueryStart = Math.max(
      httpTrendVisibleStart - httpTrendStep,
      0,
    );
    const httpMetricNames = await discoverHttpMetricNames();
    const httpDimensionMatchers = buildHttpDimensionMatchers();
    const executionsMetric = httpMetricNames.executions;
    const requestsMetric = httpMetricNames.requests;

    const taskMetricPromises = [
      ...TASK_METRIC_DEFS,
      ...TASK_TIME_DEFS,
      ...QUEUE_METRICS,
    ].map(async (item) => {
      const reducer = 'reducer' in item ? item.reducer : 'max';
      const itemKey = 'key' in item ? item.key : item.metric;
      const results = await client.query(
        'key' in item && TASK_TIME_DEFS.some((taskTime) => taskTime.key === item.key)
          ? buildLastGaugeQuery(scope, item.metric, reducer)
          : buildGaugeQuery(scope, item.metric, reducer),
        effectiveTimeRange.value.end,
      );
      return [itemKey, getVectorNumber(results)] as const;
    });

    const stageMetricPromises = STAGE_DEFS.flatMap((stage) => {
      const baseMetrics = [
        {
          key: `${stage.key}_running`,
          metric: `eaneps_price_comparison_${stage.key}_running`,
        },
        {
          key: `${stage.key}_processed`,
          metric: `eaneps_price_comparison_${stage.key}_processed`,
        },
        {
          key: `${stage.key}_remaining`,
          metric: `eaneps_price_comparison_${stage.key}_remaining`,
        },
        {
          key: `${stage.key}_failed`,
          metric: `eaneps_price_comparison_${stage.key}_failed`,
        },
      ];
      const extras = stage.extras.map((extra) => ({
        key: extra.metric,
        metric: extra.metric,
      }));

      return [...baseMetrics, ...extras].map(async (item) => {
        const results = await client.query(
          buildGaugeQuery(scope, item.metric, 'sum'),
          effectiveTimeRange.value.end,
        );
        return [item.key, getVectorNumber(results)] as const;
      });
    });

    const stageTrendPromises = Promise.all(
      STAGE_DEFS.map(async (stage) => {
        const [runningRows, processedRows, remainingRows, failedRows] =
          await Promise.all([
            client.queryRange({
              end: effectiveTimeRange.value.end,
              query: buildGroupExpr(
                buildSelector(
                  scope,
                  `eaneps_price_comparison_${stage.key}_running`,
                ),
              ),
              start: effectiveTimeRange.value.start,
              step: effectiveTimeRange.value.step,
            }),
            client.queryRange({
              end: effectiveTimeRange.value.end,
              query: buildGroupExpr(
                buildSelector(
                  scope,
                  `eaneps_price_comparison_${stage.key}_processed`,
                ),
              ),
              start: effectiveTimeRange.value.start,
              step: effectiveTimeRange.value.step,
            }),
            client.queryRange({
              end: effectiveTimeRange.value.end,
              query: buildGroupExpr(
                buildSelector(
                  scope,
                  `eaneps_price_comparison_${stage.key}_remaining`,
                ),
              ),
              start: effectiveTimeRange.value.start,
              step: effectiveTimeRange.value.step,
            }),
            client.queryRange({
              end: effectiveTimeRange.value.end,
              query: buildRangeIncreaseQuery(
                scope,
                `eaneps_price_comparison_${stage.key}_failed`,
                stageTrendWindow,
              ),
              start: effectiveTimeRange.value.start,
              step: effectiveTimeRange.value.step,
            }),
          ]);

        return {
          failedRows,
          key: stage.key,
          processedRows,
          remainingRows,
          runningRows,
        };
      }),
    );

    const snapshotHttpPromises = Promise.all([
      client.query(
        executionsMetric
          ? buildGroupedCurrentQuery(scope, executionsMetric, [
              ...HTTP_GROUP_BY,
            ])
          : buildGroupedCurrentQuery(
              scope,
              requestsMetric,
              [...HTTP_GROUP_BY],
              [...HTTP_REQUEST_MATCHERS.executions],
            ),
        effectiveTimeRange.value.end,
      ),
      client.query(
        buildGroupedCurrentQuery(
          scope,
          requestsMetric,
          [...HTTP_OUTCOME_GROUP_BY],
          [...HTTP_REQUEST_MATCHERS.completed],
        ),
        effectiveTimeRange.value.end,
      ),
      client.query(
        buildGroupedCurrentQuery(scope, httpMetricNames.durationCount, [
          ...HTTP_GROUP_BY,
        ]),
        effectiveTimeRange.value.end,
      ),
      client.query(
        buildGroupedCurrentQuery(scope, httpMetricNames.durationSum, [
          ...HTTP_GROUP_BY,
        ]),
        effectiveTimeRange.value.end,
      ),
      client.query(
        buildGroupedCurrentQuery(scope, httpMetricNames.durationBucket, [
          ...HTTP_GROUP_BY,
          'le',
        ]),
        effectiveTimeRange.value.end,
      ),
    ]);

    const trendHttpPromises = Promise.all([
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: executionsMetric
          ? buildGroupedCurrentQuery(
              scope,
              executionsMetric,
              [],
              httpDimensionMatchers,
            )
          : buildGroupedCurrentQuery(
              scope,
              requestsMetric,
              [],
              withExtraMatchers(
                httpDimensionMatchers,
                HTTP_REQUEST_MATCHERS.executions,
              ),
            ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: buildGroupedCurrentQuery(
          scope,
          requestsMetric,
          [],
          withExtraMatchers(
            httpDimensionMatchers,
            HTTP_REQUEST_MATCHERS.priced,
          ),
        ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: buildGroupedCurrentQuery(
          scope,
          requestsMetric,
          [],
          withExtraMatchers(
            httpDimensionMatchers,
            HTTP_REQUEST_MATCHERS.noPrice,
          ),
        ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: buildGroupedCurrentQuery(
          scope,
          requestsMetric,
          [],
          withExtraMatchers(httpDimensionMatchers, HTTP_REQUEST_MATCHERS.error),
        ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: buildGroupedCurrentQuery(
          scope,
          requestsMetric,
          [],
          withExtraMatchers(
            httpDimensionMatchers,
            HTTP_REQUEST_MATCHERS.timeout,
          ),
        ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: buildGroupedCurrentQuery(
          scope,
          httpMetricNames.durationCount,
          [],
          httpDimensionMatchers,
        ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
      client.queryRange({
        end: effectiveTimeRange.value.end,
        query: buildGroupedCurrentQuery(
          scope,
          httpMetricNames.durationSum,
          [],
          httpDimensionMatchers,
        ),
        start: httpTrendQueryStart,
        step: httpTrendStep,
      }),
    ]);

    const [
      taskEntries,
      stageEntries,
      stageTrendResults,
      snapshotHttpResults,
      trendHttpResults,
    ] = await Promise.all([
      Promise.all(taskMetricPromises),
      Promise.all(stageMetricPromises),
      stageTrendPromises,
      snapshotHttpPromises,
      trendHttpPromises,
    ]);

    const [
      executionsRows,
      outcomeRows,
      durationCountRows,
      durationSumRows,
      durationBucketRows,
    ] = snapshotHttpResults;

    const [
      trendExecutionRows,
      trendPricedRows,
      trendNoPriceRows,
      trendErrorRows,
      trendTimeoutRows,
      trendDurationCountRows,
      trendDurationSumRows,
    ] = trendHttpResults;

    if (currentRequestId !== requestId) {
      return;
    }

    const taskMetricMap = new Map(taskEntries);
    const stageMetricMap = new Map(stageEntries);
    const stageTrendMap = Object.fromEntries(
      stageTrendResults.map((item) => {
        const runningMap = sumMatrixByTimestamp(item.runningRows);
        const processedMap = sumMatrixByTimestamp(item.processedRows);
        const remainingMap = sumMatrixByTimestamp(item.remainingRows);
        const failedMap = sumMatrixByTimestamp(item.failedRows);
        const timestamps = new Set<number>([
          ...failedMap.keys(),
          ...processedMap.keys(),
          ...remainingMap.keys(),
          ...runningMap.keys(),
        ]);

        const sortedTimestamps = [...timestamps].sort((a, b) => a - b);
        const points = sortedTimestamps.map((timestamp, index) => {
          const previousTimestamp =
            index > 0 ? sortedTimestamps[index - 1]! : null;
          const previousProcessed =
            previousTimestamp === null
              ? null
              : (processedMap.get(previousTimestamp) ?? 0);

          return buildStageTrendPoint(
            timestamp,
            failedMap.get(timestamp) || 0,
            processedMap.get(timestamp) || 0,
            remainingMap.get(timestamp) || 0,
            runningMap.get(timestamp) || 0,
            previousProcessed,
            previousTimestamp,
          );
        });

        return [item.key, points];
      }),
    ) as Record<string, StageTrendPoint[]>;
    const baseRows = new Map<
      string,
      { hotelCount: string; operation: string; supId: string }
    >();
    const executionsMap = new Map<string, number>();
    const outcomeMap = new Map<string, Record<string, number>>();
    const durationCountMap = new Map<string, number>();
    const durationSumMap = new Map<string, number>();
    const bucketMap = new Map<string, Map<number, number>>();
    const rowKeys = new Set<string>();
    const trendExecutionsMap = diffMatrixByTimestamp(
      trendExecutionRows,
      httpTrendQueryStart,
    );
    const trendPricedMap = diffMatrixByTimestamp(
      trendPricedRows,
      httpTrendQueryStart,
    );
    const trendNoPriceMap = diffMatrixByTimestamp(
      trendNoPriceRows,
      httpTrendQueryStart,
    );
    const trendErrorMap = diffMatrixByTimestamp(
      trendErrorRows,
      httpTrendQueryStart,
    );
    const trendTimeoutMap = diffMatrixByTimestamp(
      trendTimeoutRows,
      httpTrendQueryStart,
    );
    const trendDurationCountMap = diffMatrixByTimestamp(
      trendDurationCountRows,
      httpTrendQueryStart,
    );
    const trendDurationSumMap = diffMatrixByTimestamp(
      trendDurationSumRows,
      httpTrendQueryStart,
    );
    const trendTimestamps = new Set<number>([
      ...trendDurationCountMap.keys(),
      ...trendDurationSumMap.keys(),
      ...trendErrorMap.keys(),
      ...trendExecutionsMap.keys(),
      ...trendNoPriceMap.keys(),
      ...trendPricedMap.keys(),
      ...trendTimeoutMap.keys(),
    ]);

    for (const row of executionsRows) {
      const rowKey = toMetricKey(row.metric, [
        'operation',
        'sup_id',
        'hotel_count',
      ]);
      rowKeys.add(rowKey);
      baseRows.set(rowKey, {
        hotelCount: row.metric.hotel_count || '--',
        operation: row.metric.operation || '--',
        supId: row.metric.sup_id || '--',
      });
      executionsMap.set(rowKey, getSampleNumber(row));
    }

    for (const row of outcomeRows) {
      const rowKey = toMetricKey(row.metric, [
        'operation',
        'sup_id',
        'hotel_count',
      ]);
      rowKeys.add(rowKey);
      if (!baseRows.has(rowKey)) {
        baseRows.set(rowKey, {
          hotelCount: row.metric.hotel_count || '--',
          operation: row.metric.operation || '--',
          supId: row.metric.sup_id || '--',
        });
      }
      const outcome = row.metric.outcome || '';
      const current = outcomeMap.get(rowKey) || {};
      current[outcome] = (current[outcome] || 0) + getSampleNumber(row);
      outcomeMap.set(rowKey, current);
    }

    for (const row of durationCountRows) {
      const rowKey = toMetricKey(row.metric, [
        'operation',
        'sup_id',
        'hotel_count',
      ]);
      rowKeys.add(rowKey);
      if (!baseRows.has(rowKey)) {
        baseRows.set(rowKey, {
          hotelCount: row.metric.hotel_count || '--',
          operation: row.metric.operation || '--',
          supId: row.metric.sup_id || '--',
        });
      }
      durationCountMap.set(rowKey, getSampleNumber(row));
    }

    for (const row of durationSumRows) {
      const rowKey = toMetricKey(row.metric, [
        'operation',
        'sup_id',
        'hotel_count',
      ]);
      rowKeys.add(rowKey);
      if (!baseRows.has(rowKey)) {
        baseRows.set(rowKey, {
          hotelCount: row.metric.hotel_count || '--',
          operation: row.metric.operation || '--',
          supId: row.metric.sup_id || '--',
        });
      }
      durationSumMap.set(rowKey, getSampleNumber(row));
    }

    for (const row of durationBucketRows) {
      const rowKey = toMetricKey(row.metric, [
        'operation',
        'sup_id',
        'hotel_count',
      ]);
      rowKeys.add(rowKey);
      if (!baseRows.has(rowKey)) {
        baseRows.set(rowKey, {
          hotelCount: row.metric.hotel_count || '--',
          operation: row.metric.operation || '--',
          supId: row.metric.sup_id || '--',
        });
      }

      const upperBound =
        row.metric.le === '+Inf'
          ? Number.POSITIVE_INFINITY
          : Number(row.metric.le);
      if (
        !Number.isFinite(upperBound) &&
        upperBound !== Number.POSITIVE_INFINITY
      ) {
        continue;
      }

      const current = bucketMap.get(rowKey) || new Map<number, number>();
      current.set(upperBound, getSampleNumber(row));
      bucketMap.set(rowKey, current);
    }

    const httpRows = [...rowKeys]
      .map((rowKey) =>
        buildHttpRow(
          rowKey,
          baseRows,
          executionsMap,
          outcomeMap,
          durationCountMap,
          durationSumMap,
          bucketMap,
        ),
      )
      .sort((a, b) => {
        if (b.unfinished !== a.unfinished) {
          return b.unfinished - a.unfinished;
        }
        if (b.error !== a.error) {
          return b.error - a.error;
        }
        if (b.timeout !== a.timeout) {
          return b.timeout - a.timeout;
        }
        return b.executions - a.executions;
      });

    const httpTrendPoints = [...trendTimestamps]
      .sort((a, b) => a - b)
      .filter((timestamp) => timestamp >= httpTrendVisibleStart)
      .map((timestamp) => {
        const executions = trendExecutionsMap.get(timestamp) || 0;
        const priced = trendPricedMap.get(timestamp) || 0;
        const noPrice = trendNoPriceMap.get(timestamp) || 0;
        const error = trendErrorMap.get(timestamp) || 0;
        const timeout = trendTimeoutMap.get(timestamp) || 0;
        const completed = priced + noPrice + error + timeout;
        const durationCount = trendDurationCountMap.get(timestamp) || 0;
        const durationSum = trendDurationSumMap.get(timestamp) || 0;
        const timestampMs = timestamp * 1000;

        return {
          avgMs: durationCount > 0 ? durationSum / durationCount : null,
          completed,
          error,
          executions,
          noPrice,
          priced,
          timeLabel: formatTrendTime(timestampMs),
          timeout,
          timestamp: timestampMs,
        };
      });

    dashboardData.value = {
      httpRows,
      httpTrendPoints,
      queueMetrics: QUEUE_METRICS.map((item) => ({
        label: item.label,
        value: taskMetricMap.get(item.metric) ?? null,
      })),
      stageRows: STAGE_DEFS.map((stage) => ({
        extraMetrics: stage.extras.map((item) => ({
          label: item.label,
          value: stageMetricMap.get(item.metric) ?? null,
        })),
        failed: stageMetricMap.get(`${stage.key}_failed`) ?? null,
        processed: stageMetricMap.get(`${stage.key}_processed`) ?? null,
        remaining: stageMetricMap.get(`${stage.key}_remaining`) ?? null,
        running: stageMetricMap.get(`${stage.key}_running`) ?? null,
        stage: stage.label,
      })),
      stageTrendMap,
      taskCards: TASK_METRIC_DEFS.map((item) => ({
        key: item.key,
        label: item.label,
        tone: 'tone' in item ? item.tone : undefined,
        value: taskMetricMap.get(item.key) ?? null,
      })),
      taskTimeline: TASK_TIME_DEFS.map((item) => ({
        label: item.label,
        value: taskMetricMap.get(item.key) ?? null,
      })),
    };

    selectedOperations.value = clampFilterValues(
      selectedOperations.value,
      operationOptions.value,
    );
    selectedSupIds.value = clampFilterValues(
      selectedSupIds.value,
      supIdOptions.value,
    );
    selectedHotelCounts.value = clampFilterValues(
      selectedHotelCounts.value,
      hotelCountOptions.value,
    );
    selectedOutcomes.value = clampFilterValues(selectedOutcomes.value, [
      ...HTTP_OUTCOME_OPTIONS,
    ]);
    errorMessage.value = '';
    lastLoadedAt.value = Date.now();
  } catch (error) {
    if (currentRequestId !== requestId) {
      return;
    }

    const message = getErrorMessage(error);
    errorMessage.value = message;

    if (silent) {
      ElMessage.error(`EANEPS 监控刷新失败: ${message}`);
    }
  } finally {
    if (!silent && currentRequestId === requestId) {
      loading.value = false;
    }
  }
}

async function refreshAll() {
  await loadDashboard();
}

watch(
  () => selectedRange.value,
  (value) => {
    if (value === 'custom' && customRange.value.length !== 2) {
      customRange.value = buildDefaultCustomRange();
    }
  },
  { immediate: true },
);

watch(
  () => scopeKey.value,
  async (value) => {
    dashboardData.value = null;
    errorMessage.value = '';

    if (!value) {
      return;
    }

    await loadDashboard();
  },
  { immediate: true },
);

watch(
  () => [
    selectedRange.value,
    customRange.value.map((item) => item.getTime()).join('|'),
  ],
  async () => {
    if (!scopeKey.value) {
      return;
    }

    await loadDashboard();
  },
);

watch(
  () => selectedStageTrendKey.value,
  async () => {
    await nextTick();
    renderStageTrendCharts();
  },
);

watch(
  () => dashboardData.value?.httpTrendPoints || [],
  async () => {
    await nextTick();
    renderHttpTrendCharts();
  },
  { deep: true },
);

watch(
  () => dashboardData.value?.stageTrendMap || {},
  async () => {
    await nextTick();
    renderStageTrendCharts();
  },
  { deep: true },
);

onMounted(() => {
  refreshTimer = window.setInterval(async () => {
    if (!autoRefreshEnabled.value || !scopeKey.value) {
      return;
    }

    await loadDashboard(true);
  }, 30_000);
});

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
  }
});
</script>

<template>
  <div class="eaneps-dashboard">
    <el-empty
      v-if="!currentScope"
      description="当前应用缺少 exported_instance 或 exported_job 标签，无法加载 EANEPS 业务监控面板。"
    />

    <template v-else>
      <el-card class="toolbar-card" shadow="never">
        <div class="toolbar-shell">
          <div class="toolbar-main">
            <div class="filter-group">
              <div class="filter-label">时间范围</div>
              <el-radio-group v-model="selectedRange" size="small">
                <el-radio-button
                  v-for="item in EXPEDIA_TIME_RANGE_OPTIONS"
                  :key="item.value"
                  :label="item.value"
                >
                  {{ item.label }}
                </el-radio-button>
              </el-radio-group>
            </div>

            <div
              v-if="selectedRange === 'custom'"
              class="filter-group filter-grow"
            >
              <div class="filter-label">自定义时间</div>
              <el-date-picker
                v-model="customRange"
                :clearable="false"
                end-placeholder="结束时间"
                format="YYYY-MM-DD HH:mm:ss"
                start-placeholder="开始时间"
                type="datetimerange"
                unlink-panels
              />
            </div>

            <div class="filter-group filter-grow">
              <div class="filter-label">operation</div>
              <el-select
                v-model="selectedOperations"
                clearable
                collapse-tags
                collapse-tags-tooltip
                filterable
                multiple
                placeholder="全部 operation"
              >
                <el-option
                  v-for="item in operationOptions"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </div>

            <div class="filter-group filter-grow">
              <div class="filter-label">sup_id</div>
              <el-select
                v-model="selectedSupIds"
                clearable
                collapse-tags
                collapse-tags-tooltip
                filterable
                multiple
                placeholder="全部 sup_id"
              >
                <el-option
                  v-for="item in supIdOptions"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </div>

            <div class="filter-group filter-grow">
              <div class="filter-label">hotel_count</div>
              <el-select
                v-model="selectedHotelCounts"
                clearable
                collapse-tags
                collapse-tags-tooltip
                filterable
                multiple
                placeholder="全部 hotel_count"
              >
                <el-option
                  v-for="item in hotelCountOptions"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </div>

            <div class="filter-group filter-grow">
              <div class="filter-label">outcome</div>
              <el-select
                v-model="selectedOutcomes"
                clearable
                collapse-tags
                collapse-tags-tooltip
                filterable
                multiple
                placeholder="全部 outcome"
              >
                <el-option
                  v-for="item in HTTP_OUTCOME_OPTIONS"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </div>

            <div class="filter-group">
              <div class="filter-label">自动刷新</div>
              <div class="refresh-box">
                <el-switch v-model="autoRefreshEnabled" />
                <span>30 秒</span>
              </div>
            </div>
          </div>

          <div class="toolbar-side">
            <div class="toolbar-meta">
              <span>HTTP 过滤：{{ selectedFilterSummary }}</span>
              <span>更新时间：{{ formatDateTime(lastLoadedAt) }}</span>
            </div>
            <el-button plain size="small" type="primary" @click="refreshAll">
              立即刷新
            </el-button>
          </div>
        </div>
      </el-card>

      <el-alert
        v-if="errorMessage"
        class="mt-3"
        :closable="false"
        show-icon
        type="error"
      >
        <template #title>Prometheus 请求失败：{{ errorMessage }}</template>
        <template #default>
          <el-button link type="primary" @click="refreshAll">重试</el-button>
        </template>
      </el-alert>

      <el-alert
        v-if="hasUnfinished"
        class="mt-3"
        :closable="false"
        show-icon
        type="warning"
        :title="`HTTP 查价存在未完成请求，unfinished = ${formatInteger(httpSummary.unfinished)}`"
      >
        <template #default>
          executions - (priced + no_price + error + timeout) =
          {{ formatInteger(httpSummary.unfinished) }}。
        </template>
      </el-alert>

      <el-alert
        v-if="hasInconsistentHttpSummary"
        class="mt-3"
        :closable="false"
        show-icon
        type="error"
        title="HTTP 查价汇总口径不一致：completed > executions"
      >
        <template #default>
          当前 completed 已统一为 priced + no_price + error + timeout。
          如果它仍超过 executions，说明时间窗口内存在 started / completed 跨窗。
        </template>
      </el-alert>

      <div v-loading="loading" class="dashboard-body">
        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">整体任务</div>
            <div class="section-desc">
              当前运行状态、核心产出指标和最近时间点。
            </div>
          </div>

          <div class="card-grid card-grid-4">
            <el-card
              v-for="item in dashboardData?.taskCards || []"
              :key="item.key"
              shadow="never"
            >
              <div class="metric-card">
                <div class="metric-label">{{ item.label }}</div>
                <div
                  class="metric-number"
                  :class="item.tone ? `tone-${item.tone}` : ''"
                >
                  {{ formatInteger(item.value) }}
                </div>
              </div>
            </el-card>
          </div>

          <div class="dual-grid">
            <el-card shadow="never">
              <template #header>任务时间点</template>
              <div
                v-for="item in dashboardData?.taskTimeline || []"
                :key="item.label"
                class="kv-row"
              >
                <span>{{ item.label }}</span>
                <span>{{ formatUnixSeconds(item.value) }}</span>
              </div>
            </el-card>

            <el-card shadow="never">
              <template #header>队列长度</template>
              <div
                v-for="item in dashboardData?.queueMetrics || []"
                :key="item.label"
                class="kv-row"
              >
                <span>{{ item.label }}</span>
                <span>{{ formatInteger(item.value) }}</span>
              </div>
            </el-card>
          </div>
        </section>

        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">阶段指标</div>
            <div class="section-desc">
              覆盖 enqueue / query / compare / insert 四个阶段的
              running、processed、remaining、failed 与附加指标。
            </div>
          </div>

          <el-card shadow="never">
            <el-table :data="dashboardData?.stageRows || []" stripe>
              <el-table-column label="阶段" min-width="120" prop="stage" />
              <el-table-column label="running" min-width="100">
                <template #default="scope">
                  {{ formatInteger(scope.row.running) }}
                </template>
              </el-table-column>
              <el-table-column label="processed" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.processed) }}
                </template>
              </el-table-column>
              <el-table-column label="remaining" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.remaining) }}
                </template>
              </el-table-column>
              <el-table-column label="failed" min-width="100">
                <template #default="scope">
                  <span class="text-danger">{{
                    formatInteger(scope.row.failed)
                  }}</span>
                </template>
              </el-table-column>
              <el-table-column label="附加指标" min-width="420">
                <template #default="scope">
                  <div class="extra-metrics">
                    <span
                      v-if="scope.row.extraMetrics.length === 0"
                      class="text-muted"
                    >
                      --
                    </span>
                    <el-tag
                      v-for="item in scope.row.extraMetrics"
                      :key="item.label"
                      class="extra-tag"
                      size="small"
                    >
                      {{ item.label }}: {{ formatInteger(item.value) }}
                    </el-tag>
                  </div>
                </template>
              </el-table-column>
            </el-table>
          </el-card>

          <div class="dual-grid">
            <el-card shadow="never">
              <template #header>
                <div class="trend-header">
                  <span>阶段窗口趋势</span>
                  <el-segmented
                    v-model="selectedStageTrendKey"
                    :options="
                      STAGE_DEFS.map((item) => ({
                        label: item.label,
                        value: item.key,
                      }))
                    "
                    size="small"
                  />
                </div>
              </template>
              <div class="trend-chart-shell">
                <EchartsUI ref="stageTrendRef" class="trend-chart" />
              </div>
            </el-card>

            <el-card shadow="never">
              <template #header>阶段窗口明细</template>
              <el-table
                :data="stageTrendTableRows"
                class="compact-detail-table"
                max-height="280"
                stripe
              >
                <el-table-column
                  label="时间"
                  min-width="180"
                  prop="timeLabel"
                />
                <el-table-column label="窗口完成酒店数" min-width="130">
                  <template #default="scope">
                    {{ formatInteger(scope.row.processed) }}
                  </template>
                </el-table-column>
                <el-table-column label="每秒完成酒店数" min-width="130">
                  <template #default="scope">
                    {{ formatDecimal(scope.row.processedPerSecond, 2) }}
                  </template>
                </el-table-column>
                <el-table-column label="running" min-width="100">
                  <template #default="scope">
                    {{ formatInteger(scope.row.running) }}
                  </template>
                </el-table-column>
                <el-table-column label="remaining" min-width="110">
                  <template #default="scope">
                    {{ formatInteger(scope.row.remaining) }}
                  </template>
                </el-table-column>
                <el-table-column label="failed" min-width="100">
                  <template #default="scope">
                    <span class="text-danger">{{
                      formatInteger(scope.row.failed)
                    }}</span>
                  </template>
                </el-table-column>
              </el-table>
            </el-card>
          </div>
        </section>

        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">HTTP 时间周期</div>
            <div class="section-desc">
              按当前时间范围分桶展示每个时间点的执行数、完成数、错误数、超时数和平均耗时。这里沿用
              operation、sup_id、hotel_count 筛选，不受 outcome 明细筛选影响。
            </div>
          </div>

          <div class="dual-grid">
            <el-card shadow="never">
              <template #header>完成与错误趋势</template>
              <div class="trend-chart-shell">
                <EchartsUI ref="httpCountTrendRef" class="trend-chart" />
              </div>
            </el-card>

            <el-card shadow="never">
              <template #header>平均耗时趋势</template>
              <div class="trend-chart-shell">
                <EchartsUI ref="httpLatencyTrendRef" class="trend-chart" />
              </div>
            </el-card>
          </div>

          <el-card shadow="never">
            <template #header>时间分桶明细</template>
            <el-table
              :data="httpTrendTableRows"
              class="compact-detail-table"
              max-height="280"
              stripe
            >
              <el-table-column label="时间" min-width="180" prop="timeLabel" />
              <el-table-column label="executions" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.executions) }}
                </template>
              </el-table-column>
              <el-table-column label="completed" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.completed) }}
                </template>
              </el-table-column>
              <el-table-column label="priced" min-width="100">
                <template #default="scope">
                  {{ formatInteger(scope.row.priced) }}
                </template>
              </el-table-column>
              <el-table-column label="no_price" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.noPrice) }}
                </template>
              </el-table-column>
              <el-table-column label="error" min-width="100">
                <template #default="scope">
                  <span class="text-danger">{{
                    formatInteger(scope.row.error)
                  }}</span>
                </template>
              </el-table-column>
              <el-table-column label="timeout" min-width="100">
                <template #default="scope">
                  <span class="text-warning">{{
                    formatInteger(scope.row.timeout)
                  }}</span>
                </template>
              </el-table-column>
              <el-table-column label="平均耗时" min-width="120">
                <template #default="scope">
                  {{ formatDuration(scope.row.avgMs) }}
                </template>
              </el-table-column>
            </el-table>
          </el-card>
        </section>

        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">HTTP 查价明细</div>
            <div class="section-desc">
              按 operation、sup_id、hotel_count
              聚合，展示执行、结果、一致性和耗时。
            </div>
          </div>

          <el-card shadow="never">
            <el-table
              :data="filteredHttpRows"
              :summary-method="getHttpTableSummary"
              show-summary
              stripe
              sum-text="汇总"
            >
              <el-table-column
                label="operation"
                min-width="220"
                prop="operation"
              />
              <el-table-column label="sup_id" min-width="120" prop="supId" />
              <el-table-column
                label="hotel_count"
                min-width="120"
                prop="hotelCount"
              />
              <el-table-column label="executions" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.executions) }}
                </template>
              </el-table-column>
              <el-table-column label="completed" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.completed) }}
                </template>
              </el-table-column>
              <el-table-column label="priced" min-width="100">
                <template #default="scope">
                  {{ formatInteger(scope.row.priced) }}
                </template>
              </el-table-column>
              <el-table-column label="no_price" min-width="110">
                <template #default="scope">
                  {{ formatInteger(scope.row.noPrice) }}
                </template>
              </el-table-column>
              <el-table-column label="error" min-width="100">
                <template #default="scope">
                  <span class="text-danger">{{
                    formatInteger(scope.row.error)
                  }}</span>
                </template>
              </el-table-column>
              <el-table-column label="timeout" min-width="100">
                <template #default="scope">
                  <span class="text-warning">{{
                    formatInteger(scope.row.timeout)
                  }}</span>
                </template>
              </el-table-column>
              <el-table-column label="unfinished" min-width="110">
                <template #default="scope">
                  <span :class="scope.row.unfinished > 0 ? 'text-warning' : ''">
                    {{ formatInteger(scope.row.unfinished) }}
                  </span>
                </template>
              </el-table-column>
              <el-table-column label="有价率" min-width="100">
                <template #default="scope">
                  {{
                    formatPercent(
                      getConsistentRate(scope.row.priced, scope.row.completed),
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="无价率" min-width="100">
                <template #default="scope">
                  {{
                    formatPercent(
                      getConsistentRate(scope.row.noPrice, scope.row.completed),
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="错误率" min-width="100">
                <template #default="scope">
                  {{
                    formatPercent(
                      getConsistentRate(scope.row.error, scope.row.completed),
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="超时率" min-width="100">
                <template #default="scope">
                  {{
                    formatPercent(
                      getConsistentRate(scope.row.timeout, scope.row.completed),
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="平均耗时" min-width="120">
                <template #default="scope">
                  {{ formatDuration(scope.row.avgMs) }}
                </template>
              </el-table-column>
              <el-table-column label="P50" min-width="110">
                <template #default="scope">
                  {{
                    formatBucketDuration(
                      scope.row.p50Ms,
                      scope.row.bucketCounts,
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="P80" min-width="110">
                <template #default="scope">
                  {{
                    formatBucketDuration(
                      scope.row.p80Ms,
                      scope.row.bucketCounts,
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="P95" min-width="110">
                <template #default="scope">
                  {{
                    formatBucketDuration(
                      scope.row.p95Ms,
                      scope.row.bucketCounts,
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="P99" min-width="110">
                <template #default="scope">
                  {{
                    formatBucketDuration(
                      scope.row.p99Ms,
                      scope.row.bucketCounts,
                    )
                  }}
                </template>
              </el-table-column>
              <el-table-column label="最大耗时" min-width="120">
                <template #default="scope">
                  {{
                    formatBucketDuration(
                      scope.row.maxMs,
                      scope.row.bucketCounts,
                    )
                  }}
                </template>
              </el-table-column>
            </el-table>
          </el-card>
        </section>
      </div>
    </template>
  </div>
</template>

<style scoped>
.eaneps-dashboard {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.toolbar-card {
  border-radius: 12px;
}

.toolbar-shell {
  display: flex;
  gap: 16px;
  justify-content: space-between;
}

.toolbar-main {
  display: flex;
  flex: 1;
  flex-wrap: wrap;
  gap: 12px;
}

.toolbar-side {
  display: flex;
  flex-direction: column;
  gap: 10px;
  align-items: flex-end;
  justify-content: space-between;
  min-width: 240px;
}

.toolbar-meta {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  text-align: right;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.filter-grow {
  flex: 1;
  min-width: 220px;
}

.filter-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.refresh-box {
  display: flex;
  gap: 10px;
  align-items: center;
  height: 32px;
}

.dashboard-body {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.dashboard-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.section-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.section-desc {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.card-grid {
  display: grid;
  gap: 12px;
}

.card-grid-4 {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.card-grid-5 {
  grid-template-columns: repeat(5, minmax(0, 1fr));
}

.metric-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.metric-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-number {
  font-size: 22px;
  font-weight: 700;
  color: var(--el-text-color-primary);
  word-break: break-all;
}

.metric-sub {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.dual-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.kv-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  line-height: 28px;
}

.extra-metrics {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.extra-tag {
  margin-right: 0;
}

.trend-chart-shell {
  min-height: 320px;
}

.trend-chart {
  width: 100%;
  height: 320px;
}

.compact-detail-table {
  width: 100%;
}

.text-danger,
.tone-danger {
  color: var(--el-color-danger);
}

.text-warning,
.tone-warning {
  color: var(--el-color-warning);
}

.tone-success {
  color: var(--el-color-success);
}

.tone-primary {
  color: var(--el-color-primary);
}

.text-muted {
  color: var(--el-text-color-secondary);
}

@media (max-width: 1600px) {
  .card-grid-5 {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 1280px) {
  .card-grid-4,
  .card-grid-5,
  .dual-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 992px) {
  .toolbar-shell {
    flex-direction: column;
  }

  .toolbar-side {
    align-items: stretch;
    min-width: 0;
  }

  .toolbar-meta {
    text-align: left;
  }

  .card-grid-4,
  .card-grid-5,
  .dual-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
