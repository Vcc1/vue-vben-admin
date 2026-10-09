<script lang="ts" setup>
import type {
  AppSummaryItem,
  AppViewModel,
  MonitorTargetAppGroup,
  MonitorTargetConfig,
  MonitorTargetGroupOption,
  MonitorTargetSidebarGroup,
} from './types';

import type {
  PrometheusSeriesResult,
  PrometheusVectorResult,
} from '#/api/prometheus/types';

import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';

import { ElMessage } from 'element-plus';

import {
  MetricsAndAlertsBaseUrl,
  MetricsAndAlertsClient,
  type AlertStateDto,
  type MetricsAndAlertsGroupDto,
  type MetricsAndAlertsMonitorTargetDto,
} from '#/api/metricsAndAlerts';
import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import AppBox from './AppBox.vue';
import AppSidebar from './AppSidebar.vue';

const client = new PrometheusClient();
const metricsAndAlertsClient = new MetricsAndAlertsClient();

const DEFAULT_MONITOR_TARGET_GROUP: MonitorTargetGroupOption = {
  groupName: '默认',
  id: 0,
  isDefault: true,
  isEnabled: true,
  sortOrder: Number.MAX_SAFE_INTEGER,
};
const HEARTBEAT_ONLINE_SECONDS = 300;
const HISTORY_LOOKBACK_SECONDS = 30 * 24 * 60 * 60;
const INSTANCE_QUERY_BATCH_SIZE = 10;

const metricsAndAlertsConfigured = Boolean(MetricsAndAlertsBaseUrl);
const loading = ref(false);
const selectedAppName = ref('');
const showHistory = ref(false);
const autoRefreshEnabled = ref(true);
const refreshKey = ref(0);
const appMap = ref<Record<string, AppViewModel>>({});
const historyAppMap = ref<Record<string, AppViewModel>>({});
const historyLoading = ref(false);
const historyLoadedAt = ref(0);
const historyDetailLoadingKeys = new Set<string>();
const monitorTargetGroups = ref<MonitorTargetGroupOption[]>([]);
const monitorTargetMap = ref<Record<string, MonitorTargetConfig>>({});
const activeAlertStates = ref<AlertStateDto[]>([]);
const monitorTargetConfigLoadedAt = ref(0);

let pollingTimer: number | undefined;
let monitorTargetErrorShown = false;

const groupDialog = reactive({
  form: {
    groupName: '',
    id: 0,
    isEnabled: true,
    sortOrder: 0,
  },
  mode: 'create' as 'create' | 'edit',
  visible: false,
});

const appDialog = reactive({
  form: {
    groupId: 0,
    isEnabled: true,
    notifyUsersText: '',
    sortOrder: 0,
    targetKey: '',
    targetName: '',
  },
  visible: false,
});

const savingGroup = ref(false);
const savingApp = ref(false);

function isRecentHeartbeat(item: AppViewModel) {
  if (!item.heartbeatTimestamp) {
    return false;
  }
  const nowSeconds = Math.floor(Date.now() / 1000);
  return nowSeconds - item.heartbeatTimestamp <= HEARTBEAT_ONLINE_SECONDS;
}

const displayedEntries = computed(() =>
  Object.entries(appMap.value).filter(
    ([, item]) =>
      showHistory.value ||
      item.heartbeatTimestamp === null ||
      isRecentHeartbeat(item),
  ),
);

function getEnvironmentName(item: AppViewModel) {
  return (
    item.attributes.deployment_environment ||
    item.attributes.environment ||
    item.attributes.env ||
    '--'
  );
}

function getHostName(item: AppViewModel) {
  return item.attributes.host_name || item.attributes['host.name'] || '--';
}

function compareHeartbeatDesc(a: null | number, b: null | number) {
  const left = a ?? -1;
  const right = b ?? -1;
  return right - left;
}

function compareAppSummaryItem(a: AppSummaryItem, b: AppSummaryItem) {
  const heartbeatOrder = compareHeartbeatDesc(
    a.heartbeatTimestamp,
    b.heartbeatTimestamp,
  );
  if (heartbeatOrder !== 0) {
    return heartbeatOrder;
  }

  const envOrder = a.environmentName.localeCompare(b.environmentName);
  if (envOrder !== 0) {
    return envOrder;
  }

  const hostOrder = a.hostName.localeCompare(b.hostName);
  if (hostOrder !== 0) {
    return hostOrder;
  }

  return a.instance.localeCompare(b.instance);
}

function compareMonitorTargetGroups(
  a: MonitorTargetGroupOption,
  b: MonitorTargetGroupOption,
) {
  if (a.isDefault && !b.isDefault) {
    return 1;
  }
  if (!a.isDefault && b.isDefault) {
    return -1;
  }
  if (a.sortOrder !== b.sortOrder) {
    return a.sortOrder - b.sortOrder;
  }
  if (a.groupName !== b.groupName) {
    return a.groupName.localeCompare(b.groupName);
  }
  return a.id - b.id;
}

