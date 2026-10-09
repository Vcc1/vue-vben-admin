import type {
  ExpediaAccountMetricFocusKey,
  ExpediaLatencyChartKey,
  ExpediaPendingPriority,
  ExpediaResolvedMetricNames,
  ExpediaSessionEvent,
  ExpediaTaskResult,
  ExpediaTimeRangeKey,
} from './types';

export const EXPEDIA_TIME_RANGE_OPTIONS: Array<{
  label: string;
  value: ExpediaTimeRangeKey;
}> = [
  { label: '近 15 分钟', value: '15m' },
  { label: '近 1 小时', value: '1h' },
  { label: '近 6 小时', value: '6h' },
  { label: '近 24 小时', value: '24h' },
  { label: '自定义', value: 'custom' },
];

export const EXPEDIA_PENDING_PRIORITY_ORDER: ExpediaPendingPriority[] = ['high', 'normal'];

export const EXPEDIA_PENDING_PRIORITY_META: Record<
  ExpediaPendingPriority,
  { color: string; label: string }
> = {
  high: {
    color: '#f56c6c',
    label: '高优先级待执行任务',
  },
  normal: {
    color: '#e6a23c',
    label: '普通待执行任务',
  },
};

export const EXPEDIA_TASK_RESULT_ORDER: ExpediaTaskResult[] = [
  'success',
  'no_price',
  'offline',
  'error',
];

export const EXPEDIA_TASK_RESULT_META: Record<
  ExpediaTaskResult,
  {
    color: string;
    label: string;
    overviewLabel: string;
  }
> = {
  error: {
    color: '#f56c6c',
    label: 'error',
    overviewLabel: '最近 1 小时 error 数',
  },
  no_price: {
    color: '#909399',
    label: 'no_price',
    overviewLabel: '最近 1 小时 no_price 数',
  },
  offline: {
    color: '#e6a23c',
    label: 'offline',
    overviewLabel: '最近 1 小时 offline 数',
  },
  success: {
    color: '#67c23a',
    label: 'success',
    overviewLabel: '最近 1 小时 success 数',
  },
};

export const EXPEDIA_SESSION_EVENT_ORDER: ExpediaSessionEvent[] = [
  'cache_hit_memory',
  'cache_hit_db',
  'cache_hit_file',
  'login_refresh',
  'shared_sha_refresh',
];

export const EXPEDIA_SESSION_EVENT_META: Record<
  ExpediaSessionEvent,
  { color: string; label: string }
> = {
  cache_hit_db: {
    color: '#409eff',
    label: 'cache_hit_db',
  },
  cache_hit_file: {
    color: '#13c2c2',
    label: 'cache_hit_file',
  },
  cache_hit_memory: {
    color: '#67c23a',
    label: 'cache_hit_memory',
  },
  login_refresh: {
    color: '#e6a23c',
    label: 'login_refresh',
  },
  shared_sha_refresh: {
    color: '#722ed1',
    label: 'shared_sha_refresh',
  },
};

export const EXPEDIA_LATENCY_CHART_META: Array<{
  key: ExpediaLatencyChartKey;
  label: string;
  matcherMode: 'eq' | 'regex';
  matcherValue: string;
}> = [
  {
    key: 'query',
    label: 'Query 耗时',
    matcherMode: 'eq',
    matcherValue: 'query',
  },
  {
    key: 'persist',
    label: 'Persist 耗时',
    matcherMode: 'eq',
    matcherValue: 'persist',
  },
  {
    key: 'login',
    label: 'Login 耗时',
    matcherMode: 'eq',
    matcherValue: 'login',
  },
  {
    key: 'callback',
    label: 'Callback 耗时',
    matcherMode: 'regex',
    matcherValue: 'success_callback|error_callback',
  },
];

export const EXPEDIA_LATENCY_SERIES_META = {
  avg: {
    color: '#13c2c2',
    label: '平均耗时',
  },
  p50: {
    color: '#409eff',
    label: 'P50',
  },
  p95: {
    color: '#f56c6c',
    label: 'P95',
  },
} as const;

export const EXPEDIA_OVERVIEW_ACCOUNT_METRICS: Record<
  ExpediaAccountMetricFocusKey,
  { tone: 'primary' | 'warning'; label: string }
> = {
  inProgressTaskCount: {
    label: '当前处理中任务总数',
    tone: 'warning',
  },
  workerCount: {
    label: '当前 worker 总数',
    tone: 'primary',
  },
};

type MetricKey = keyof ExpediaResolvedMetricNames;

