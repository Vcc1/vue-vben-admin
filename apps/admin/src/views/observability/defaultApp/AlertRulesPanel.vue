<script lang="ts" setup>
import type {
  AlertNotifyLogDto,
  AlertStateDto,
  MetricAlertPreviewDto,
  MetricAlertRuleDto,
  MetricQueryItem,
  ScheduledMetricMessagePreviewDto,
  ScheduledMetricMessageRuleDto,
  TargetAliveAlertRuleDto,
  TargetUrlMonitorRuleDto,
  UpsertMetricAlertRuleRequest,
  UpsertScheduledMetricMessageRuleRequest,
  UpsertTargetAliveAlertRuleRequest,
  UpsertTargetUrlMonitorRuleRequest,
} from '#/api/metricsAndAlerts';
import type { AppViewModel, MonitorTargetConfig } from './types';

import { computed, reactive, ref, watch } from 'vue';

import { ElMessage, ElMessageBox } from 'element-plus';

import {
  MetricsAndAlertsBaseUrl,
  MetricsAndAlertsClient,
} from '#/api/metricsAndAlerts';
import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import { getRuntimeQueryTime } from './appPanelUtils';

const TARGET_TYPE_APPLICATION = 1;
const ALERT_TYPE_ALIVE = 1;
const ALERT_TYPE_URL = 2;
const ALERT_TYPE_METRIC = 3;
const PROMQL_KEYWORDS = new Set([
  'and',
  'bool',
  'by',
  'group_left',
  'group_right',
  'ignoring',
  'offset',
  'on',
  'or',
  'unless',
  'without',
]);

type AlertRuleType = 'alive' | 'metric' | 'url';

interface AlertRuleListItem {
  alertType: number;
  id: number;
  isEnabled: boolean;
  paramsText: string;
  ruleName: string;
  source: MetricAlertRuleDto | TargetAliveAlertRuleDto | TargetUrlMonitorRuleDto;
  statusText: string;
  statusType: '' | 'danger' | 'info' | 'success' | 'warning';
  type: AlertRuleType;
  typeText: string;
  updatedAt?: null | string;
}

const props = defineProps<{
  app: AppViewModel | null;
  refreshKey?: number;
  targetConfig?: MonitorTargetConfig | null;
}>();

const client = new MetricsAndAlertsClient();
const prometheusClient = new PrometheusClient();
const metricsAndAlertsConfigured = Boolean(MetricsAndAlertsBaseUrl);

const activeTab = ref<'alerts' | 'reports' | 'messages'>('alerts');
const loading = ref(false);
const notifyLogsLoading = ref(false);
const savingAlert = ref(false);
const savingReport = ref(false);
const previewingAlert = ref(false);
const previewingReport = ref(false);
const alertDrawerVisible = ref(false);
const reportDrawerVisible = ref(false);
const alertPreview = ref<MetricAlertPreviewDto | null>(null);
const reportPreview = ref<ScheduledMetricMessagePreviewDto | null>(null);
const promqlEditorVisible = ref(false);
const promqlEditorTarget = ref<'alert' | 'report'>('alert');
const promqlEditorIndex = ref(-1);
const promqlEditorValue = ref('');
const promqlEditorAutoScope = ref(true);
const promqlEditorValidating = ref(false);

const aliveRules = ref<TargetAliveAlertRuleDto[]>([]);
const urlRules = ref<TargetUrlMonitorRuleDto[]>([]);
const metricRules = ref<MetricAlertRuleDto[]>([]);
const reportRules = ref<ScheduledMetricMessageRuleDto[]>([]);
const alertStates = ref<AlertStateDto[]>([]);
const notifyLogs = ref<AlertNotifyLogDto[]>([]);
const notifyLogsPage = ref(1);
const notifyLogsPageSize = ref(20);
const notifyLogsTotal = ref(0);
const notifyLogFilters = reactive({
  ruleValue: '',
  timeRange: [] as [] | [Date, Date],
});

const alertForm = reactive({
  aggregateInstances: false,
  checkIntervalSeconds: 30,
  durationMinutes: 0,
  expectedStatusCodesText: '200',
  formula: '',
  httpMethod: 'GET',
  id: 0,
  isEnabled: true,
  messageTemplate:
    '应用：{targetName}\n规则：{ruleName}\n实例：{instance}\n公式：{formula}\n当前值：\n{currentValues}\n异常实例：{abnormalInstances}',
  minAliveCount: 1,
  notifyMinuteQueueText: '0,5,10,120',
  notifyUsersText: '',
  offlineSeconds: 60,
  queryItems: [] as MetricQueryItem[],
  recoverTemplate:
    '应用：{targetName}\n规则：{ruleName}\n恢复时间：{recoveredAt}\n当前值：\n{currentValues}',
  ruleName: '',
  timeoutSeconds: 10,
  type: 'alive' as AlertRuleType,
  url: '',
  urlName: '',
});

const reportForm = reactive({
  aggregateInstances: false,
  cron: '0 9 * * *',
  id: 0,
  isEnabled: true,
  messageTemplate: '应用：{targetName}\n报表：{ruleName}\n时间：{now}\n当前值：\n{currentValues}',
  notifyUsersText: '',
  queryItems: [] as MetricQueryItem[],
  ruleName: '',
  timeZoneId: 'Asia/Shanghai',
});

const targetKey = computed(
  () => props.targetConfig?.targetKey || props.app?.appName || '',
);

const targetReady = computed(
  () => metricsAndAlertsConfigured && Boolean(props.targetConfig?.targetKey),
);

const normalizedAlertRules = computed<AlertRuleListItem[]>(() => {
  const rows: AlertRuleListItem[] = [];

  for (const rule of aliveRules.value) {
    rows.push({
      alertType: ALERT_TYPE_ALIVE,
      id: rule.id,
      isEnabled: rule.isEnabled,
      paramsText: `最小在线 ${rule.minAliveCount}，离线 ${rule.offlineSeconds}s，持续 ${rule.durationMinutes}m`,
      ruleName: rule.ruleName || '在线实例数',
      source: rule,
      ...buildRuleStatus(ALERT_TYPE_ALIVE, rule.id, rule.isEnabled),
      type: 'alive',
      typeText: '在线实例数',
      updatedAt: rule.updatedAt,
    });
  }

  for (const rule of urlRules.value) {
    rows.push({
      alertType: ALERT_TYPE_URL,
      id: rule.id,
      isEnabled: rule.isEnabled,
      paramsText: `${rule.httpMethod} ${rule.url}，${rule.timeoutSeconds}s，状态 ${rule.expectedStatusCodes.join(',') || '200'}`,
      ruleName: rule.ruleName || rule.urlName || rule.url,
      source: rule,
      ...buildRuleStatus(ALERT_TYPE_URL, rule.id, rule.isEnabled),
      type: 'url',
      typeText: 'URL巡检',
      updatedAt: rule.updatedAt,
    });
  }

  for (const rule of metricRules.value) {
    rows.push({
      alertType: ALERT_TYPE_METRIC,
      id: rule.id,
      isEnabled: rule.isEnabled,
      paramsText: `${rule.aggregateInstances ? '聚合计算' : '独立计算'}，${rule.formula}，${rule.queryItems.length} 个查询变量，持续 ${rule.durationMinutes}m`,
      ruleName: rule.ruleName || rule.formula,
      source: rule,
      ...buildRuleStatus(ALERT_TYPE_METRIC, rule.id, rule.isEnabled),
      type: 'metric',
      typeText: '指标公式',
      updatedAt: rule.updatedAt,
    });
  }

  return rows.sort((left, right) => {
    const leftTime = Date.parse(left.updatedAt || '') || 0;
    const rightTime = Date.parse(right.updatedAt || '') || 0;
    return rightTime - leftTime || left.typeText.localeCompare(right.typeText) || left.id - right.id;
  });
});