function compareSidebarGroups(
  a: MonitorTargetSidebarGroup,
  b: MonitorTargetSidebarGroup,
) {
  if (a.isDefault && !b.isDefault) {
    return 1;
  }
  if (!a.isDefault && b.isDefault) {
    return -1;
  }
  if (a.sortOrder !== b.sortOrder) {
    return a.sortOrder - b.sortOrder;
  }
  if (a.groupName !== b.groupName) {
    return a.groupName.localeCompare(b.groupName);
  }
  return a.groupId - b.groupId;
}

function compareMonitorTargetApps(
  a: MonitorTargetAppGroup,
  b: MonitorTargetAppGroup,
) {
  const leftSort = a.appTarget?.sortOrder ?? Number.MAX_SAFE_INTEGER;
  const rightSort = b.appTarget?.sortOrder ?? Number.MAX_SAFE_INTEGER;
  if (leftSort !== rightSort) {
    return leftSort - rightSort;
  }
  return a.appName.localeCompare(b.appName);
}

function isOnlineHeartbeat(timestamp: null | number) {
  if (!timestamp) {
    return false;
  }
  const nowSeconds = Math.floor(Date.now() / 1000);
  return nowSeconds - timestamp <= HEARTBEAT_ONLINE_SECONDS;
}

function normalizeAlertInstanceName(value?: null | string) {
  return value?.trim().toLowerCase() || '';
}

function hasActiveAppAlert(appName: string) {
  return activeAlertStates.value.some(
    (state) =>
      state.targetKey === appName &&
      state.isActive &&
      !normalizeAlertInstanceName(state.instanceName),
  );
}

function hasActiveInstanceAlert(appName: string, instance: string) {
  const normalizedInstance = normalizeAlertInstanceName(instance);
  if (!normalizedInstance) {
    return false;
  }

  return activeAlertStates.value.some(
    (state) =>
      state.targetKey === appName &&
      state.isActive &&
      normalizeAlertInstanceName(state.instanceName) === normalizedInstance,
  );
}

const appItems = computed<AppSummaryItem[]>(() =>
  displayedEntries.value
    .map(([itemKey, item]) => ({
      appName: item.appName,
      environmentName: getEnvironmentName(item),
      hasActiveInstanceAlert: hasActiveInstanceAlert(item.appName, item.instance),
      heartbeatTimestamp: item.heartbeatTimestamp,
      hostName: getHostName(item),
      instance: item.instance,
      itemKey,
      jobName: item.jobDisplayName,
      serviceVersion: item.serviceVersion || '--',
    }))
    .sort(compareAppSummaryItem),
);

const sidebarGroups = computed<MonitorTargetSidebarGroup[]>(() => {
  const configuredGroups = new Map<number, MonitorTargetSidebarGroup>();
  const defaultGroup: MonitorTargetSidebarGroup = {
    activeAlertCount: 0,
    apps: [],
    groupId: DEFAULT_MONITOR_TARGET_GROUP.id,
    groupKey: `group:${DEFAULT_MONITOR_TARGET_GROUP.id}`,
    groupName: DEFAULT_MONITOR_TARGET_GROUP.groupName,
    isDefault: true,
    isEnabled: true,
    onlineCount: 0,
    sortOrder: DEFAULT_MONITOR_TARGET_GROUP.sortOrder,
  };

  configuredGroups.set(defaultGroup.groupId, defaultGroup);

  for (const group of monitorTargetGroups.value) {
    configuredGroups.set(group.id, {
      activeAlertCount: 0,
      apps: [],
      groupId: group.id,
      groupKey: `group:${group.id}`,
      groupName: group.groupName,
      isDefault: false,
      isEnabled: group.isEnabled,
      onlineCount: 0,
      sortOrder: group.sortOrder,
    });
  }

  const apps = new Map<string, MonitorTargetAppGroup>();
  for (const item of appItems.value) {
    const current = apps.get(item.appName);
    const targetConfig = monitorTargetMap.value[item.appName] ?? null;

    if (current) {
      current.items.push(item);
      if (
        compareHeartbeatDesc(
          item.heartbeatTimestamp,
          current.latestHeartbeatTimestamp,
        ) < 0
      ) {
        current.latestHeartbeatTimestamp = item.heartbeatTimestamp;
      }
      if (isOnlineHeartbeat(item.heartbeatTimestamp)) {
        current.onlineCount += 1;
      }
      continue;
    }

    apps.set(item.appName, {
      appName: item.appName,
      appTarget: targetConfig,
      groupKey: `app:${item.appName}`,
      hasActiveAppAlert: hasActiveAppAlert(item.appName),
      items: [item],
      latestHeartbeatTimestamp: item.heartbeatTimestamp,
      onlineCount: isOnlineHeartbeat(item.heartbeatTimestamp) ? 1 : 0,
    });
  }

  for (const app of apps.values()) {
    app.items.sort(compareAppSummaryItem);

    const rawGroupId = app.appTarget?.groupId ?? null;
    const resolvedGroupId =
      rawGroupId !== null && configuredGroups.has(rawGroupId) ? rawGroupId : 0;
    const parent = configuredGroups.get(resolvedGroupId) || defaultGroup;

    parent.apps.push(app);
    parent.onlineCount += app.onlineCount;
    if (app.hasActiveAppAlert || app.items.some((item) => item.hasActiveInstanceAlert)) {
      parent.activeAlertCount += 1;
    }
  }

  return [...configuredGroups.values()]
    .map((group) => ({
      ...group,
      apps: [...group.apps].sort(compareMonitorTargetApps),
    }))
    .sort(compareSidebarGroups);
});

