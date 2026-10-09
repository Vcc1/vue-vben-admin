export type ExpediaTimeRangeKey = '15m' | '1h' | '6h' | '24h' | 'custom';

export interface ExpediaDashboardTargetScope {
  exportedInstance: string;
  exportedJob: string;
  otelScopeName?: string;
}

export interface ExpediaResolvedMetricNames {
  accountInProgressTaskCount: string;
  accountWorkerCount: string;
  dbPendingTaskCount: string;
  operationDurationBucket: string;
  operationDurationCount: string;
  operationDurationSum: string;
  sessionTotal: string;
  taskTotal: string;
}

export type ExpediaPendingPriority = 'high' | 'normal';

export type ExpediaTaskResult = 'error' | 'no_price' | 'offline' | 'success';

export type ExpediaSessionEvent =
  | 'cache_hit_db'
  | 'cache_hit_file'
  | 'cache_hit_memory'
  | 'login_refresh'
  | 'shared_sha_refresh';

export type ExpediaLatencyChartKey = 'callback' | 'login' | 'persist' | 'query';

export type ExpediaAccountMetricFocusKey = 'inProgressTaskCount' | 'workerCount';

export type ExpediaAccountTableSortKey =
  | 'accountName'
  | 'cacheHitDbCount'
  | 'cacheHitFileCount'
  | 'cacheHitMemoryCount'
  | 'error15m'
  | 'inProgressTaskCount'
  | 'loginRefreshCount'
  | 'noPrice15m'
  | 'offline15m'
  | 'persistAvgMs'
  | 'queryAvgMs'
  | 'queryP95Ms'
  | 'sharedShaRefreshCount'
  | 'success15m'
  | 'workerCount';

export type ExpediaOverviewFilter =
  | { kind: 'accountMetric'; value: ExpediaAccountMetricFocusKey }
  | { kind: 'pendingPriority'; value: ExpediaPendingPriority }
  | { kind: 'taskResult'; value: ExpediaTaskResult }
  | null;

export interface ExpediaOverviewCard {
  description: string;
  filter: ExpediaOverviewFilter;
  key: string;
  label: string;
  tone: 'danger' | 'info' | 'primary' | 'success' | 'warning';
  value: number;
}

export interface ExpediaLineSeries {
  color: string;
  data: Array<[number, number]>;
  key: string;
  name: string;
}

export interface ExpediaLatencyChartData {
  key: ExpediaLatencyChartKey;
  label: string;
  series: ExpediaLineSeries[];
}

export interface ExpediaAccountTableRow {
  accountName: string;
  cacheHitDbCount: number;
  cacheHitFileCount: number;
  cacheHitMemoryCount: number;
  error15m: number;
  inProgressTaskCount: number;
  loginRefreshCount: number;
  noPrice15m: number;
  offline15m: number;
  persistAvgMs: number;
  queryAvgMs: number;
  queryP95Ms: number;
  sharedShaRefreshCount: number;
  success15m: number;
  workerCount: number;
}

export interface ExpediaResolvedTimeRange {
  end: number;
  key: ExpediaTimeRangeKey;
  start: number;
  step: number;
}

export interface ExpediaDashboardData {
  accountRows: ExpediaAccountTableRow[];
  hasAnyData: boolean;
  latencyCharts: ExpediaLatencyChartData[];
  metricNames: ExpediaResolvedMetricNames;
  overviewCards: ExpediaOverviewCard[];
  pendingTrend: ExpediaLineSeries[];
  resultTrend: ExpediaLineSeries[];
  sessionTrend: ExpediaLineSeries[];
}