const EXPEDIA_METRIC_ALIASES: Record<MetricKey, string[]> = {
  accountInProgressTaskCount: [
    'expedia_taap_account_in_progress_task_count',
    'expedia_taap_account_in_progress_task_count_tasks',
  ],
  accountWorkerCount: [
    'expedia_taap_account_worker_count',
    'expedia_taap_account_worker_count_workers',
  ],
  dbPendingTaskCount: [
    'expedia_chase_room_db_pending_task_count',
    'expedia_chase_room_db_pending_task_count_tasks',
  ],
  operationDurationBucket: [
    'expedia_chase_room_operation_duration_bucket',
    'expedia_chase_room_operation_duration_milliseconds_bucket',
  ],
  operationDurationCount: [
    'expedia_chase_room_operation_duration_count',
    'expedia_chase_room_operation_duration_milliseconds_count',
  ],
  operationDurationSum: [
    'expedia_chase_room_operation_duration_sum',
    'expedia_chase_room_operation_duration_milliseconds_sum',
  ],
  sessionTotal: ['expedia_taap_session_total', 'expedia_taap_session_events_total'],
  taskTotal: ['expedia_chase_room_task_total', 'expedia_chase_room_task_tasks_total'],
};

const EXPEDIA_METRIC_PATTERNS: Record<MetricKey, RegExp[]> = {
  accountInProgressTaskCount: [/^expedia_taap_account_in_progress_task_count(?:_tasks)?$/],
  accountWorkerCount: [/^expedia_taap_account_worker_count(?:_workers)?$/],
  dbPendingTaskCount: [/^expedia_chase_room_db_pending_task_count(?:_tasks)?$/],
  operationDurationBucket: [
    /^expedia_chase_room_operation_duration(?:_[a-z]+)?_bucket$/,
    /^expedia_chase_room_operation_duration_milliseconds_bucket$/,
  ],
  operationDurationCount: [
    /^expedia_chase_room_operation_duration(?:_[a-z]+)?_count$/,
    /^expedia_chase_room_operation_duration_milliseconds_count$/,
  ],
  operationDurationSum: [
    /^expedia_chase_room_operation_duration(?:_[a-z]+)?_sum$/,
    /^expedia_chase_room_operation_duration_milliseconds_sum$/,
  ],
  sessionTotal: [/^expedia_taap_session(?:_events)?_total$/],
  taskTotal: [/^expedia_chase_room_task(?:_tasks)?_total$/],
};

function resolveSingleMetric(
  metricNames: string[],
  aliases: string[],
  patterns: RegExp[],
  metricKey: MetricKey,
) {
  const exact = aliases.find((name) => metricNames.includes(name));
  if (exact) {
    return exact;
  }

  const matched = metricNames.find((name) => patterns.some((pattern) => pattern.test(name)));
  if (matched) {
    return matched;
  }

  throw new Error(`Prometheus metric "${metricKey}" was not found.`);
}

export function resolveExpediaMetricNames(metricNames: string[]): ExpediaResolvedMetricNames {
  return {
    accountInProgressTaskCount: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.accountInProgressTaskCount,
      EXPEDIA_METRIC_PATTERNS.accountInProgressTaskCount,
      'accountInProgressTaskCount',
    ),
    accountWorkerCount: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.accountWorkerCount,
      EXPEDIA_METRIC_PATTERNS.accountWorkerCount,
      'accountWorkerCount',
    ),
    dbPendingTaskCount: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.dbPendingTaskCount,
      EXPEDIA_METRIC_PATTERNS.dbPendingTaskCount,
      'dbPendingTaskCount',
    ),
    operationDurationBucket: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.operationDurationBucket,
      EXPEDIA_METRIC_PATTERNS.operationDurationBucket,
      'operationDurationBucket',
    ),
    operationDurationCount: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.operationDurationCount,
      EXPEDIA_METRIC_PATTERNS.operationDurationCount,
      'operationDurationCount',
    ),
    operationDurationSum: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.operationDurationSum,
      EXPEDIA_METRIC_PATTERNS.operationDurationSum,
      'operationDurationSum',
    ),
    sessionTotal: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.sessionTotal,
      EXPEDIA_METRIC_PATTERNS.sessionTotal,
      'sessionTotal',
    ),
    taskTotal: resolveSingleMetric(
      metricNames,
      EXPEDIA_METRIC_ALIASES.taskTotal,
      EXPEDIA_METRIC_PATTERNS.taskTotal,
      'taskTotal',
    ),
  };
}
