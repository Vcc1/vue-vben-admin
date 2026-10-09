import type {
  PrometheusMatrixResult,
  PrometheusValue,
  PrometheusVectorResult,
} from '#/api/prometheus/types';
import type {
  ExpediaAccountTableRow,
  ExpediaDashboardData,
  ExpediaDashboardTargetScope,
  ExpediaLatencyChartData,
  ExpediaLineSeries,
  ExpediaOverviewCard,
  ExpediaResolvedMetricNames,
  ExpediaResolvedTimeRange,
} from './types';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import {
  EXPEDIA_LATENCY_CHART_META,
  EXPEDIA_LATENCY_SERIES_META,
  EXPEDIA_OVERVIEW_ACCOUNT_METRICS,
  EXPEDIA_PENDING_PRIORITY_META,
  EXPEDIA_PENDING_PRIORITY_ORDER,
  EXPEDIA_SESSION_EVENT_META,
  EXPEDIA_SESSION_EVENT_ORDER,
  EXPEDIA_TASK_RESULT_META,
  EXPEDIA_TASK_RESULT_ORDER,
  resolveExpediaMetricNames,
} from './metrics';
import { buildTimelineMillis, normalizeNumber } from './formatters';
import {
  buildGaugeInstantQuery,
  buildGaugeRangeQuery,
  buildHistogramAverageInstantQuery,
  buildHistogramAverageRangeQuery,
  buildHistogramQuantileInstantQuery,
  buildHistogramQuantileRangeQuery,
  buildIncreaseInstantQuery,
  buildIncreaseRangeQuery,
  buildScopedMetricMatchers,
} from './queries';

const client = new PrometheusClient();
const EXPEDIA_METRIC_DISCOVERY_MATCHER = '{__name__=~"expedia_.*"}';

let metricNamesCache: ExpediaResolvedMetricNames | null = null;
let metricNamesPromise: null | Promise<ExpediaResolvedMetricNames> = null;

function readVectorValue(value?: null | PrometheusValue) {
  return normalizeNumber(value?.[1]);
}

function mapVectorByLabel(results: PrometheusVectorResult[], labelKey: string) {
  const output = new Map<string, number>();

  for (const row of results) {
    const labelValue = row.metric[labelKey];
    if (!labelValue) {
      continue;
    }

    output.set(labelValue, (output.get(labelValue) || 0) + readVectorValue(row.value));
  }

  return output;
}

function mapNestedVector(
  results: PrometheusVectorResult[],
  firstLabel: string,
  secondLabel: string,
) {
  const output = new Map<string, Map<string, number>>();

  for (const row of results) {
    const firstValue = row.metric[firstLabel];
    const secondValue = row.metric[secondLabel];

    if (!firstValue || !secondValue) {
      continue;
    }

    const current = output.get(firstValue) || new Map<string, number>();
    current.set(secondValue, (current.get(secondValue) || 0) + readVectorValue(row.value));
    output.set(firstValue, current);
  }

  return output;
}

function buildSeriesForOrderedLabels(
  results: PrometheusMatrixResult[],
  labelKey: string,
  orderedLabels: string[],
  metaMap: Record<string, { color: string; label: string }>,
  timeline: number[],
) {
  const rowMap = new Map<string, Map<number, number>>();

  for (const row of results) {
    const labelValue = row.metric[labelKey];
    if (!labelValue) {
      continue;
    }

    const current = rowMap.get(labelValue) || new Map<number, number>();
    for (const [timestamp, rawValue] of row.values) {
      current.set(timestamp * 1000, normalizeNumber(rawValue));
    }
    rowMap.set(labelValue, current);
  }

  return orderedLabels.map<ExpediaLineSeries>((labelValue) => {
    const pointMap = rowMap.get(labelValue) || new Map<number, number>();
    const meta = metaMap[labelValue];

    return {
      color: meta?.color || '#909399',
      data: timeline.map((timestamp) => [timestamp, pointMap.get(timestamp) || 0]),
      key: labelValue,
      name: meta?.label || labelValue,
    };
  });
}