const displayedAppMap = computed(() => Object.fromEntries(displayedEntries.value));
const selectedApp = computed(
  () => displayedAppMap.value[selectedAppName.value] || null,
);
const selectedAppTargetConfig = computed(() =>
  selectedApp.value ? monitorTargetMap.value[selectedApp.value.appName] ?? null : null,
);
const availableMonitorTargetGroups = computed(() =>
  [DEFAULT_MONITOR_TARGET_GROUP, ...monitorTargetGroups.value].sort(
    compareMonitorTargetGroups,
  ),
);

function parseAppName(job: string) {
  if (!job) {
    return 'unknown-app';
  }
  const parts = job.split('/');
  const suffix = parts.at(-1)?.trim();
  return suffix || job;
}

function getJobSuffix(job: string) {
  if (!job) {
    return null;
  }
  return parseAppName(job);
}

function getMetricInstance(metric: Record<string, string>) {
  return metric.exported_instance || metric.instance || '';
}

function getMetricJob(metric: Record<string, string>) {
  return metric.exported_job || metric.job || '';
}

function escapeRegex(value: string) {
  return value.replaceAll(/[.*+?^${}()|[\]\\]/g, String.raw`\\$&`);
}

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', String.raw`\"`);
}

function dedupeVectorRows(rows: PrometheusVectorResult[]) {
  const map = new Map<string, PrometheusVectorResult>();
  for (const row of rows) {
    const key = JSON.stringify(row.metric);
    if (!map.has(key)) {
      map.set(key, row);
    }
  }
  return [...map.values()];
}

function chunkValues<T>(values: T[], size: number) {
  if (size <= 0) {
    return [values];
  }

  const chunks: T[][] = [];
  for (let index = 0; index < values.length; index += size) {
    chunks.push(values.slice(index, index + size));
  }
  return chunks;
}

async function queryByInstance(
  buildQuery: (labelName: 'exported_instance' | 'instance') => string,
  time?: number,
) {
  const labels: Array<'exported_instance' | 'instance'> = [
    'exported_instance',
    'instance',
  ];
  const allRows: PrometheusVectorResult[] = [];

  for (const labelName of labels) {
    try {
      const rows = await client.query(buildQuery(labelName), time);
      allRows.push(...rows);
    } catch {
      // ignore one label path and let the fallback path continue
    }
  }

  return dedupeVectorRows(allRows);
}

async function queryByInstances(
  instances: string[],
  buildQuery: (
    labelName: 'exported_instance' | 'instance',
    matcher: string,
  ) => string,
  time?: number,
) {
  const uniqueInstances = [...new Set(instances.filter(Boolean))];
  if (uniqueInstances.length === 0) {
    return [] as PrometheusVectorResult[];
  }

  const labels: Array<'exported_instance' | 'instance'> = [
    'exported_instance',
    'instance',
  ];
  const allRows: PrometheusVectorResult[] = [];
  const batches = chunkValues(uniqueInstances, INSTANCE_QUERY_BATCH_SIZE);

  for (const batch of batches) {
    const batchMatcher = batch.map((value) => escapeRegex(value)).join('|');
    if (!batchMatcher) {
      continue;
    }

    for (const labelName of labels) {
      try {
        const rows = await client.query(
          buildQuery(labelName, batchMatcher),
          time,
        );
        allRows.push(...rows);
      } catch {
        // ignore one label path and let the fallback path continue
      }
    }
  }

  return dedupeVectorRows(allRows);
}

function createHistoricalItem(
  metric: PrometheusSeriesResult,
  appName: string,
  instance: string,
): AppViewModel {
  const attrs = Object.fromEntries(
    Object.entries(metric)
      .filter(([key, value]) => {
        if (!value) {
          return false;
        }
        return ![
          '__name__',
          'exported_instance',
          'exported_job',
          'instance',
          'job',
        ].includes(key);
      })
      .sort((a, b) => a[0].localeCompare(b[0])),
  );

  return {
    appName,
    attributes: attrs,
    availableMetrics: [],
    config: {},
    heartbeatTimestamp: null,
    instance,
    jobDisplayName: appName,
    latestMetricValues: [],
    inflight: null,
    instances: [instance],
    jobs: [getMetricJob(metric)].filter((job): job is string => Boolean(job)),
    serviceVersion: metric.service_version || '',
    startTimeSeconds: null,
    uptimeSeconds: null,
  };
}