const notifyLogRuleOptions = computed(() =>
  normalizedAlertRules.value.map((rule) => ({
    label: `${rule.typeText} / ${rule.ruleName}`,
    value: `${rule.alertType}:${rule.id}`,
  })),
);

function buildRuleStatus(alertType: number, ruleId: number, isEnabled: boolean) {
  if (!isEnabled) {
    return {
      statusText: '已禁用',
      statusType: 'info' as const,
    };
  }

  const activeCount = alertStates.value.filter(
    (state) => state.alertType === alertType && state.ruleId === ruleId && state.isActive,
  ).length;

  if (activeCount > 0) {
    return {
      statusText: activeCount > 1 ? `告警中 ${activeCount}` : '告警中',
      statusType: 'danger' as const,
    };
  }

  return {
    statusText: '正常',
    statusType: 'success' as const,
  };
}

function formatDateTime(value?: null | string) {
  if (!value) {
    return '--';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function formatNotifyType(value: number) {
  return value === 2 ? '恢复' : '告警';
}

function formatAlertType(value: number) {
  if (value === ALERT_TYPE_ALIVE) {
    return '在线实例数';
  }
  if (value === ALERT_TYPE_URL) {
    return 'URL巡检';
  }
  if (value === ALERT_TYPE_METRIC) {
    return '指标公式';
  }
  return `类型${value}`;
}

function getNotifyLogFilterTimes() {
  const [start, end] = notifyLogFilters.timeRange;
  return {
    endTime: end ? end.toISOString() : undefined,
    startTime: start ? start.toISOString() : undefined,
  };
}

function getNotifyLogRuleFilter() {
  if (!notifyLogFilters.ruleValue) {
    return {
      alertType: undefined as number | undefined,
      ruleId: undefined as number | undefined,
    };
  }

  const [alertTypeText, ruleIdText] = notifyLogFilters.ruleValue.split(':', 2);
  const alertType = Number(alertTypeText);
  const ruleId = Number(ruleIdText);
  return {
    alertType: Number.isFinite(alertType) ? alertType : undefined,
    ruleId: Number.isFinite(ruleId) ? ruleId : undefined,
  };
}

function normalizeNotifyLogsResult(
  result: unknown,
  page = notifyLogsPage.value,
  pageSize = notifyLogsPageSize.value,
) {
  if (Array.isArray(result)) {
    const normalizedPage = Math.max(1, page);
    const normalizedPageSize = Math.max(1, pageSize);
    const start = (normalizedPage - 1) * normalizedPageSize;
    return {
      items: (result as AlertNotifyLogDto[]).slice(start, start + normalizedPageSize),
      page: normalizedPage,
      pageSize: normalizedPageSize,
      totalCount: result.length,
    };
  }

  const paged = result as {
    items?: AlertNotifyLogDto[];
    page?: number;
    pageSize?: number;
    totalCount?: number;
  } | null;

  return {
    items: paged?.items ?? [],
    page: paged?.page ?? page,
    pageSize: paged?.pageSize ?? pageSize,
    totalCount: paged?.totalCount ?? (paged?.items?.length ?? 0),
  };
}

function splitTextValues(value: string) {
  return value
    .split(/[\n,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function splitIntValues(value: string) {
  return splitTextValues(value)
    .map((item) => Number(item))
    .filter((item) => Number.isFinite(item))
    .map((item) => Math.trunc(item));
}

function normalizeQueryItems(items: MetricQueryItem[]) {
  return items
    .map((item) => ({
      aggregateMode: item.aggregateMode || 'avg',
      name: item.name.trim(),
      promql: item.promql.trim(),
      valueMode: item.valueMode || 'instant',
    }))
    .filter((item) => item.name && item.promql);
}

function ensureReady() {
  if (!metricsAndAlertsConfigured) {
    ElMessage.warning('MetricsAndAlerts 未配置。');
    return false;
  }

  if (!targetReady.value) {
    ElMessage.warning('请先保存应用基础配置后再管理规则。');
    return false;
  }

  return true;
}

async function loadRules() {
  if (!metricsAndAlertsConfigured || !props.targetConfig?.targetKey) {
    aliveRules.value = [];
    urlRules.value = [];
    metricRules.value = [];
    reportRules.value = [];
    alertStates.value = [];
    notifyLogs.value = [];
    notifyLogsTotal.value = 0;
    return;
  }

  loading.value = true;
  try {
    const key = props.targetConfig.targetKey;
    const ruleFilter = getNotifyLogRuleFilter();
    const [alive, urls, metrics, reports, states, logs] = await Promise.all([
      client.listTargetAliveAlertRules(key, TARGET_TYPE_APPLICATION),
      client.listTargetUrlMonitorRules(key, TARGET_TYPE_APPLICATION),
      client.listMetricAlertRules({
        includeDisabled: true,
        targetKey: key,
        targetType: TARGET_TYPE_APPLICATION,
      }),
      client.listScheduledMessageRules({
        includeDisabled: true,
        targetKey: key,
        targetType: TARGET_TYPE_APPLICATION,
      }),
      client.listAlertStates({
        isActive: true,
        targetKey: key,
        targetType: TARGET_TYPE_APPLICATION,
      }),
      client.listAlertNotifyLogs({
        ...getNotifyLogFilterTimes(),
        alertType: ruleFilter.alertType,
        page: notifyLogsPage.value,
        pageSize: notifyLogsPageSize.value,
        ruleId: ruleFilter.ruleId,
        targetKey: key,
        targetType: TARGET_TYPE_APPLICATION,
      }),
    ]);
    const normalizedLogs = normalizeNotifyLogsResult(
      logs,
      notifyLogsPage.value,
      notifyLogsPageSize.value,
    );

    aliveRules.value = alive;
    urlRules.value = urls;
    metricRules.value = metrics;
    reportRules.value = reports;
    alertStates.value = states;
    notifyLogs.value = normalizedLogs.items;
    notifyLogsPage.value = normalizedLogs.page;
    notifyLogsPageSize.value = normalizedLogs.pageSize;
    notifyLogsTotal.value = normalizedLogs.totalCount;
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '加载告警规则失败。');
  } finally {
    loading.value = false;
  }
}

async function loadAlertNotifyLogs(page = notifyLogsPage.value) {
  if (!metricsAndAlertsConfigured || !props.targetConfig?.targetKey) {
    notifyLogs.value = [];
    notifyLogsTotal.value = 0;
    return;
  }

  notifyLogsLoading.value = true;
  try {
    const { startTime, endTime } = getNotifyLogFilterTimes();
    const ruleFilter = getNotifyLogRuleFilter();
    const requestedPageSize = notifyLogsPageSize.value;
    const result = normalizeNotifyLogsResult(
      await client.listAlertNotifyLogs({
        alertType: ruleFilter.alertType,
        endTime,
        page,
        pageSize: requestedPageSize,
        ruleId: ruleFilter.ruleId,
        startTime,
        targetKey: props.targetConfig.targetKey,
        targetType: TARGET_TYPE_APPLICATION,
      }),
      page,
      requestedPageSize,
    );
    notifyLogsPage.value = result.page;
    notifyLogs.value = result.items;
    notifyLogsPageSize.value = result.pageSize;
    notifyLogsTotal.value = result.totalCount;
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '加载告警消息失败。');
  } finally {
    notifyLogsLoading.value = false;
  }
}

async function applyNotifyLogFilters() {
  notifyLogsPage.value = 1;
  await loadAlertNotifyLogs(1);
}

async function resetNotifyLogFilters() {
  notifyLogFilters.ruleValue = '';
  notifyLogFilters.timeRange = [];
  notifyLogsPage.value = 1;
  await loadAlertNotifyLogs(1);
}

async function handleNotifyLogsPageChange(page: number) {
  notifyLogsPage.value = page;
  await loadAlertNotifyLogs(page);
}

async function handleNotifyLogsPageSizeChange(pageSize: number) {
  notifyLogsPageSize.value = pageSize;
  notifyLogsPage.value = 1;
  await loadAlertNotifyLogs(1);
}

function resetAlertForm(type: AlertRuleType = 'alive') {
  alertForm.type = type;
  alertForm.id = 0;
  alertForm.ruleName = type === 'alive' ? '在线实例数' : type === 'url' ? 'URL巡检' : '指标公式';
  alertForm.isEnabled = true;
  alertForm.minAliveCount = 1;
  alertForm.offlineSeconds = 60;
  alertForm.durationMinutes = 0;
  alertForm.notifyMinuteQueueText = '0,5,10,120';
  alertForm.notifyUsersText = props.targetConfig?.notifyUsers.join(', ') || '';
  alertForm.aggregateInstances = false;
  alertForm.urlName = '';
  alertForm.url = '';
  alertForm.httpMethod = 'GET';
  alertForm.timeoutSeconds = 10;
  alertForm.checkIntervalSeconds = 30;
  alertForm.expectedStatusCodesText = '200';
  alertForm.formula = '';
  alertForm.queryItems = [{ aggregateMode: 'avg', name: 'value', promql: '', valueMode: 'instant' }];
  alertForm.messageTemplate =
    '应用：{targetName}\n规则：{ruleName}\n实例：{instance}\n公式：{formula}\n当前值：\n{currentValues}\n异常实例：{abnormalInstances}';
  alertForm.recoverTemplate =
    '应用：{targetName}\n规则：{ruleName}\n恢复时间：{recoveredAt}\n当前值：\n{currentValues}';
  alertPreview.value = null;
}

function openCreateAlert(type: AlertRuleType = 'alive') {
  if (!ensureReady()) {
    return;
  }
  resetAlertForm(type);
  alertDrawerVisible.value = true;
}

function handleCreateAlertCommand(command: string | number | object) {
  if (command === 'alive' || command === 'url' || command === 'metric') {
    openCreateAlert(command);
  }
}

function openEditAlert(row: unknown) {
  const item = row as AlertRuleListItem;
  if (!ensureReady()) {
    return;
  }

  resetAlertForm(item.type);
  alertForm.id = item.id;
  alertForm.type = item.type;
  alertForm.ruleName = item.ruleName;
  alertForm.isEnabled = item.isEnabled;

  if (item.type === 'alive') {
    const rule = item.source as TargetAliveAlertRuleDto;
    alertForm.minAliveCount = rule.minAliveCount;
    alertForm.offlineSeconds = rule.offlineSeconds;
    alertForm.durationMinutes = rule.durationMinutes;
    alertForm.notifyMinuteQueueText = rule.notifyMinuteQueue.join(', ');
    alertForm.notifyUsersText = rule.notifyUsers.join(', ');
  } else if (item.type === 'url') {
    const rule = item.source as TargetUrlMonitorRuleDto;
    alertForm.ruleName = rule.ruleName || rule.urlName || rule.url;
    alertForm.urlName = rule.urlName || '';
    alertForm.url = rule.url;
    alertForm.httpMethod = rule.httpMethod || 'GET';
    alertForm.timeoutSeconds = rule.timeoutSeconds;
    alertForm.checkIntervalSeconds = rule.checkIntervalSeconds;
    alertForm.expectedStatusCodesText = rule.expectedStatusCodes.join(', ');
    alertForm.durationMinutes = rule.durationMinutes;
    alertForm.notifyMinuteQueueText = rule.notifyMinuteQueue.join(', ');
    alertForm.notifyUsersText = rule.notifyUsers.join(', ');
  } else {
    const rule = item.source as MetricAlertRuleDto;
    alertForm.ruleName = rule.ruleName;
    alertForm.aggregateInstances = rule.aggregateInstances;
    alertForm.formula = rule.formula;
    alertForm.queryItems = rule.queryItems.map((item) => ({ ...item }));
    alertForm.durationMinutes = rule.durationMinutes;
    alertForm.notifyMinuteQueueText = rule.notifyMinuteQueue.join(', ');
    alertForm.notifyUsersText = rule.notifyUsers.join(', ');
    alertForm.messageTemplate = rule.messageTemplate || alertForm.messageTemplate;
    alertForm.recoverTemplate = rule.recoverTemplate || alertForm.recoverTemplate;
  }

  alertPreview.value = null;
  alertDrawerVisible.value = true;
}

function buildAlivePayload(): UpsertTargetAliveAlertRuleRequest {
  return {
    durationMinutes: Math.max(0, alertForm.durationMinutes),
    id: alertForm.id,
    isEnabled: alertForm.isEnabled,
    minAliveCount: Math.max(1, alertForm.minAliveCount),
    notifyMinuteQueue: splitIntValues(alertForm.notifyMinuteQueueText),
    notifyUsers: splitTextValues(alertForm.notifyUsersText),
    offlineSeconds: Math.max(1, alertForm.offlineSeconds),
    ruleName: alertForm.ruleName.trim() || null,
    targetKey: targetKey.value,
    targetType: TARGET_TYPE_APPLICATION,
  };
}

function buildUrlPayload(): UpsertTargetUrlMonitorRuleRequest {
  return {
    checkIntervalSeconds: Math.max(1, alertForm.checkIntervalSeconds),
    durationMinutes: Math.max(0, alertForm.durationMinutes),
    expectedStatusCodes: splitIntValues(alertForm.expectedStatusCodesText),
    httpMethod: alertForm.httpMethod || 'GET',
    id: alertForm.id,
    isEnabled: alertForm.isEnabled,
    notifyMinuteQueue: splitIntValues(alertForm.notifyMinuteQueueText),
    notifyUsers: splitTextValues(alertForm.notifyUsersText),
    ruleName: alertForm.ruleName.trim() || null,
    targetKey: targetKey.value,
    targetType: TARGET_TYPE_APPLICATION,
    timeoutSeconds: Math.max(1, alertForm.timeoutSeconds),
    url: alertForm.url.trim(),
    urlName: alertForm.urlName.trim() || null,
  };
}

function buildMetricPayload(): UpsertMetricAlertRuleRequest {
  return {
    aggregateInstances: alertForm.aggregateInstances,
    durationMinutes: Math.max(0, alertForm.durationMinutes),
    formula: alertForm.formula.trim(),
    id: alertForm.id,
    isEnabled: alertForm.isEnabled,
    messageTemplate: alertForm.messageTemplate.trim() || null,
    notifyMinuteQueue: splitIntValues(alertForm.notifyMinuteQueueText),
    notifyUsers: splitTextValues(alertForm.notifyUsersText),
    queryItems: normalizeQueryItems(alertForm.queryItems),
    recoverTemplate: alertForm.recoverTemplate.trim() || null,
    ruleName: alertForm.ruleName.trim() || alertForm.formula.trim(),
    targetKey: targetKey.value,
    targetType: TARGET_TYPE_APPLICATION,
  };
}

function validateAlertForm() {
  if (!alertForm.ruleName.trim()) {
    ElMessage.warning('请填写规则名称。');
    return false;
  }

  if (alertForm.type === 'url' && !alertForm.url.trim()) {
    ElMessage.warning('请填写 URL。');
    return false;
  }

  if (alertForm.type === 'metric') {
    if (normalizeQueryItems(alertForm.queryItems).length === 0) {
      ElMessage.warning('请至少填写一个查询变量。');
      return false;
    }
    if (!alertForm.formula.trim()) {
      ElMessage.warning('请填写公式。');
      return false;
    }
  }

  return true;
}

async function submitAlertForm() {
  if (!ensureReady() || !validateAlertForm()) {
    return;
  }

  savingAlert.value = true;
  try {
    if (alertForm.type === 'alive') {
      await client.upsertTargetAliveAlertRule(buildAlivePayload());
    } else if (alertForm.type === 'url') {
      await client.upsertTargetUrlMonitorRule(buildUrlPayload());
    } else {
      await client.upsertMetricAlertRule(buildMetricPayload());
    }

    alertDrawerVisible.value = false;
    await loadRules();
    ElMessage.success('规则已保存。');
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '保存规则失败。');
  } finally {
    savingAlert.value = false;
  }
}

async function deleteAlertRule(row: unknown) {
  const item = row as AlertRuleListItem;
  if (!ensureReady()) {
    return;
  }

  try {
    await ElMessageBox.confirm(`确认禁用规则“${item.ruleName}”？`, '禁用规则', {
      confirmButtonText: '禁用',
      cancelButtonText: '取消',
      type: 'warning',
    });

    if (item.type === 'alive') {
      await client.deleteTargetAliveAlertRule(item.id);
    } else if (item.type === 'url') {
      await client.deleteTargetUrlMonitorRule(item.id);
    } else {
      await client.deleteMetricAlertRule(item.id);
    }

    await loadRules();
    ElMessage.success('规则已禁用。');
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error instanceof Error ? error.message : '禁用规则失败。');
    }
  }
}