function buildSingleAggregatedSeries(
  results: PrometheusMatrixResult[],
  timeline: number[],
  seriesKey: string,
  seriesLabel: string,
  color: string,
) {
  const pointMap = new Map<number, number>();

  for (const row of results) {
    for (const [timestamp, rawValue] of row.values) {
      const millis = timestamp * 1000;
      pointMap.set(millis, (pointMap.get(millis) || 0) + normalizeNumber(rawValue));
    }
  }

  return {
    color,
    data: timeline.map((timestamp) => [timestamp, pointMap.get(timestamp) || 0]) as Array<
      [number, number]
    >,
    key: seriesKey,
    name: seriesLabel,
  };
}

function collectAccountsFromResults(
  seedAccounts: string[],
  vectorGroups: Array<PrometheusVectorResult[]>,
) {
  const accountSet = new Set<string>(seedAccounts);

  for (const group of vectorGroups) {
    for (const row of group) {
      const accountName = row.metric.account_name;
      if (accountName) {
        accountSet.add(accountName);
      }
    }
  }

  return [...accountSet].sort((a, b) => a.localeCompare(b));
}

function hasSeriesData(series: ExpediaLineSeries[]) {
  return series.some((item) => item.data.some(([, value]) => value > 0));
}

async function queryInstant(query: string, time: number) {
  return client.query(query, time);
}

async function queryRange(query: string, timeRange: ExpediaResolvedTimeRange) {
  return client.queryRange({
    end: timeRange.end,
    query,
    start: timeRange.start,
    step: timeRange.step,
  });
}

export async function discoverExpediaMetricNames(forceRefresh = false) {
  if (metricNamesCache && !forceRefresh) {
    return metricNamesCache;
  }

  if (metricNamesPromise && !forceRefresh) {
    return metricNamesPromise;
  }

  metricNamesPromise = (async () => {
    let metricNames: string[];

    try {
      metricNames = await client.labelValues('__name__', [EXPEDIA_METRIC_DISCOVERY_MATCHER]);
    } catch {
      const allMetricNames = await client.labelValues('__name__');
      metricNames = allMetricNames.filter((name) => name.startsWith('expedia_'));
    }

    const resolved = resolveExpediaMetricNames(metricNames);
    metricNamesCache = resolved;
    return resolved;
  })();

  try {
    return await metricNamesPromise;
  } finally {
    metricNamesPromise = null;
  }
}

export async function loadExpediaTaapAccountNames(targetScope: ExpediaDashboardTargetScope) {
  const metricNames = await discoverExpediaMetricNames();
  const matchers = buildScopedMetricMatchers(
    [
      metricNames.taskTotal,
      metricNames.operationDurationBucket,
      metricNames.sessionTotal,
      metricNames.accountWorkerCount,
      metricNames.accountInProgressTaskCount,
    ],
    targetScope,
  );

  const values = await client.labelValues('account_name', matchers);
  return [...new Set(values.filter(Boolean))].sort((a, b) => a.localeCompare(b));
}