async function loadHistoricalApps() {
  const now = Math.floor(Date.now() / 1000);
  if (
    historyLoading.value ||
    (historyLoadedAt.value && now - historyLoadedAt.value < HEARTBEAT_ONLINE_SECONDS)
  ) {
    return;
  }

  historyLoading.value = true;
  try {
    const series = await client.series(
      ['target_info{type="AppMetrics"}'],
      now - HISTORY_LOOKBACK_SECONDS,
      now,
    );

    const nextHistoryMap: Record<string, AppViewModel> = {
      ...historyAppMap.value,
    };

    for (const metric of series) {
      const job = getMetricJob(metric);
      const appName = getJobSuffix(job);
      const instance = getMetricInstance(metric);
      if (!appName || !instance) {
        continue;
      }

      const key = `${appName}@@${instance}`;
      if (!nextHistoryMap[key]) {
        nextHistoryMap[key] = createHistoricalItem(metric, appName, instance);
      }
    }

    const heartbeatRows = await queryByInstances(
      Object.values(nextHistoryMap).map((item) => item.instance),
      (labelName, matcher) =>
        `max_over_time(app_last_heartbeat_timestamp{${labelName}=~"${matcher}"}[30d])`,
    );

    const heartbeatByInstance = new Map<string, number>();
    for (const row of heartbeatRows) {
      const instance = getMetricInstance(row.metric);
      const heartbeat = Number(row.value[1]);
      if (!instance || !Number.isFinite(heartbeat)) {
        continue;
      }

      const previous = heartbeatByInstance.get(instance);
      if (previous === undefined || heartbeat > previous) {
        heartbeatByInstance.set(instance, heartbeat);
      }
    }

    for (const [key, item] of Object.entries(nextHistoryMap)) {
      const heartbeat = heartbeatByInstance.get(item.instance);
      if (heartbeat === undefined) {
        continue;
      }

      nextHistoryMap[key] = {
        ...item,
        heartbeatTimestamp: Math.floor(heartbeat),
      };
    }

    historyAppMap.value = nextHistoryMap;
    historyLoadedAt.value = now;
  } catch (error) {
    ElMessage.warning(
      `加载历史应用失败: ${error instanceof Error ? error.message : String(error)}`,
    );
  } finally {
    historyLoading.value = false;
  }
}

async function loadHistoricalSnapshot(itemKey: string) {
  if (!showHistory.value || historyDetailLoadingKeys.has(itemKey)) {
    return;
  }

  const current = appMap.value[itemKey];
  if (!current) {
    return;
  }

  historyDetailLoadingKeys.add(itemKey);
  try {
    const instance = current.instance;
    const escapedInstance = escapeLabelValue(instance);

    const heartbeatRows = await queryByInstance(
      (labelName) =>
        `max_over_time(app_last_heartbeat_timestamp{${labelName}="${escapedInstance}"}[30d])`,
    );

    const heartbeatValue = heartbeatRows
      .map((row) => Number(row.value[1]))
      .filter((value) => Number.isFinite(value))
      .sort((a, b) => b - a)[0];

    const hasHeartbeatValue =
      typeof heartbeatValue === 'number' && Number.isFinite(heartbeatValue);
    const snapshotTime = hasHeartbeatValue
      ? Math.floor(heartbeatValue)
      : undefined;

    const snapshotRows = await queryByInstance(
      (labelName) => `{${labelName}="${escapedInstance}"}`,
      snapshotTime,
    );

    if (snapshotRows.length === 0) {
      return;
    }

    const instanceSet = new Set([instance]);
    const targetRows = snapshotRows.filter(
      (row) => row.metric.__name__ === 'target_info',
    );
    const jobs = [
      ...new Set(
        targetRows.map((row) => getMetricJob(row.metric)).filter(Boolean),
      ),
    ];

    const nextItem: AppViewModel = {
      ...current,
      attributes: mergeAttributes(targetRows, snapshotRows, instanceSet),
      availableMetrics: extractAvailableMetrics(snapshotRows, instanceSet),
      heartbeatTimestamp:
        (hasHeartbeatValue ? Math.floor(heartbeatValue) : null) ||
        current.heartbeatTimestamp,
      jobDisplayName:
        jobs
          .map((job) => getJobSuffix(job))
          .filter((job): job is string => Boolean(job))
          .sort((a, b) => a.localeCompare(b))[0] || current.jobDisplayName,
      jobs: jobs.length > 0 ? jobs : current.jobs,
      latestMetricValues: extractLatestMetricValues(snapshotRows, instanceSet),
      inflight: numberFromResult(snapshotRows, 'app_inflight', instanceSet),
      startTimeSeconds: numberFromResult(
        snapshotRows,
        'app_start_time_seconds',
        instanceSet,
        'min',
      ),
      serviceVersion:
        targetRows.find((row) => row.metric.service_version)?.metric
          .service_version || current.serviceVersion,
      uptimeSeconds: numberFromResult(
        snapshotRows,
        'app_uptime_seconds',
        instanceSet,
      ),
    };

    appMap.value = {
      ...appMap.value,
      [itemKey]: nextItem,
    };
    historyAppMap.value = {
      ...historyAppMap.value,
      [itemKey]: nextItem,
    };
  } catch (error) {
    ElMessage.warning(
      `加载历史快照失败: ${error instanceof Error ? error.message : String(error)}`,
    );
  } finally {
    historyDetailLoadingKeys.delete(itemKey);
  }
}