function addMetricQueryItem(target: 'alert' | 'report') {
  const rows = target === 'alert' ? alertForm.queryItems : reportForm.queryItems;
  rows.push({ aggregateMode: 'avg', name: '', promql: '', valueMode: 'instant' });
}

function removeMetricQueryItem(target: 'alert' | 'report', index: number) {
  const rows = target === 'alert' ? alertForm.queryItems : reportForm.queryItems;
  rows.splice(index, 1);
}

function openPromqlEditor(target: 'alert' | 'report', index: number) {
  const rows = target === 'alert' ? alertForm.queryItems : reportForm.queryItems;
  const row = rows[index];
  if (!row) {
    return;
  }

  promqlEditorTarget.value = target;
  promqlEditorIndex.value = index;
  promqlEditorValue.value = row.promql || '';
  promqlEditorAutoScope.value = true;
  promqlEditorVisible.value = true;
}

function escapePrometheusLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function escapePrometheusRegexValue(value: string) {
  return value.replace(/[|\\{}()[\]^$+*?.-]/g, '\\$&');
}

function buildScopeMatcher(labelName: string, values: string[]) {
  const uniqueValues = [...new Set(values.map((item) => item.trim()).filter(Boolean))];
  if (uniqueValues.length === 0) {
    return null;
  }
  if (uniqueValues.length === 1) {
    return `${labelName}="${escapePrometheusLabelValue(uniqueValues[0]!)}"`;
  }
  return `${labelName}=~"${uniqueValues.map(escapePrometheusRegexValue).join('|')}"`;
}