export async function loadExpediaTaapDashboardData(params: {
  accountNames: string[];
  accounts: string[];
  targetScope: ExpediaDashboardTargetScope;
  timeRange: ExpediaResolvedTimeRange;
}): Promise<ExpediaDashboardData> {
  const metricNames = await discoverExpediaMetricNames();
  const timeline = buildTimelineMillis(
    params.timeRange.start,
    params.timeRange.end,
    params.timeRange.step,
  );

  const pendingCurrentQuery = buildGaugeInstantQuery(
    metricNames.dbPendingTaskCount,
    params.targetScope,
    {
      extraMatchers: [`priority=~"${EXPEDIA_PENDING_PRIORITY_ORDER.join('|')}"`],
      groupBy: ['priority'],
    },
  );

  const workerCurrentQuery = buildGaugeInstantQuery(
    metricNames.accountWorkerCount,
    params.targetScope,
    {
      accounts: params.accounts,
      groupBy: ['account_name'],
    },
  );

  const inProgressCurrentQuery = buildGaugeInstantQuery(
    metricNames.accountInProgressTaskCount,
    params.targetScope,
    {
      accounts: params.accounts,
      groupBy: ['account_name'],
    },
  );

  const resultOverviewQuery = buildIncreaseInstantQuery(
    metricNames.taskTotal,
    params.targetScope,
    {
      accounts: params.accounts,
      groupBy: ['result'],
      window: '1h',
    },
  );

  const pendingTrendQuery = buildGaugeRangeQuery(
    metricNames.dbPendingTaskCount,
    params.targetScope,
    {
      extraMatchers: [`priority=~"${EXPEDIA_PENDING_PRIORITY_ORDER.join('|')}"`],
      groupBy: ['priority'],
    },
  );

  const resultTrendQuery = buildIncreaseRangeQuery(metricNames.taskTotal, params.targetScope, {
    accounts: params.accounts,
    groupBy: ['result'],
    window: '5m',
  });

  const sessionTrendQuery = buildIncreaseRangeQuery(
    metricNames.sessionTotal,
    params.targetScope,
    {
      accounts: params.accounts,
      groupBy: ['event'],
      window: '5m',
    },
  );

  const tableTaskResultQuery = buildIncreaseInstantQuery(
    metricNames.taskTotal,
    params.targetScope,
    {
      accounts: params.accounts,
      groupBy: ['account_name', 'result'],
      window: '15m',
    },
  );

  const tableSessionEventQuery = buildIncreaseInstantQuery(
    metricNames.sessionTotal,
    params.targetScope,
    {
      accounts: params.accounts,
      groupBy: ['account_name', 'event'],
      window: '15m',
    },
  );

  const tableQueryAvgQuery = buildHistogramAverageInstantQuery(
    metricNames,
    params.targetScope,
    { mode: 'eq', value: 'query' },
    params.accounts,
    true,
  );

  const tableQueryP95Query = buildHistogramQuantileInstantQuery(
    metricNames.operationDurationBucket,
    params.targetScope,
    { mode: 'eq', value: 'query' },
    0.95,
    params.accounts,
    true,
  );

  const tablePersistAvgQuery = buildHistogramAverageInstantQuery(
    metricNames,
    params.targetScope,
    { mode: 'eq', value: 'persist' },
    params.accounts,
    true,
  );

  const latencyRequests = EXPEDIA_LATENCY_CHART_META.flatMap((item) => [
    queryRange(
      buildHistogramQuantileRangeQuery(
        metricNames.operationDurationBucket,
        params.targetScope,
        { mode: item.matcherMode, value: item.matcherValue },
        0.5,
        params.accounts,
      ),
      params.timeRange,
    ),
    queryRange(
      buildHistogramQuantileRangeQuery(
        metricNames.operationDurationBucket,
        params.targetScope,
        { mode: item.matcherMode, value: item.matcherValue },
        0.95,
        params.accounts,
      ),
      params.timeRange,
    ),
    queryRange(
      buildHistogramAverageRangeQuery(
        metricNames,
        params.targetScope,
        { mode: item.matcherMode, value: item.matcherValue },
        params.accounts,
      ),
      params.timeRange,
    ),
  ]);

  const [
    pendingCurrentRows,
    workerCurrentRows,
    inProgressCurrentRows,
    resultOverviewRows,
    pendingTrendRows,
    resultTrendRows,
    sessionTrendRows,
    ...restRows
  ] = await Promise.all([
    queryInstant(pendingCurrentQuery, params.timeRange.end),
    queryInstant(workerCurrentQuery, params.timeRange.end),
    queryInstant(inProgressCurrentQuery, params.timeRange.end),
    queryInstant(resultOverviewQuery, params.timeRange.end),
    queryRange(pendingTrendQuery, params.timeRange),
    queryRange(resultTrendQuery, params.timeRange),
    queryRange(sessionTrendQuery, params.timeRange),
    ...latencyRequests,
    queryInstant(tableTaskResultQuery, params.timeRange.end),
    queryInstant(tableSessionEventQuery, params.timeRange.end),
    queryInstant(tableQueryAvgQuery, params.timeRange.end),
    queryInstant(tableQueryP95Query, params.timeRange.end),
    queryInstant(tablePersistAvgQuery, params.timeRange.end),
  ]);

  const latencyRangeResultCount = EXPEDIA_LATENCY_CHART_META.length * 3;
  const latencyRows = restRows.slice(0, latencyRangeResultCount) as PrometheusMatrixResult[][];
  const tableRows = restRows.slice(latencyRangeResultCount) as PrometheusVectorResult[][];

  const [
    tableTaskResultRows = [],
    tableSessionEventRows = [],
    tableQueryAvgRows = [],
    tableQueryP95Rows = [],
    tablePersistAvgRows = [],
  ] = tableRows;

  const pendingCurrentMap = mapVectorByLabel(pendingCurrentRows, 'priority');
  const workerCurrentMap = mapVectorByLabel(workerCurrentRows, 'account_name');
  const inProgressCurrentMap = mapVectorByLabel(inProgressCurrentRows, 'account_name');
  const resultOverviewMap = mapVectorByLabel(resultOverviewRows, 'result');
  const taskResultTableMap = mapNestedVector(tableTaskResultRows, 'account_name', 'result');
  const sessionEventTableMap = mapNestedVector(tableSessionEventRows, 'account_name', 'event');
  const tableQueryAvgMap = mapVectorByLabel(tableQueryAvgRows, 'account_name');
  const tableQueryP95Map = mapVectorByLabel(tableQueryP95Rows, 'account_name');
  const tablePersistAvgMap = mapVectorByLabel(tablePersistAvgRows, 'account_name');

  const accountNames = collectAccountsFromResults(params.accountNames, [
    workerCurrentRows,
    inProgressCurrentRows,
    tableTaskResultRows,
    tableSessionEventRows,
    tableQueryAvgRows,
    tableQueryP95Rows,
    tablePersistAvgRows,
  ]);

  const overviewCards: ExpediaOverviewCard[] = [
    ...EXPEDIA_PENDING_PRIORITY_ORDER.map((priority) => ({
      description: '当前值',
      filter: { kind: 'pendingPriority', value: priority } as const,
      key: `pending:${priority}`,
      label: EXPEDIA_PENDING_PRIORITY_META[priority].label,
      tone:
        priority === 'high'
          ? ('danger' as const)
          : ('warning' as const),
      value: pendingCurrentMap.get(priority) || 0,
    })),
    ...(['workerCount', 'inProgressTaskCount'] as const).map((key) => ({
      description: '当前值',
      filter: { kind: 'accountMetric', value: key } as const,
      key,
      label: EXPEDIA_OVERVIEW_ACCOUNT_METRICS[key].label,
      tone: EXPEDIA_OVERVIEW_ACCOUNT_METRICS[key].tone,
      value:
        key === 'workerCount'
          ? [...workerCurrentMap.values()].reduce((sum, value) => sum + value, 0)
          : [...inProgressCurrentMap.values()].reduce((sum, value) => sum + value, 0),
    })),
    ...EXPEDIA_TASK_RESULT_ORDER.map((result) => ({
      description: 'increase(...[1h])',
      filter: { kind: 'taskResult', value: result } as const,
      key: `result:${result}`,
      label: EXPEDIA_TASK_RESULT_META[result].overviewLabel,
      tone:
        result === 'success'
          ? ('success' as const)
          : result === 'error'
            ? ('danger' as const)
            : ('info' as const),
      value: resultOverviewMap.get(result) || 0,
    })),
  ];

  const pendingTrend = buildSeriesForOrderedLabels(
    pendingTrendRows,
    'priority',
    EXPEDIA_PENDING_PRIORITY_ORDER,
    EXPEDIA_PENDING_PRIORITY_META,
    timeline,
  );

  const resultTrend = buildSeriesForOrderedLabels(
    resultTrendRows,
    'result',
    EXPEDIA_TASK_RESULT_ORDER,
    EXPEDIA_TASK_RESULT_META,
    timeline,
  );

  const sessionTrend = buildSeriesForOrderedLabels(
    sessionTrendRows,
    'event',
    EXPEDIA_SESSION_EVENT_ORDER,
    EXPEDIA_SESSION_EVENT_META,
    timeline,
  );

  const latencyCharts = EXPEDIA_LATENCY_CHART_META.map<ExpediaLatencyChartData>((item, index) => {
    const baseIndex = index * 3;
    const p50Rows = latencyRows[baseIndex] || [];
    const p95Rows = latencyRows[baseIndex + 1] || [];
    const avgRows = latencyRows[baseIndex + 2] || [];

    return {
      key: item.key,
      label: item.label,
      series: [
        buildSingleAggregatedSeries(
          p50Rows,
          timeline,
          `${item.key}:p50`,
          EXPEDIA_LATENCY_SERIES_META.p50.label,
          EXPEDIA_LATENCY_SERIES_META.p50.color,
        ),
        buildSingleAggregatedSeries(
          p95Rows,
          timeline,
          `${item.key}:p95`,
          EXPEDIA_LATENCY_SERIES_META.p95.label,
          EXPEDIA_LATENCY_SERIES_META.p95.color,
        ),
        buildSingleAggregatedSeries(
          avgRows,
          timeline,
          `${item.key}:avg`,
          EXPEDIA_LATENCY_SERIES_META.avg.label,
          EXPEDIA_LATENCY_SERIES_META.avg.color,
        ),
      ],
    };
  });

  const accountRows = accountNames.map<ExpediaAccountTableRow>((accountName) => {
    const resultMap = taskResultTableMap.get(accountName) || new Map<string, number>();
    const sessionMap = sessionEventTableMap.get(accountName) || new Map<string, number>();

    return {
      accountName,
      cacheHitDbCount: sessionMap.get('cache_hit_db') || 0,
      cacheHitFileCount: sessionMap.get('cache_hit_file') || 0,
      cacheHitMemoryCount: sessionMap.get('cache_hit_memory') || 0,
      error15m: resultMap.get('error') || 0,
      inProgressTaskCount: inProgressCurrentMap.get(accountName) || 0,
      loginRefreshCount: sessionMap.get('login_refresh') || 0,
      noPrice15m: resultMap.get('no_price') || 0,
      offline15m: resultMap.get('offline') || 0,
      persistAvgMs: tablePersistAvgMap.get(accountName) || 0,
      queryAvgMs: tableQueryAvgMap.get(accountName) || 0,
      queryP95Ms: tableQueryP95Map.get(accountName) || 0,
      sharedShaRefreshCount: sessionMap.get('shared_sha_refresh') || 0,
      success15m: resultMap.get('success') || 0,
      workerCount: workerCurrentMap.get(accountName) || 0,
    };
  });

  const hasAnyData =
    overviewCards.some((item) => item.value > 0) ||
    hasSeriesData(pendingTrend) ||
    hasSeriesData(resultTrend) ||
    hasSeriesData(sessionTrend) ||
    latencyCharts.some((item) => hasSeriesData(item.series)) ||
    accountRows.length > 0;

  return {
    accountRows,
    hasAnyData,
    latencyCharts,
    metricNames,
    overviewCards,
    pendingTrend,
    resultTrend,
    sessionTrend,
  };
}