function numberFromResult(
  list: PrometheusVectorResult[],
  metricName: string,
  instanceSet: Set<string>,
  strategy: 'max' | 'min' = 'max',
) {
  const rows = list.filter(
    (item) =>
      item.metric.__name__ === metricName &&
      instanceSet.has(getMetricInstance(item.metric)),
  );
  if (rows.length === 0) {
    return null;
  }

  const values = rows
    .map((item) => Number(item.value[1]))
    .filter((value) => Number.isFinite(value));
  if (values.length === 0) {
    return null;
  }

  return strategy === 'min' ? Math.min(...values) : Math.max(...values);
}

function mergeConfig(list: PrometheusVectorResult[], instanceSet: Set<string>) {
  const output: Record<string, string> = {};
  for (const row of list) {
    if (row.metric.__name__ !== 'app_config_info') {
      continue;
    }
    if (!instanceSet.has(getMetricInstance(row.metric))) {
      continue;
    }
    const key = row.metric.key;
    const value = row.metric.value;
    if (key) {
      output[key] = value || '';
    }
  }
  return output;
}

function mergeAttributes(
  targetInfo: PrometheusVectorResult[],
  metricsVector: PrometheusVectorResult[],
  instanceSet: Set<string>,
) {
  const output = new Map<string, Set<string>>();
  const ignoreKeys = new Set([
    '__name__',
    'exported_instance',
    'exported_job',
    'instance',
    'job',
    'key',
    'value',
  ]);

  const append = (metric: Record<string, string>) => {
    for (const [key, value] of Object.entries(metric)) {
      if (ignoreKeys.has(key) || !value) {
        continue;
      }
      const current = output.get(key) || new Set<string>();
      current.add(value);
      output.set(key, current);
    }
  };

  for (const row of targetInfo) {
    if (!instanceSet.has(getMetricInstance(row.metric))) {
      continue;
    }
    append(row.metric);
  }

  for (const row of metricsVector) {
    if (!instanceSet.has(getMetricInstance(row.metric))) {
      continue;
    }
    if (!row.metric.__name__?.startsWith('app_')) {
      continue;
    }
    append(row.metric);
  }

  return Object.fromEntries(
    [...output.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([key, set]) => [
        key,
        [...set].sort((a, b) => a.localeCompare(b)).join(', '),
      ]),
  );
}

function extractAvailableMetrics(
  metricsVector: PrometheusVectorResult[],
  instanceSet: Set<string>,
) {
  return [
    ...new Set(
      metricsVector
        .filter((row) => instanceSet.has(getMetricInstance(row.metric)))
        .map((row) => row.metric.__name__)
        .filter((metricName): metricName is string => Boolean(metricName)),
    ),
  ].sort((a, b) => a.localeCompare(b));
}

function extractLatestMetricValues(
  metricsVector: PrometheusVectorResult[],
  instanceSet: Set<string>,
) {
  const ignoreKeys = new Set([
    '__name__',
    'exported_instance',
    'exported_job',
    'instance',
    'job',
  ]);

  return metricsVector
    .filter((row) => instanceSet.has(getMetricInstance(row.metric)))
    .map((row) => {
      const metricName = row.metric.__name__ || 'unknown_metric';
      const labelsText = Object.entries(row.metric)
        .filter(([key, value]) => !ignoreKeys.has(key) && Boolean(value))
        .sort((a, b) => a[0].localeCompare(b[0]))
        .map(([key, value]) => `${key}=${value}`)
        .join(', ');

      return {
        labelsText: labelsText || '-',
        metricName,
        valueText: row.value?.[1] ?? '--',
      };
    })
    .sort((a, b) => {
      const nameOrder = a.metricName.localeCompare(b.metricName);
      if (nameOrder !== 0) {
        return nameOrder;
      }
      return a.labelsText.localeCompare(b.labelsText);
    });
}

function mapMonitorTargetGroup(
  group: MetricsAndAlertsGroupDto,
): MonitorTargetGroupOption {
  return {
    groupName: group.groupName,
    id: group.id,
    isEnabled: group.isEnabled,
    sortOrder: group.sortOrder,
  };
}

function mapMonitorTarget(
  target: MetricsAndAlertsMonitorTargetDto,
): MonitorTargetConfig {
  return {
    checkIntervalSeconds: target.checkIntervalSeconds ?? null,
    checkTimeoutSeconds: target.checkTimeoutSeconds ?? null,
    checkUrl: target.checkUrl ?? null,
    checkUrlName: target.checkUrlName ?? null,
    durationMinutes: target.durationMinutes ?? null,
    expectedStatusCodes: target.expectedStatusCodes || [],
    groupId: target.groupId ?? null,
    groupName: target.groupName ?? null,
    httpMethod: target.httpMethod ?? 'GET',
    id: target.id,
    isEnabled: target.isEnabled,
    minAliveCount: target.minAliveCount ?? null,
    notifyMinuteQueue: target.notifyMinuteQueue || [],
    notifyUsers: target.notifyUsers || [],
    offlineSeconds: target.offlineSeconds ?? null,
    sortOrder: target.sortOrder,
    targetKey: target.targetKey,
    targetName: target.targetName ?? null,
  };
}