function queryHasExplicitScope(promql: string) {
  return (
    promql.includes('exported_job=') ||
    promql.includes('exported_job=~') ||
    promql.includes('exported_instance=') ||
    promql.includes('exported_instance=~') ||
    promql.includes('job=') ||
    promql.includes('job=~') ||
    promql.includes('instance=') ||
    promql.includes('instance=~')
  );
}

function isIdentifierStart(char: string) {
  return /[A-Za-z_:]/.test(char);
}

function isIdentifierChar(char: string) {
  return /[A-Za-z0-9_:]/.test(char);
}

function findClosingBrace(source: string, start: number) {
  let inString = false;
  for (let index = start; index < source.length; index += 1) {
    const char = source[index] || '';
    const previous = index > 0 ? source[index - 1] || '' : '';
    if (char === '"' && previous !== '\\') {
      inString = !inString;
      continue;
    }
    if (!inString && char === '}') {
      return index;
    }
  }
  return -1;
}

function selectorAlreadyScoped(selector: string, labelName: string) {
  const alternateLabel = labelName === 'exported_job' ? 'job' : 'instance';
  return (
    selector.includes(`${labelName}=`) ||
    selector.includes(`${labelName}=~`) ||
    selector.includes(`${alternateLabel}=`) ||
    selector.includes(`${alternateLabel}=~`)
  );
}

