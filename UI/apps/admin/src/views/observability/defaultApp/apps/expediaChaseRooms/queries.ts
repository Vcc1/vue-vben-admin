import type { ExpediaDashboardTargetScope, ExpediaResolvedMetricNames } from './types';

interface BaseQueryOptions {
  accounts?: string[];
  extraMatchers?: string[];
  groupBy?: string[];
}

interface CounterQueryOptions extends BaseQueryOptions {
  window: string;
}

interface HistogramMatcher {
  mode: 'eq' | 'regex';
  value: string;
}

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function escapeRegex(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function buildScopeMatchers(targetScope: ExpediaDashboardTargetScope) {
  const matchers = [
    `exported_instance="${escapeLabelValue(targetScope.exportedInstance)}"`,
    `exported_job="${escapeLabelValue(targetScope.exportedJob)}"`,
  ];

  if (targetScope.otelScopeName) {
    matchers.push(`otel_scope_name="${escapeLabelValue(targetScope.otelScopeName)}"`);
  }

  return matchers;
}

function buildAccountMatcher(accounts: string[] = []) {
  if (!accounts.length) {
    return null;
  }

  return `account_name=~"${accounts.map((item) => escapeRegex(item)).join('|')}"`;
}

function buildSelector(
  metricName: string,
  targetScope: ExpediaDashboardTargetScope,
  accounts: string[] = [],
  extraMatchers: string[] = [],
) {
  const matchers = [...buildScopeMatchers(targetScope)];
  const accountMatcher = buildAccountMatcher(accounts);

  if (accountMatcher) {
    matchers.push(accountMatcher);
  }

  matchers.push(...extraMatchers);

  return `${metricName}{${matchers.join(',')}}`;
}

function buildGroupedExpr(expr: string, groupBy: string[] = []) {
  if (!groupBy.length) {
    return `sum(${expr})`;
  }

  return `sum by (${groupBy.join(', ')}) (${expr})`;
}

function buildHistogramOperationMatcher(matcher: HistogramMatcher) {
  const operator = matcher.mode === 'regex' ? '=~' : '=';
  return `operation${operator}"${escapeLabelValue(matcher.value)}"`;
}

export function buildScopedMetricMatchers(
  metricNames: string[],
  targetScope: ExpediaDashboardTargetScope,
) {
  return metricNames.map((metricName) => buildSelector(metricName, targetScope));
}

export function buildGaugeInstantQuery(
  metricName: string,
  targetScope: ExpediaDashboardTargetScope,
  options: BaseQueryOptions = {},
) {
  const selector = buildSelector(
    metricName,
    targetScope,
    options.accounts,
    options.extraMatchers,
  );
  return buildGroupedExpr(selector, options.groupBy);
}

export function buildGaugeRangeQuery(
  metricName: string,
  targetScope: ExpediaDashboardTargetScope,
  options: BaseQueryOptions = {},
) {
  const selector = buildSelector(
    metricName,
    targetScope,
    options.accounts,
    options.extraMatchers,
  );
  return buildGroupedExpr(selector, options.groupBy);
}

export function buildIncreaseInstantQuery(
  metricName: string,
  targetScope: ExpediaDashboardTargetScope,
  options: CounterQueryOptions,
) {
  const selector = buildSelector(
    metricName,
    targetScope,
    options.accounts,
    options.extraMatchers,
  );
  return buildGroupedExpr(`increase(${selector}[${options.window}])`, options.groupBy);
}

export function buildIncreaseRangeQuery(
  metricName: string,
  targetScope: ExpediaDashboardTargetScope,
  options: CounterQueryOptions,
) {
  const selector = buildSelector(
    metricName,
    targetScope,
    options.accounts,
    options.extraMatchers,
  );
  return buildGroupedExpr(`increase(${selector}[${options.window}])`, options.groupBy);
}

export function buildHistogramQuantileInstantQuery(
  bucketMetricName: string,
  targetScope: ExpediaDashboardTargetScope,
  operationMatcher: HistogramMatcher,
  quantile: number,
  accounts: string[] = [],
  groupByAccount = false,
) {
  const selector = buildSelector(bucketMetricName, targetScope, accounts, [
    buildHistogramOperationMatcher(operationMatcher),
  ]);
  const groupBy = groupByAccount ? 'le, account_name' : 'le';

  return `histogram_quantile(${quantile}, sum by (${groupBy}) (rate(${selector}[5m])))`;
}

export function buildHistogramQuantileRangeQuery(
  bucketMetricName: string,
  targetScope: ExpediaDashboardTargetScope,
  operationMatcher: HistogramMatcher,
  quantile: number,
  accounts: string[] = [],
) {
  const selector = buildSelector(bucketMetricName, targetScope, accounts, [
    buildHistogramOperationMatcher(operationMatcher),
  ]);

  return `histogram_quantile(${quantile}, sum by (le) (rate(${selector}[5m])))`;
}

export function buildHistogramAverageInstantQuery(
  metricNames: Pick<
    ExpediaResolvedMetricNames,
    'operationDurationCount' | 'operationDurationSum'
  >,
  targetScope: ExpediaDashboardTargetScope,
  operationMatcher: HistogramMatcher,
  accounts: string[] = [],
  groupByAccount = false,
) {
  const sumSelector = buildSelector(metricNames.operationDurationSum, targetScope, accounts, [
    buildHistogramOperationMatcher(operationMatcher),
  ]);
  const countSelector = buildSelector(metricNames.operationDurationCount, targetScope, accounts, [
    buildHistogramOperationMatcher(operationMatcher),
  ]);
  const groupBy = groupByAccount ? 'account_name' : '';
  const numerator = groupBy
    ? `sum by (${groupBy}) (rate(${sumSelector}[5m]))`
    : `sum(rate(${sumSelector}[5m]))`;
  const denominator = groupBy
    ? `sum by (${groupBy}) (rate(${countSelector}[5m]))`
    : `sum(rate(${countSelector}[5m]))`;

  return `${numerator} / ${denominator}`;
}

export function buildHistogramAverageRangeQuery(
  metricNames: Pick<
    ExpediaResolvedMetricNames,
    'operationDurationCount' | 'operationDurationSum'
  >,
  targetScope: ExpediaDashboardTargetScope,
  operationMatcher: HistogramMatcher,
  accounts: string[] = [],
) {
  const sumSelector = buildSelector(metricNames.operationDurationSum, targetScope, accounts, [
    buildHistogramOperationMatcher(operationMatcher),
  ]);
  const countSelector = buildSelector(metricNames.operationDurationCount, targetScope, accounts, [
    buildHistogramOperationMatcher(operationMatcher),
  ]);

  return `sum(rate(${sumSelector}[5m])) / sum(rate(${countSelector}[5m]))`;
}