async function loadMonitorTargetConfig(options?: {
  force?: boolean;
  notifyError?: boolean;
}) {
  const force = options?.force ?? false;
  const notifyError = options?.notifyError ?? true;

  if (!metricsAndAlertsConfigured) {
    monitorTargetGroups.value = [];
    monitorTargetMap.value = {};
    return {
      groupList: monitorTargetGroups.value,
      targetMap: monitorTargetMap.value,
    };
  }

  const now = Date.now();
  if (
    !force &&
    monitorTargetConfigLoadedAt.value > 0 &&
    now - monitorTargetConfigLoadedAt.value < 30_000
  ) {
    return {
      groupList: monitorTargetGroups.value,
      targetMap: monitorTargetMap.value,
    };
  }

  try {
    const [groups, targets] = await Promise.all([
      metricsAndAlertsClient.listGroups(false),
      metricsAndAlertsClient.listApplications(),
    ]);

    monitorTargetGroups.value = groups.map(mapMonitorTargetGroup);
    monitorTargetMap.value = Object.fromEntries(
      targets.map((target) => {
        const mapped = mapMonitorTarget(target);
        return [mapped.targetKey, mapped];
      }),
    );
    monitorTargetConfigLoadedAt.value = now;
    monitorTargetErrorShown = false;

    return {
      groupList: monitorTargetGroups.value,
      targetMap: monitorTargetMap.value,
    };
  } catch (error) {
    if (notifyError && !monitorTargetErrorShown) {
      ElMessage.warning(
        `加载监控分组配置失败: ${error instanceof Error ? error.message : String(error)}`,
      );
      monitorTargetErrorShown = true;
    }

    return {
      groupList: monitorTargetGroups.value,
      targetMap: monitorTargetMap.value,
    };
  }
}

async function loadAppMetrics(
  silent = false,
  forceMonitorTargetConfig = false,
) {
  if (!silent) {
    loading.value = true;
  }

  try {
    const [targetInfo, , alerts] = await Promise.all([
      client.query('target_info{type="AppMetrics"}'),
      loadMonitorTargetConfig({
        force: forceMonitorTargetConfig,
        notifyError: !silent,
      }),
      metricsAndAlertsConfigured
        ? metricsAndAlertsClient.listAlertStates({
            isActive: true,
            targetType: 1,
          })
        : Promise.resolve([] as AlertStateDto[]),
    ]);

    activeAlertStates.value = alerts;

    const groups = new Map<
      string,
      {
        instance: string;
        jobs: Set<string>;
        serviceVersion: string;
      }
    >();

    for (const row of targetInfo) {
      const job = getMetricJob(row.metric);
      const appName = getJobSuffix(job);
      if (!appName) {
        continue;
      }

      const instance = getMetricInstance(row.metric);
      if (!instance) {
        continue;
      }

      const groupKey = `${appName}@@${instance}`;
      const group = groups.get(groupKey) || {
        instance,
        jobs: new Set<string>(),
        serviceVersion: '',
      };

      if (job) {
            group.jobs.add(job);
      }
      if (!group.serviceVersion && row.metric.service_version) {
        group.serviceVersion = row.metric.service_version;
      }
      groups.set(groupKey, group);
    }

    const allInstances = [
      ...new Set(
        targetInfo.map((row) => getMetricInstance(row.metric)).filter(Boolean),
      ),
    ];
    const metricsOfInterest =
      'app_config_info|app_inflight|app_last_heartbeat_timestamp|app_start_time_seconds|app_uptime_seconds|test';

    const metricsVector =
      allInstances.length > 0
        ? await queryByInstances(
            allInstances,
            (labelName, matcher) =>
              `{${labelName}=~"${matcher}",__name__=~"${metricsOfInterest}"}`,
          )
        : [];

    const allMetricsVector =
      allInstances.length > 0
        ? await queryByInstances(
            allInstances,
            (labelName, matcher) => `{${labelName}=~"${matcher}"}`,
          )
        : [];

    const nextMap: Record<string, AppViewModel> = {};
    for (const [groupKey, group] of groups) {
      const [splitAppName] = groupKey.split('@@');
      const appName = splitAppName || 'unknown-app';
      const jobs = [...group.jobs];
      const instanceSet = new Set([group.instance]);
      const currentHeartbeat = numberFromResult(
        metricsVector,
        'app_last_heartbeat_timestamp',
        instanceSet,
      );

      nextMap[groupKey] = {
        appName,
        attributes: mergeAttributes(targetInfo, allMetricsVector, instanceSet),
        availableMetrics: extractAvailableMetrics(
          allMetricsVector,
          instanceSet,
        ),
        config: mergeConfig(metricsVector, instanceSet),
        heartbeatTimestamp:
          currentHeartbeat ??
          appMap.value[groupKey]?.heartbeatTimestamp ??
          historyAppMap.value[groupKey]?.heartbeatTimestamp ??
          null,
        instance: group.instance,
        jobDisplayName:
          jobs
            .map((job) => getJobSuffix(job))
            .filter((job): job is string => Boolean(job))
            .sort((a, b) => a.localeCompare(b))[0] || appName,
        latestMetricValues: extractLatestMetricValues(
          allMetricsVector,
          instanceSet,
        ),
        inflight: numberFromResult(metricsVector, 'app_inflight', instanceSet),
        instances: [group.instance],
        jobs,
        serviceVersion: group.serviceVersion,
        startTimeSeconds: numberFromResult(
          metricsVector,
          'app_start_time_seconds',
          instanceSet,
          'min',
        ),
        uptimeSeconds: numberFromResult(
          metricsVector,
          'app_uptime_seconds',
          instanceSet,
        ),
      };
    }

    for (const [key, previous] of Object.entries(appMap.value)) {
      if (!nextMap[key]) {
        nextMap[key] = previous;
      }
    }

    for (const [key, historical] of Object.entries(historyAppMap.value)) {
      if (!nextMap[key]) {
        nextMap[key] = historical;
      }
    }

    appMap.value = nextMap;
    refreshKey.value += 1;

    if (!selectedAppName.value || !nextMap[selectedAppName.value]) {
      const nextVisibleItems = Object.entries(nextMap)
        .filter(
          ([, item]) =>
            showHistory.value ||
            item.heartbeatTimestamp === null ||
            isRecentHeartbeat(item),
        )
        .map(([itemKey, item]) => ({
          appName: item.appName,
          environmentName: getEnvironmentName(item),
          heartbeatTimestamp: item.heartbeatTimestamp,
          hostName: getHostName(item),
          instance: item.instance,
          itemKey,
          jobName: item.jobDisplayName,
          serviceVersion: item.serviceVersion || '--',
        }))
        .sort(compareAppSummaryItem);
      selectedAppName.value = nextVisibleItems[0]?.itemKey || '';
    }
  } catch (error) {
    if (!silent) {
      ElMessage.error(
        `加载应用指标失败: ${error instanceof Error ? error.message : String(error)}`,
      );
    }
  } finally {
    if (!silent) {
      loading.value = false;
    }
  }
}