function rewritePromqlWithMatcher(promql: string, matcher: string) {
  let output = '';
  let index = 0;
  let inString = false;

  while (index < promql.length) {
    const char = promql[index] || '';
    const previous = index > 0 ? promql[index - 1] || '' : '';
    if (char === '"' && previous !== '\\') {
      inString = !inString;
      output += char;
      index += 1;
      continue;
    }

    if (inString || !isIdentifierStart(char)) {
      output += char;
      index += 1;
      continue;
    }

    const start = index;
    index += 1;
    while (index < promql.length && isIdentifierChar(promql[index] || '')) {
      index += 1;
    }

    const identifier = promql.slice(start, index);
    let cursor = index;
    while (cursor < promql.length && /\s/.test(promql[cursor] || '')) {
      cursor += 1;
    }

    const next = promql[cursor] || '';
    if (PROMQL_KEYWORDS.has(identifier.toLowerCase()) || next === '(') {
      output += promql.slice(start, index);
      continue;
    }

    if (next === '{') {
      const closingBrace = findClosingBrace(promql, cursor + 1);
      if (closingBrace < 0) {
        output += promql.slice(start, index);
        continue;
      }

      const beforeSelector = promql.slice(start, cursor);
      const selectorBody = promql.slice(cursor + 1, closingBrace);
      const nextSelector = selectorAlreadyScoped(selectorBody, matcher.startsWith('exported_job') ? 'exported_job' : 'exported_instance')
        ? selectorBody
        : selectorBody.trim()
          ? `${selectorBody}, ${matcher}`
          : matcher;
      output += `${beforeSelector}{${nextSelector}}`;
      index = closingBrace + 1;
      continue;
    }

    if (next === '[' || next === '@' || next === '' || /[\s),+\-/*%^<>=!]/.test(next)) {
      output += `${promql.slice(start, index)}{${matcher}}`;
      continue;
    }

    output += promql.slice(start, index);
  }

  return output;
}

function getScopedPromql(promql: string) {
  const trimmed = promql.trim();
  if (!promqlEditorAutoScope.value || !props.app || queryHasExplicitScope(trimmed)) {
    return trimmed;
  }

  const matcher = buildScopeMatcher('exported_job', props.app.jobs);

  return matcher ? rewritePromqlWithMatcher(trimmed, matcher) : trimmed;
}

async function applyPromqlEditor() {
  const query = getScopedPromql(promqlEditorValue.value);
  if (!query) {
    ElMessage.warning('PromQL 不能为空');
    return;
  }

  promqlEditorValidating.value = true;
  try {
    const queryTime = getRuntimeQueryTime(props.app);
    const result = await prometheusClient.query(query, queryTime ?? undefined);
  const rows = promqlEditorTarget.value === 'alert' ? alertForm.queryItems : reportForm.queryItems;
  const row = rows[promqlEditorIndex.value];
  if (row) {
      row.promql = query;
    }

    promqlEditorVisible.value = false;
    ElMessage.success(
      result.length > 0
        ? `PromQL 验证通过，命中 ${result.length} 条结果`
        : 'PromQL 语法验证通过，当前时间点没有结果',
    );
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : 'PromQL 验证失败');
  }
  finally {
    promqlEditorValidating.value = false;
  }
}

async function previewMetricAlert() {
  if (!ensureReady() || !validateAlertForm()) {
    return;
  }

  previewingAlert.value = true;
  try {
    alertPreview.value = await client.previewMetricAlertRule(buildMetricPayload());
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '查询测试失败。');
  } finally {
    previewingAlert.value = false;
  }
}

async function testSendMetricAlert() {
  if (!ensureReady() || !validateAlertForm()) {
    return;
  }

  previewingAlert.value = true;
  try {
    const result = await client.testSendMetricAlertRule(buildMetricPayload());
    if (result.isSuccess) {
      ElMessage.success('测试消息已发送。');
    } else {
      ElMessage.error(result.errorMessage || '测试消息发送失败。');
    }
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '测试消息发送失败。');
  } finally {
    previewingAlert.value = false;
  }
}

function resetReportForm() {
  reportForm.id = 0;
  reportForm.ruleName = '定时报表';
  reportForm.aggregateInstances = false;
  reportForm.queryItems = [{ aggregateMode: 'avg', name: 'value', promql: '', valueMode: 'instant' }];
  reportForm.messageTemplate = '应用：{targetName}\n报表：{ruleName}\n时间：{now}\n当前值：\n{currentValues}';
  reportForm.cron = '0 9 * * *';
  reportForm.timeZoneId = 'Asia/Shanghai';
  reportForm.notifyUsersText = props.targetConfig?.notifyUsers.join(', ') || '';
  reportForm.isEnabled = true;
  reportPreview.value = null;
}

function openCreateReport() {
  if (!ensureReady()) {
    return;
  }
  resetReportForm();
  reportDrawerVisible.value = true;
}

function openEditReport(row: unknown) {
  const rule = row as ScheduledMetricMessageRuleDto;
  if (!ensureReady()) {
    return;
  }

  resetReportForm();
  reportForm.id = rule.id;
  reportForm.ruleName = rule.ruleName;
  reportForm.aggregateInstances = rule.aggregateInstances;
  reportForm.queryItems = rule.queryItems.map((item) => ({ ...item }));
  reportForm.messageTemplate = rule.messageTemplate;
  reportForm.cron = rule.cron;
  reportForm.timeZoneId = rule.timeZoneId || 'Asia/Shanghai';
  reportForm.notifyUsersText = rule.notifyUsers.join(', ');
  reportForm.isEnabled = rule.isEnabled;
  reportPreview.value = null;
  reportDrawerVisible.value = true;
}

function buildReportPayload(): UpsertScheduledMetricMessageRuleRequest {
  return {
    aggregateInstances: reportForm.aggregateInstances,
    cron: reportForm.cron.trim(),
    id: reportForm.id,
    isEnabled: reportForm.isEnabled,
    messageTemplate: reportForm.messageTemplate.trim(),
    notifyUsers: splitTextValues(reportForm.notifyUsersText),
    queryItems: normalizeQueryItems(reportForm.queryItems),
    ruleName: reportForm.ruleName.trim(),
    targetKey: targetKey.value,
    targetType: TARGET_TYPE_APPLICATION,
    timeZoneId: reportForm.timeZoneId.trim() || 'Asia/Shanghai',
  };
}

function validateReportForm() {
  if (!reportForm.ruleName.trim()) {
    ElMessage.warning('请填写报表名称。');
    return false;
  }
  if (!reportForm.cron.trim()) {
    ElMessage.warning('请填写 Cron。');
    return false;
  }
  if (normalizeQueryItems(reportForm.queryItems).length === 0) {
    ElMessage.warning('请至少填写一个查询变量。');
    return false;
  }
  if (!reportForm.messageTemplate.trim()) {
    ElMessage.warning('请填写消息模板。');
    return false;
  }
  return true;
}

async function submitReportForm() {
  if (!ensureReady() || !validateReportForm()) {
    return;
  }

  savingReport.value = true;
  try {
    await client.upsertScheduledMessageRule(buildReportPayload());
    reportDrawerVisible.value = false;
    await loadRules();
    ElMessage.success('定时报表已保存。');
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '保存定时报表失败。');
  } finally {
    savingReport.value = false;
  }
}

async function deleteReportRule(row: unknown) {
  const rule = row as ScheduledMetricMessageRuleDto;
  if (!ensureReady()) {
    return;
  }

  try {
    await ElMessageBox.confirm(`确认禁用定时报表“${rule.ruleName}”？`, '禁用定时报表', {
      confirmButtonText: '禁用',
      cancelButtonText: '取消',
      type: 'warning',
    });
    await client.deleteScheduledMessageRule(rule.id);
    await loadRules();
    ElMessage.success('定时报表已禁用。');
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error instanceof Error ? error.message : '禁用定时报表失败。');
    }
  }
}

async function previewReport() {
  if (!ensureReady() || !validateReportForm()) {
    return;
  }

  previewingReport.value = true;
  try {
    reportPreview.value = await client.previewScheduledMessageRule(buildReportPayload());
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '模板预览失败。');
  } finally {
    previewingReport.value = false;
  }
}

async function testSendReport() {
  if (!ensureReady() || !validateReportForm()) {
    return;
  }

  previewingReport.value = true;
  try {
    const result = await client.testSendScheduledMessageRule(buildReportPayload());
    if (result.isSuccess) {
      ElMessage.success('测试消息已发送。');
    } else {
      ElMessage.error(result.errorMessage || '测试消息发送失败。');
    }
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '测试消息发送失败。');
  } finally {
    previewingReport.value = false;
  }
}

watch(
  () => [targetKey.value, props.targetConfig?.targetKey, props.refreshKey],
  () => {
    notifyLogsPage.value = 1;
    void loadRules();
  },
  { immediate: true },
);

watch(activeTab, (value) => {
  if (value === 'messages') {
    void loadAlertNotifyLogs();
  }
});
</script>

<template>
  <div class="alert-rules-panel">
    <el-alert
      v-if="!metricsAndAlertsConfigured"
      :closable="false"
      show-icon
      title="MetricsAndAlerts 未配置"
      type="warning"
    />
    <el-alert
      v-else-if="!targetReady"
      :closable="false"
      show-icon
      title="请先在应用编辑中保存基础配置"
      type="info"
    />

    <el-tabs v-model="activeTab" class="rules-tabs">
      <el-tab-pane label="告警规则" name="alerts">
        <div class="toolbar">
          <div class="toolbar-title">应用：{{ targetKey || '--' }}</div>
          <div class="toolbar-actions">
            <el-button :loading="loading" plain size="small" @click="loadRules">刷新</el-button>
            <el-dropdown trigger="click" @command="handleCreateAlertCommand">
              <el-button :disabled="!targetReady" size="small" type="primary">
                新增告警规则
              </el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="alive">在线实例数</el-dropdown-item>
                  <el-dropdown-item command="url">URL巡检</el-dropdown-item>
                  <el-dropdown-item command="metric">指标公式</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
        </div>

        <el-table v-loading="loading" :data="normalizedAlertRules" border size="small">
          <el-table-column label="类型" min-width="110">
            <template #default="{ row }">
              <el-tag size="small">{{ row.typeText }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="名称" min-width="180" prop="ruleName" show-overflow-tooltip />
          <el-table-column label="启用" width="78">
            <template #default="{ row }">
              <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
                {{ row.isEnabled ? '启用' : '禁用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="核心参数" min-width="280" prop="paramsText" show-overflow-tooltip />
          <el-table-column label="当前状态" width="110">
            <template #default="{ row }">
              <el-tag :type="row.statusType" size="small">{{ row.statusText }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="更新时间" min-width="160">
            <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
          </el-table-column>
          <el-table-column fixed="right" label="操作" width="150">
            <template #default="{ row }">
              <el-button link size="small" type="primary" @click="openEditAlert(row)">编辑</el-button>
              <el-button link size="small" type="danger" @click="deleteAlertRule(row)">禁用</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="定时报表" name="reports">
        <div class="toolbar">
          <div class="toolbar-title">应用：{{ targetKey || '--' }}</div>
          <div class="toolbar-actions">
            <el-button :loading="loading" plain size="small" @click="loadRules">刷新</el-button>
            <el-button :disabled="!targetReady" size="small" type="primary" @click="openCreateReport">
              新增定时报表
            </el-button>
          </div>
        </div>

        <el-table v-loading="loading" :data="reportRules" border size="small">
          <el-table-column label="名称" min-width="180" prop="ruleName" show-overflow-tooltip />
          <el-table-column label="启用" width="78">
            <template #default="{ row }">
              <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
                {{ row.isEnabled ? '启用' : '禁用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="Cron" min-width="130" prop="cron" />
          <el-table-column label="时区" min-width="130" prop="timeZoneId" />
          <el-table-column label="查询变量" width="100">
            <template #default="{ row }">{{ row.queryItems.length }}</template>
          </el-table-column>
          <el-table-column label="上次发送" min-width="160">
            <template #default="{ row }">{{ formatDateTime(row.lastRunAt) }}</template>
          </el-table-column>
          <el-table-column label="下次发送" min-width="160">
            <template #default="{ row }">{{ formatDateTime(row.nextRunAt) }}</template>
          </el-table-column>
          <el-table-column fixed="right" label="操作" width="150">
            <template #default="{ row }">
              <el-button link size="small" type="primary" @click="openEditReport(row)">编辑</el-button>
              <el-button link size="small" type="danger" @click="deleteReportRule(row)">禁用</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="告警消息" name="messages">
        <div class="toolbar">
          <div class="toolbar-title">应用：{{ targetKey || '--' }}</div>
          <div class="toolbar-actions">
            <el-button :loading="notifyLogsLoading" plain size="small" @click="loadAlertNotifyLogs()">刷新</el-button>
          </div>
        </div>

        <div class="notify-log-filters">
          <el-select
            v-model="notifyLogFilters.ruleValue"
            clearable
            filterable
            placeholder="按告警规则筛选"
            style="width: 280px"
          >
            <el-option
              v-for="option in notifyLogRuleOptions"
              :key="option.value"
              :label="option.label"
              :value="option.value"
            />
          </el-select>
          <el-date-picker
            v-model="notifyLogFilters.timeRange"
            end-placeholder="结束时间"
            range-separator="~"
            start-placeholder="开始时间"
            type="datetimerange"
          />
          <el-button :loading="notifyLogsLoading" plain size="small" @click="applyNotifyLogFilters">筛选</el-button>
          <el-button :loading="notifyLogsLoading" plain size="small" @click="resetNotifyLogFilters">重置</el-button>
        </div>

        <el-table v-loading="notifyLogsLoading" :data="notifyLogs" border size="small">
          <el-table-column label="时间" min-width="170">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="类型" width="110">
            <template #default="{ row }">
              <el-tag size="small">{{ formatAlertType(row.alertType) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="动作" width="90">
            <template #default="{ row }">
              <el-tag :type="row.notifyType === 2 ? 'success' : 'danger'" size="small">
                {{ formatNotifyType(row.notifyType) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="规则" min-width="180">
            <template #default="{ row }">{{ row.ruleName || '--' }}</template>
          </el-table-column>
          <el-table-column label="标题" min-width="220" prop="title" show-overflow-tooltip />
          <el-table-column label="结果" width="90">
            <template #default="{ row }">
              <el-tag :type="row.isSuccess ? 'success' : 'danger'" size="small">
                {{ row.isSuccess ? '成功' : '失败' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="内容" min-width="360" prop="content" show-overflow-tooltip />
          <el-table-column label="错误信息" min-width="220" prop="errorMessage" show-overflow-tooltip />
        </el-table>

        <div class="notify-log-pagination">
          <el-pagination
            :current-page="notifyLogsPage"
            :page-size="notifyLogsPageSize"
            :page-sizes="[20, 50, 100]"
            :total="notifyLogsTotal"
            background
            layout="total, sizes, prev, pager, next"
            @current-change="handleNotifyLogsPageChange"
            @size-change="handleNotifyLogsPageSizeChange"
          />
        </div>
      </el-tab-pane>
    </el-tabs>

    <el-drawer v-model="alertDrawerVisible" :title="alertForm.id ? '编辑告警规则' : '新增告警规则'" size="1080px">
      <el-form label-width="118px">
        <el-form-item label="规则类型">
          <el-radio-group
            v-model="alertForm.type"
            :disabled="alertForm.id > 0"
            size="small"
          >
            <el-radio-button label="alive">在线实例数</el-radio-button>
            <el-radio-button label="url">URL巡检</el-radio-button>
            <el-radio-button label="metric">指标公式</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="规则名称">
          <el-input v-model="alertForm.ruleName" />
        </el-form-item>

        <template v-if="alertForm.type === 'alive'">
          <el-form-item label="最小在线数">
            <el-input-number v-model="alertForm.minAliveCount" :min="1" controls-position="right" style="width: 100%" />
          </el-form-item>
          <el-form-item label="离线秒数">
            <el-input-number v-model="alertForm.offlineSeconds" :min="1" controls-position="right" style="width: 100%" />
          </el-form-item>
        </template>

        <template v-if="alertForm.type === 'url'">
          <el-form-item label="URL 名称">
            <el-input v-model="alertForm.urlName" />
          </el-form-item>
          <el-form-item label="URL">
            <el-input v-model="alertForm.url" placeholder="https://example.com/health" />
          </el-form-item>
          <el-form-item label="HTTP 方法">
            <el-select v-model="alertForm.httpMethod" style="width: 100%">
              <el-option label="GET" value="GET" />
              <el-option label="POST" value="POST" />
              <el-option label="PUT" value="PUT" />
              <el-option label="DELETE" value="DELETE" />
              <el-option label="HEAD" value="HEAD" />
            </el-select>
          </el-form-item>
          <el-form-item label="超时秒数">
            <el-input-number v-model="alertForm.timeoutSeconds" :min="1" controls-position="right" style="width: 100%" />
          </el-form-item>
          <el-form-item label="巡检间隔秒">
            <el-input-number v-model="alertForm.checkIntervalSeconds" :min="1" controls-position="right" style="width: 100%" />
          </el-form-item>
          <el-form-item label="期望状态码">
            <el-input v-model="alertForm.expectedStatusCodesText" placeholder="200,204" />
          </el-form-item>
        </template>

        <template v-if="alertForm.type === 'metric'">
          <el-form-item label="查询变量">
            <div class="query-editor">
              <el-table :data="alertForm.queryItems" border size="small" table-layout="auto">
                <el-table-column label="变量名" width="150">
                  <template #default="{ row }">
                    <el-input v-model="row.name" placeholder="cpuUsage" />
                  </template>
                </el-table-column>
                <el-table-column label="PromQL" min-width="460">
                  <template #default="{ row, $index }">
                    <div class="promql-cell">
                      <el-input
                        :model-value="row.promql"
                        placeholder="点击编辑 PromQL"
                        readonly
                        @click="openPromqlEditor('alert', $index)"
                      />
                      <el-button plain size="small" @click="openPromqlEditor('alert', $index)">编辑</el-button>
                    </div>
                  </template>
                </el-table-column>
                <el-table-column width="130">
                  <template #header>
                    <span class="column-help">
                      聚合方式
                      <el-tooltip
                        content="只在勾选“聚合实例”时生效。avg=平均值，sum=求和，max=最大值，min=最小值，count=计数。"
                        placement="top"
                      >
                        <span class="help-badge">?</span>
                      </el-tooltip>
                    </span>
                  </template>
                  <template #default="{ row }">
                    <el-select v-model="row.aggregateMode">
                      <el-option label="平均值" value="avg" />
                      <el-option label="求和" value="sum" />
                      <el-option label="最大值" value="max" />
                      <el-option label="最小值" value="min" />
                      <el-option label="计数" value="count" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column width="130">
                  <template #header>
                    <span class="column-help">
                      取值方式
                      <el-tooltip
                        content="当前值：直接使用这次查询结果。变化值：表示更关注和上一次相比是否变化，通常配合公式里的 changed(x)、delta(x) 使用。"
                        placement="top"
                      >
                        <span class="help-badge">?</span>
                      </el-tooltip>
                    </span>
                  </template>
                  <template #default="{ row }">
                    <el-select v-model="row.valueMode">
                      <el-option label="当前值" value="instant" />
                      <el-option label="变化值" value="changed" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="操作" width="72">
                  <template #default="{ $index }">
                    <el-button link size="small" type="danger" @click="removeMetricQueryItem('alert', $index)">删除</el-button>
                  </template>
                </el-table-column>
              </el-table>
              <el-button class="query-add" plain size="small" @click="addMetricQueryItem('alert')">添加变量</el-button>
            </div>
          </el-form-item>
          <el-form-item label="实例模式">
            <div class="mode-editor">
              <el-radio-group v-model="alertForm.aggregateInstances">
                <el-radio-button :label="false">分别计算</el-radio-button>
                <el-radio-button :label="true">聚合计算</el-radio-button>
              </el-radio-group>
              <el-tooltip
                content="分别计算：每个实例独立判断和告警。聚合计算：先把所有实例按每个变量聚合成一个值，再统一判断。"
                placement="top"
              >
                <span class="help-badge">?</span>
              </el-tooltip>
            </div>
          </el-form-item>
          <el-form-item label="公式">
            <el-input v-model="alertForm.formula" placeholder="cpuUsage > 80 && errorRate > 2" />
          </el-form-item>
          <el-form-item label="告警模板">
            <el-input v-model="alertForm.messageTemplate" :rows="5" type="textarea" />
          </el-form-item>
          <el-form-item label="恢复模板">
            <el-input v-model="alertForm.recoverTemplate" :rows="4" type="textarea" />
          </el-form-item>
        </template>

        <el-form-item v-if="alertForm.type !== 'metric'" label="持续分钟">
          <el-input-number v-model="alertForm.durationMinutes" :min="0" controls-position="right" style="width: 100%" />
        </el-form-item>
        <el-form-item v-else label="持续分钟">
          <el-input-number v-model="alertForm.durationMinutes" :min="0" controls-position="right" style="width: 100%" />
        </el-form-item>
        <el-form-item label="通知队列">
          <el-input v-model="alertForm.notifyMinuteQueueText" placeholder="0,5,10,120" />
        </el-form-item>
        <el-form-item label="通知人">
          <el-input v-model="alertForm.notifyUsersText" :rows="3" type="textarea" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="alertForm.isEnabled" active-text="启用" inactive-text="禁用" inline-prompt />
        </el-form-item>
      </el-form>

      <div v-if="alertForm.type === 'metric'" class="preview-actions">
        <el-button :loading="previewingAlert" plain @click="previewMetricAlert">查询测试</el-button>
        <el-button :loading="previewingAlert" plain @click="testSendMetricAlert">发测试消息</el-button>
      </div>
      <div v-if="alertPreview" class="preview-box">
        <div class="preview-title">查询结果</div>
        <pre>{{ alertPreview.currentValues }}</pre>
        <div class="preview-title">告警消息</div>
        <pre>{{ alertPreview.messageContent }}</pre>
        <div class="preview-title">恢复消息</div>
        <pre>{{ alertPreview.recoverContent }}</pre>
      </div>

      <template #footer>
        <el-button @click="alertDrawerVisible = false">关闭</el-button>
        <el-button :loading="savingAlert" type="primary" @click="submitAlertForm">保存</el-button>
      </template>
    </el-drawer>

    <el-drawer v-model="reportDrawerVisible" :title="reportForm.id ? '编辑定时报表' : '新增定时报表'" size="1080px">
      <el-form label-width="118px">
        <el-form-item label="报表名称">
          <el-input v-model="reportForm.ruleName" />
        </el-form-item>
        <el-form-item label="Cron">
          <el-input v-model="reportForm.cron" />
        </el-form-item>
        <el-form-item label="时区">
          <el-input v-model="reportForm.timeZoneId" />
        </el-form-item>
        <el-form-item label="查询变量">
          <div class="query-editor">
            <el-table :data="reportForm.queryItems" border size="small" table-layout="auto">
              <el-table-column label="变量名" width="150">
                <template #default="{ row }">
                  <el-input v-model="row.name" placeholder="orderCount" />
                </template>
              </el-table-column>
              <el-table-column label="PromQL" min-width="460">
                <template #default="{ row, $index }">
                  <div class="promql-cell">
                    <el-input
                      :model-value="row.promql"
                      placeholder="点击编辑 PromQL"
                      readonly
                      @click="openPromqlEditor('report', $index)"
                    />
                    <el-button plain size="small" @click="openPromqlEditor('report', $index)">编辑</el-button>
                  </div>
                </template>
              </el-table-column>
              <el-table-column width="130">
                <template #header>
                  <span class="column-help">
                    聚合方式
                    <el-tooltip
                      content="只在勾选“聚合实例”时生效。avg=平均值，sum=求和，max=最大值，min=最小值，count=计数。"
                      placement="top"
                    >
                      <span class="help-badge">?</span>
                    </el-tooltip>
                  </span>
                </template>
                <template #default="{ row }">
                  <el-select v-model="row.aggregateMode">
                    <el-option label="平均值" value="avg" />
                    <el-option label="求和" value="sum" />
                    <el-option label="最大值" value="max" />
                    <el-option label="最小值" value="min" />
                    <el-option label="计数" value="count" />
                  </el-select>
                </template>
              </el-table-column>
              <el-table-column width="130">
                <template #header>
                  <span class="column-help">
                    取值方式
                    <el-tooltip
                      content="当前值：直接使用这次查询结果。变化值：表示更关注和上一次相比是否变化，通常配合公式里的 changed(x)、delta(x) 使用。"
                      placement="top"
                    >
                      <span class="help-badge">?</span>
                    </el-tooltip>
                  </span>
                </template>
                <template #default="{ row }">
                  <el-select v-model="row.valueMode">
                    <el-option label="当前值" value="instant" />
                    <el-option label="变化值" value="changed" />
                  </el-select>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="72">
                <template #default="{ $index }">
                  <el-button link size="small" type="danger" @click="removeMetricQueryItem('report', $index)">删除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <el-button class="query-add" plain size="small" @click="addMetricQueryItem('report')">添加变量</el-button>
          </div>
        </el-form-item>
        <el-form-item label="实例模式">
          <div class="mode-editor">
            <el-radio-group v-model="reportForm.aggregateInstances">
              <el-radio-button :label="false">分别发送</el-radio-button>
              <el-radio-button :label="true">聚合发送</el-radio-button>
            </el-radio-group>
            <el-tooltip
              content="分别发送：每个实例各渲染一条消息。聚合发送：先把所有实例按每个变量聚合成一个值，再发送一条消息。"
              placement="top"
            >
              <span class="help-badge">?</span>
            </el-tooltip>
          </div>
        </el-form-item>
        <el-form-item label="消息模板">
          <el-input v-model="reportForm.messageTemplate" :rows="6" type="textarea" />
        </el-form-item>
        <el-form-item label="通知人">
          <el-input v-model="reportForm.notifyUsersText" :rows="3" type="textarea" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="reportForm.isEnabled" active-text="启用" inactive-text="禁用" inline-prompt />
        </el-form-item>
      </el-form>

      <div class="preview-actions">
        <el-button :loading="previewingReport" plain @click="previewReport">模板预览</el-button>
        <el-button :loading="previewingReport" plain @click="testSendReport">发测试消息</el-button>
      </div>
      <div v-if="reportPreview" class="preview-box">
        <div class="preview-title">查询结果</div>
        <pre>{{ reportPreview.currentValues }}</pre>
        <div class="preview-title">消息内容</div>
        <pre>{{ reportPreview.messageContent }}</pre>
      </div>

      <template #footer>
        <el-button @click="reportDrawerVisible = false">关闭</el-button>
        <el-button :loading="savingReport" type="primary" @click="submitReportForm">保存</el-button>
      </template>
    </el-drawer>

    <el-dialog v-model="promqlEditorVisible" title="编辑 PromQL" width="80vw" top="6vh">
      <div class="promql-editor-toolbar">
        <el-switch
          v-model="promqlEditorAutoScope"
          active-text="自动补当前应用/实例范围"
          inactive-text="不自动补范围"
        />
        <span class="promql-editor-tip">
          已手写 <code>exported_job</code> / <code>exported_instance</code> 时保持原样
        </span>
      </div>
      <el-input v-model="promqlEditorValue" :rows="18" type="textarea" />

      <template #footer>
        <el-button @click="promqlEditorVisible = false">取消</el-button>
        <el-button :loading="promqlEditorValidating" type="primary" @click="applyPromqlEditor">确定并验证</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.alert-rules-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
}

.rules-tabs {
  min-width: 0;
}

.toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.toolbar-title {
  min-width: 0;
  overflow: hidden;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.toolbar-actions {
  display: flex;
  flex: 0 0 auto;
  gap: 8px;
  align-items: center;
}

.query-editor {
  width: 100%;
  min-width: 0;
  overflow-x: auto;
}

.query-editor :deep(.el-table) {
  min-width: 980px;
}

.query-add {
  margin-top: 8px;
}

.promql-cell {
  display: flex;
  gap: 8px;
  align-items: center;
}

.promql-cell :deep(.el-input) {
  min-width: 0;
  flex: 1 1 auto;
}

.column-help {
  display: inline-flex;
  gap: 6px;
  align-items: center;
}

.help-badge {
  display: inline-flex;
  width: 16px;
  height: 16px;
  cursor: help;
  border: 1px solid var(--el-border-color);
  border-radius: 999px;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  line-height: 1;
  color: var(--el-text-color-secondary);
}

.mode-editor {
  display: flex;
  gap: 8px;
  align-items: center;
}

.promql-editor-toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.promql-editor-tip {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.preview-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  padding-top: 4px;
}

.preview-box {
  padding: 10px;
  margin-top: 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.preview-title {
  margin: 8px 0 4px;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
}

.preview-title:first-child {
  margin-top: 0;
}

.notify-log-pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 12px;
}

.notify-log-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 12px;
}

pre {
  max-height: 220px;
  padding: 8px;
  margin: 0;
  overflow: auto;
  font-size: 12px;
  line-height: 1.5;
  color: var(--el-text-color-primary);
  white-space: pre-wrap;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}
</style>