function stopPolling() {
  if (!pollingTimer) {
    return;
  }
  clearInterval(pollingTimer);
  pollingTimer = undefined;
}

function startPolling() {
  stopPolling();
  pollingTimer = window.setInterval(async () => {
    await loadAppMetrics(true);
  }, 15_000);
}

function syncPolling() {
  if (!autoRefreshEnabled.value) {
    stopPolling();
    return;
  }
  startPolling();
}

async function handleRefresh() {
  await loadAppMetrics(false, true);
}

function ensureMetricsAndAlertsReady() {
  if (metricsAndAlertsConfigured) {
    return true;
  }
  ElMessage.warning('请先在配置中设置 MetricsAndAlerts 服务地址。');
  return false;
}

function resetGroupDialog() {
  groupDialog.form.groupName = '';
  groupDialog.form.id = 0;
  groupDialog.form.isEnabled = true;
  groupDialog.form.sortOrder = 0;
}

function openCreateGroupDialog() {
  if (!ensureMetricsAndAlertsReady()) {
    return;
  }
  groupDialog.mode = 'create';
  resetGroupDialog();
  groupDialog.visible = true;
}

function openEditGroupDialog(group: MonitorTargetSidebarGroup) {
  if (!ensureMetricsAndAlertsReady() || group.isDefault) {
    return;
  }
  groupDialog.mode = 'edit';
  groupDialog.form.groupName = group.groupName;
  groupDialog.form.id = group.groupId;
  groupDialog.form.isEnabled = group.isEnabled;
  groupDialog.form.sortOrder = group.sortOrder;
  groupDialog.visible = true;
}

function splitTextValues(value: string) {
  return value
    .split(/[\n,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function openEditAppDialog(appGroup: MonitorTargetAppGroup) {
  if (!ensureMetricsAndAlertsReady()) {
    return;
  }

  const target = appGroup.appTarget;
  appDialog.form.groupId = target?.groupId ?? 0;
  appDialog.form.isEnabled = target?.isEnabled ?? true;
  appDialog.form.notifyUsersText = (target?.notifyUsers || []).join(', ');
  appDialog.form.sortOrder = target?.sortOrder ?? 0;
  appDialog.form.targetKey = appGroup.appName;
  appDialog.form.targetName = target?.targetName ?? '';
  appDialog.visible = true;
}

async function submitGroupDialog() {
  if (!groupDialog.form.groupName.trim()) {
    ElMessage.warning('请先填写分组名称。');
    return;
  }

  savingGroup.value = true;
  try {
    await metricsAndAlertsClient.upsertGroup({
      groupName: groupDialog.form.groupName.trim(),
      id: groupDialog.form.id,
      isEnabled: groupDialog.form.isEnabled,
      sortOrder: groupDialog.form.sortOrder,
    });
    groupDialog.visible = false;
    resetGroupDialog();
    await loadAppMetrics(false, true);
    ElMessage.success(
      groupDialog.mode === 'create' ? '监控分组已创建。' : '监控分组已更新。',
    );
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '保存监控分组失败。');
  } finally {
    savingGroup.value = false;
  }
}

async function submitAppDialog() {
  if (!appDialog.form.targetKey.trim()) {
    ElMessage.warning('应用标识不能为空。');
    return;
  }

  savingApp.value = true;
  try {
    await metricsAndAlertsClient.upsertMonitorTarget({
      groupId: appDialog.form.groupId > 0 ? appDialog.form.groupId : null,
      isEnabled: appDialog.form.isEnabled,
      notifyUsers: splitTextValues(appDialog.form.notifyUsersText),
      sortOrder: appDialog.form.sortOrder,
      targetKey: appDialog.form.targetKey.trim(),
      targetName: appDialog.form.targetName.trim() || null,
      targetType: 1,
    });
    appDialog.visible = false;
    await loadAppMetrics(false, true);
    ElMessage.success('应用配置已更新。');
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '保存应用配置失败。');
  } finally {
    savingApp.value = false;
  }
}

onMounted(async () => {
  await loadAppMetrics(false, true);
  syncPolling();
});

watch(autoRefreshEnabled, () => {
  syncPolling();
});

watch(
  () => [
    showHistory.value,
    appItems.value.map((item) => item.itemKey).join('|'),
  ],
  () => {
    if (
      !selectedAppName.value ||
      !displayedAppMap.value[selectedAppName.value]
    ) {
      selectedAppName.value = appItems.value[0]?.itemKey || '';
    }
  },
);

watch(
  () => showHistory.value,
  async (enabled) => {
    if (!enabled) {
      return;
    }

    await loadHistoricalApps();
    const nextMap = { ...appMap.value };
    for (const [key, historical] of Object.entries(historyAppMap.value)) {
      if (!nextMap[key]) {
        nextMap[key] = historical;
      }
    }
    appMap.value = nextMap;
  },
);

watch(
  () => selectedAppName.value,
  async (itemKey) => {
    if (!itemKey) {
      return;
    }

    const item = appMap.value[itemKey];
    if (!item) {
      return;
    }

    const shouldLoadSnapshot =
      showHistory.value &&
      (!isRecentHeartbeat(item) || item.latestMetricValues.length === 0);
    if (!shouldLoadSnapshot) {
      return;
    }

    await loadHistoricalSnapshot(itemKey);
  },
);

onUnmounted(() => {
  stopPolling();
});
</script>

<template>
  <div class="default-app-panel">
    <AppSidebar
      v-model="selectedAppName"
      v-model:show-history="showHistory"
      :groups="sidebarGroups"
      :loading="loading"
      :metrics-configured="metricsAndAlertsConfigured"
      @create-group="openCreateGroupDialog"
      @edit-app="openEditAppDialog"
      @edit-group="openEditGroupDialog"
      @refresh="handleRefresh"
    />
    <div class="right-panel">
      <AppBox
        v-model:auto-refresh-enabled="autoRefreshEnabled"
        :app="selectedApp"
        :refresh-key="refreshKey"
        :refreshing="loading"
        :target-config="selectedAppTargetConfig"
        @refresh="handleRefresh"
      />
    </div>

    <el-dialog
      v-model="groupDialog.visible"
      :title="groupDialog.mode === 'create' ? '新增监控分组' : '编辑监控分组'"
      width="520px"
    >
      <el-form label-width="96px">
        <el-form-item label="分组名称">
          <el-input
            v-model="groupDialog.form.groupName"
            placeholder="例如：核心业务"
          />
        </el-form-item>
        <el-form-item label="排序值">
          <el-input-number
            v-model="groupDialog.form.sortOrder"
            :step="1"
            controls-position="right"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch
            v-model="groupDialog.form.isEnabled"
            active-text="启用"
            inactive-text="禁用"
            inline-prompt
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="groupDialog.visible = false">关闭</el-button>
        <el-button :loading="savingGroup" type="primary" @click="submitGroupDialog">
          {{ groupDialog.mode === 'create' ? '创建' : '保存' }}
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="appDialog.visible"
      title="编辑应用配置"
      width="560px"
    >
      <el-form label-width="96px">
        <el-form-item label="应用标识">
          <el-input v-model="appDialog.form.targetKey" disabled />
        </el-form-item>
        <el-form-item label="显示名称">
          <el-input
            v-model="appDialog.form.targetName"
            placeholder="可选，用于告警或展示"
          />
        </el-form-item>
        <el-form-item label="监控分组">
          <el-select v-model="appDialog.form.groupId" style="width: 100%">
            <el-option
              v-for="group in availableMonitorTargetGroups"
              :key="group.id"
              :label="group.groupName"
              :value="group.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="排序值">
          <el-input-number
            v-model="appDialog.form.sortOrder"
            :step="1"
            controls-position="right"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="通知用户">
          <el-input
            v-model="appDialog.form.notifyUsersText"
            :rows="3"
            placeholder="多个 userid 用逗号分隔"
            type="textarea"
          />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch
            v-model="appDialog.form.isEnabled"
            active-text="启用"
            inactive-text="禁用"
            inline-prompt
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="appDialog.visible = false">关闭</el-button>
        <el-button :loading="savingApp" type="primary" @click="submitAppDialog">
          保存
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.default-app-panel {
  display: flex;
  height: calc(100vh - 170px);
  min-height: 520px;
}

.right-panel {
  flex: 1;
  min-width: 0;
}

@media (max-width: 992px) {
  .default-app-panel {
    flex-direction: column;
    height: auto;
  }

  .right-panel {
    min-height: 480px;
  }
}
</style>
