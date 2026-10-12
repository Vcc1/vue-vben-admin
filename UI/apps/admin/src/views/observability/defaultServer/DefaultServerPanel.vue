<script lang="ts" setup>
import type {
  DetailMetricsRaw,
  DetailTab,
  MetricNavItem,
  MetricNavItemType,
  RangeKey,
  ServerApplicationItem,
} from './types';

import type {
  DiskUsageInfo,
  PrometheusServerCard,
  PrometheusValue,
  PrometheusVectorResult,
} from '#/api/prometheus/types';

import { computed, onMounted, onUnmounted, ref, watch } from 'vue';

import { createIconifyIcon } from '@vben/icons';

import { ElMessage } from 'element-plus';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import ServerBasicInfoPanel from './ServerBasicInfoPanel.vue';
import ServerPerformancePanel from './ServerPerformancePanel.vue';

defineOptions({ name: 'DefaultServerPanel' });

const WindowsIcon = createIconifyIcon('ri:windows-fill');
const DockerIcon = createIconifyIcon('mdi:docker');
const LinuxIcon = createIconifyIcon('mdi:linux');
const KubernetesIcon = createIconifyIcon('mdi:kubernetes');
const ServerIcon = createIconifyIcon('lucide:server');

const DISK_LABEL_KEYS = ['disk', 'device', 'volume', 'mountpoint'];
const INTERFACE_LABEL_KEYS = ['interface', 'nic', 'device'];
const OFFLINE_LAST_VALUE_LOOKBACK = '24h';
const OFFLINE_THRESHOLD_SECONDS = 120;

interface ServerMetricScope {
  hostLabel:
    | 'host.name'
    | 'host_name'
    | 'net.host.name'
    | 'server.address'
    | null;
  hostValue: null | string;
  instanceLabel: 'exported_instance' | 'instance';
  instanceValue: string;
  jobLabel: 'exported_job' | 'job' | null;
  jobValue: null | string;
}

const rangeOptions: Array<{ label: string; value: RangeKey }> = [
  { label: '最近 15m', value: '15m' },
  { label: '最近 1h', value: '1h' },
  { label: '最近 6h', value: '6h' },
  { label: '最近 24h', value: '24h' },
];

const rangeStepMap: Record<RangeKey, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

const client = new PrometheusClient();

const searchText = ref('');
const servers = ref<string[]>([]);
const serverDisplayNames = ref<Record<string, string>>({});
const serverScopes = ref<Record<string, ServerMetricScope>>({});
const serverCards = ref<Record<string, PrometheusServerCard>>({});
const serverOfflineMap = ref<Record<string, boolean>>({});
const listLoading = ref(false);
const listError = ref('');

const selectedServer = ref('');
const selectedRange = ref<RangeKey>('1h');
const detailLoading = ref(false);
const detailError = ref('');

const selectedDisk = ref('C:');
const diskOptions = ref<string[]>([]);
const selectedInterface = ref('');
const interfaceOptions = ref<string[]>([]);
const activeMetricKey = ref('cpu');
const detailTab = ref<DetailTab>('basic');
const serverApplications = ref<ServerApplicationItem[]>([]);
const detailRaw = ref<DetailMetricsRaw | null>(null);

function getServerDisplayName(server: string) {
  return serverDisplayNames.value[server] || server;
}

const filteredServers = computed(() => {
  const keyword = searchText.value.trim().toLowerCase();
  if (!keyword) {
    return servers.value;
  }
  return servers.value.filter((server) =>
    getServerDisplayName(server).toLowerCase().includes(keyword),
  );
});

const selectedServerDisplayName = computed(() =>
  getServerDisplayName(selectedServer.value),
);

const metricNavItems = computed<MetricNavItem[]>(() => {
  const items: Array<{ key: string; label: string; type: MetricNavItemType }> =
    [
      { key: 'cpu', label: 'CPU', type: 'cpu' },
      { key: 'memory', label: '内存', type: 'memory' },
    ];

  for (const disk of diskOptions.value) {
    items.push({ key: `disk:${disk}`, label: `磁盘 ${disk}`, type: 'disk' });
  }

  for (const networkInterface of interfaceOptions.value) {
    items.push({
      key: `network:${networkInterface}`,
      label: `网卡 ${networkInterface}`,
      type: 'network',
    });
  }

  return items;
});

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', String.raw`\"`);
}

function isPromqlSafeLabelName(label: string) {
  return /^[a-zA-Z_][a-zA-Z0-9_]*$/.test(label);
}

function buildServerSelector(server: string) {
  const scope = serverScopes.value[server];
  const matchers: string[] = [];

  if (scope) {
    if (
      scope.hostLabel &&
      scope.hostValue &&
      isPromqlSafeLabelName(scope.hostLabel)
    ) {
      matchers.push(
        `${scope.hostLabel}="${escapeLabelValue(scope.hostValue)}"`,
      );
    } else {
      matchers.push(
        `${scope.instanceLabel}="${escapeLabelValue(scope.instanceValue)}"`,
      );
    }
    if (scope.jobLabel && scope.jobValue) {
      matchers.push(`${scope.jobLabel}="${escapeLabelValue(scope.jobValue)}"`);
    }
  } else {
    matchers.push(`exported_instance="${escapeLabelValue(server)}"`);
  }

  return `{${matchers.join(',')}}`;
}

function buildServerScope(
  metric: Record<string, string>,
): null | ServerMetricScope {
  const exportedInstance = metric.exported_instance?.trim();
  const instance = metric.instance?.trim();
  const exportedJob = metric.exported_job?.trim();
  const job = metric.job?.trim();
  const hostEntries = [
    ['host_name', metric.host_name?.trim()],
    ['host.name', metric['host.name']?.trim()],
    ['net.host.name', metric['net.host.name']?.trim()],
    [
      'server.address',
      metric['server.address']?.trim() || metric.server_address?.trim(),
    ],
  ] as const;
  const hostEntry = hostEntries.find(([, value]) =>
    Boolean(value && !value.toLowerCase().includes('otel-collector')),
  );

  if (exportedInstance) {
    return {
      hostLabel: (hostEntry?.[0] as ServerMetricScope['hostLabel']) || null,
      hostValue: hostEntry?.[1] || null,
      instanceLabel: 'exported_instance',
      instanceValue: exportedInstance,
      jobLabel: exportedJob ? 'exported_job' : (job ? 'job' : null),
      jobValue: exportedJob || job || null,
    };
  }

  if (instance) {
    return {
      hostLabel: (hostEntry?.[0] as ServerMetricScope['hostLabel']) || null,
      hostValue: hostEntry?.[1] || null,
      instanceLabel: 'instance',
      instanceValue: instance,
      jobLabel: exportedJob ? 'exported_job' : (job ? 'job' : null),
      jobValue: exportedJob || job || null,
    };
  }

  return null;
}

function getScopeDisplayName(
  metric: Record<string, string>,
  scope: ServerMetricScope,
) {
  const candidates = [
    metric.host_name,
    metric['host.name'],
    metric.server_address,
    metric['server.address'],
    metric['net.host.name'],
    metric.exported_instance,
    metric.instance,
  ].filter(Boolean) as string[];

  const readable = candidates.find((value) => {
    const normalized = value.trim().toLowerCase();
    return normalized && !normalized.includes('otel-collector');
  });

  return readable || scope.instanceValue;
}

function parseAppName(job: string) {
  if (!job) {
    return '';
  }
  const parts = job.split('/');
  return parts.at(-1)?.trim() || job;
}

function normalizeHostValue(value: string) {
  return value.trim().toLowerCase();
}

function extractHostCandidates(value: string) {
  const normalized = normalizeHostValue(value);
  if (!normalized) {
    return [] as string[];
  }

  const results = new Set<string>([normalized]);
  const noProtocol = normalized.replace(/^https?:\/\//, '');
  results.add(noProtocol);

  const hostPart = noProtocol.split('/')[0] || noProtocol;
  results.add(hostPart);

  const colonIndex = hostPart.lastIndexOf(':');
  if (colonIndex > 0) {
    results.add(hostPart.slice(0, colonIndex));
  }

  return [...results].filter(Boolean);
}

function metricMatchesServer(metric: Record<string, string>, server: string) {
  const serverCandidates = new Set(extractHostCandidates(server));
  const metricValues = [
    metric.exported_instance,
    metric.instance,
    metric.host_name,
    metric['host.name'],
    metric.server_address,
    metric['server.address'],
    metric['net.host.name'],
    metric['net.sock.peer.addr'],
  ].filter(Boolean) as string[];

  return metricValues.some((value) =>
    extractHostCandidates(value).some((candidate) =>
      serverCandidates.has(candidate),
    ),
  );
}

function buildLastValueQuery(expr: string) {
  return `last_over_time(${expr}[${OFFLINE_LAST_VALUE_LOOKBACK}])`;
}

function buildLastRateValueQuery(expr: string) {
  return `last_over_time(rate(${expr}[1m])[${OFFLINE_LAST_VALUE_LOOKBACK}:1m])`;
}

function buildLastTimestampQuery(expr: string) {
  return `max_over_time(timestamp(${expr})[${OFFLINE_LAST_VALUE_LOOKBACK}:1m])`;
}

function parseNumber(value?: null | PrometheusValue) {
  if (!value) {
    return null;
  }
  const num = Number(value[1]);
  return Number.isFinite(num) ? num : null;
}

function getVectorValue(results: PrometheusVectorResult[]) {
  if (results.length === 0) {
    return { ts: null, value: null as null | number };
  }
  const sorted = [...results].sort((a, b) => b.value[0] - a.value[0]);
  return {
    ts: sorted[0]?.value?.[0] ?? null,
    value: parseNumber(sorted[0]?.value),
  };
}

function formatPercent(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return `${value.toFixed(2)}%`;
}

function formatTimestamp(timestamp: null | number) {
  if (!timestamp) {
    return '--';
  }
  return new Date(timestamp * 1000).toLocaleString();
}

function getLabel(metric: Record<string, string>, keys: string[]) {
  for (const key of keys) {
    if (metric[key]) {
      return metric[key];
    }
  }
  return 'unknown';
}

function normalizeSystemType(value: string) {
  const text = value.trim().toLowerCase();
  if (!text) {
    return null;
  }
  if (text.includes('opentelemetry') || text.includes('otel')) {
    return null;
  }
  if (text.includes('docker') || text.includes('container')) {
    return 'Docker';
  }
  if (text.includes('windows') || text.includes('win')) {
    return 'Windows';
  }
  if (
    text.includes('linux') ||
    text.includes('ubuntu') ||
    text.includes('debian') ||
    text.includes('centos')
  ) {
    return 'Linux';
  }
  if (text.includes('kubernetes') || text.includes('k8s')) {
    return 'Kubernetes';
  }
  return value;
}

function inferSystemType(
  server: string,
  metric?: Record<string, string>,
  diskNames: string[] = [],
) {
  const candidateValues = [
    metric?.os_type,
    metric?.os_name,
    metric?.['os.name'],
    metric?.host_os,
    metric?.host_type,
    metric?.platform,
  ].filter(Boolean) as string[];

  for (const value of candidateValues) {
    const normalized = normalizeSystemType(value);
    if (normalized) {
      return normalized;
    }
  }

  const serverText = server.toLowerCase();
  if (serverText.includes('docker')) {
    return 'Docker';
  }
  if (serverText.includes('win')) {
    return 'Windows';
  }
  if (serverText.includes('linux')) {
    return 'Linux';
  }

  if (diskNames.some((name) => /^[a-z]:$/i.test(name))) {
    return 'Windows';
  }
  if (diskNames.some((name) => name.startsWith('/'))) {
    return 'Linux';
  }

  return '--';
}

function getSystemTypeIcon(systemType?: null | string) {
  switch (systemType) {
    case 'Docker': {
      return DockerIcon;
    }
    case 'Kubernetes': {
      return KubernetesIcon;
    }
    case 'Linux': {
      return LinuxIcon;
    }
    case 'Windows': {
      return WindowsIcon;
    }
    default: {
      return ServerIcon;
    }
  }
}

function getSystemTypeClass(systemType?: null | string) {
  switch (systemType) {
    case 'Docker': {
      return 'is-docker';
    }
    case 'Kubernetes': {
      return 'is-kubernetes';
    }
    case 'Linux': {
      return 'is-linux';
    }
    case 'Windows': {
      return 'is-windows';
    }
    default: {
      return 'is-default';
    }
  }
}
function isServerOffline(server: string) {
  const offline = serverOfflineMap.value[server];
  if (typeof offline === 'boolean') {
    return offline;
  }
  const updatedAt = serverCards.value[server]?.updatedAt;
  if (!updatedAt) {
    return true;
  }
  return Math.floor(Date.now() / 1000) - updatedAt > OFFLINE_THRESHOLD_SECONDS;
}

function getServerLastReportAt(server: string) {
  const disks = serverCards.value[server]?.diskUsage ?? [];
  const timestamps = disks
    .map((disk) => disk.lastReportedAt)
    .filter(
      (timestamp): timestamp is number =>
        typeof timestamp === 'number' && Number.isFinite(timestamp),
    );
  if (timestamps.length === 0) {
    return null;
  }
  return Math.max(...timestamps);
}

function getRangeParams(range: RangeKey) {
  const end = Math.floor(Date.now() / 1000);
  const rangeSecondsMap: Record<RangeKey, number> = {
    '15m': 15 * 60,
    '1h': 60 * 60,
    '6h': 6 * 60 * 60,
    '24h': 24 * 60 * 60,
  };

  return {
    end,
    start: end - rangeSecondsMap[range],
    step: rangeStepMap[range],
  };
}

async function loadServerCards() {
  const targetInfoRows = await client
    .query('target_info')
    .catch(() => [] as PrometheusVectorResult[]);

  const rows = await Promise.all(
    servers.value.map(async (server) => {
      const selector = buildServerSelector(server);
      const [
        up,
        cpu,
        memUsed,
        memTotal,
        currentDiskTotalVec,
        diskUsedVec,
        diskTotalVec,
        diskLastReportTsVec,
        diskReadVec,
        diskWriteVec,
      ] = await Promise.all([
        client.query(`up${selector}`),
        client.query(
          buildLastValueQuery(`system_cpu_usage_percent${selector}`),
        ),
        client.query(
          buildLastValueQuery(`system_memory_used_bytes${selector}`),
        ),
        client.query(
          buildLastValueQuery(`system_memory_total_bytes${selector}`),
        ),
        client.query(`system_disk_total_bytes${selector}`),
        client.query(buildLastValueQuery(`system_disk_used_bytes${selector}`)),
        client.query(buildLastValueQuery(`system_disk_total_bytes${selector}`)),
        client.query(
          buildLastTimestampQuery(`system_disk_total_bytes${selector}`),
        ),
        client.query(
          buildLastRateValueQuery(`system_disk_io_read_bytes_total${selector}`),
        ),
        client.query(
          buildLastRateValueQuery(
            `system_disk_io_write_bytes_total${selector}`,
          ),
        ),
      ]);

      const upMetric = getVectorValue(up);
      const hasCurrentDiskMetrics = currentDiskTotalVec.length > 0;
      const isOffline =
        (upMetric.value === null || upMetric.value < 1) &&
        !hasCurrentDiskMetrics;
      const cpuMetric = getVectorValue(cpu);
      const usedMetric = getVectorValue(memUsed);
      const totalMetric = getVectorValue(memTotal);
      const activeDiskNames = new Set(
        currentDiskTotalVec
          .map((item) => getLabel(item.metric, DISK_LABEL_KEYS))
          .filter((name) => name !== 'unknown'),
      );

      const memoryUsagePercent =
        usedMetric.value !== null && totalMetric.value && totalMetric.value > 0
          ? (usedMetric.value / totalMetric.value) * 100
          : null;
      const serverUpdatedAt = cpuMetric.ts || usedMetric.ts || totalMetric.ts;

      const diskUsage: DiskUsageInfo[] = diskUsedVec
        .flatMap((usedItem) => {
          const label = getLabel(usedItem.metric, DISK_LABEL_KEYS);
          if (!isOffline && !activeDiskNames.has(label)) {
            return [];
          }

          const totalItem = diskTotalVec.find(
            (t) => getLabel(t.metric, DISK_LABEL_KEYS) === label,
          );
          if (!totalItem) {
            return [];
          }

          const usedVal = parseNumber(usedItem.value);
          const totalVal = parseNumber(totalItem.value);
          const readItem = diskReadVec.find(
            (item) => getLabel(item.metric, DISK_LABEL_KEYS) === label,
          );
          const writeItem = diskWriteVec.find(
            (item) => getLabel(item.metric, DISK_LABEL_KEYS) === label,
          );
          const lastReportItem = diskLastReportTsVec.find(
            (item) => getLabel(item.metric, DISK_LABEL_KEYS) === label,
          );

          if (usedVal === null || totalVal === null || totalVal === 0) {
            return [];
          }
          return [
            {
              fsType: totalItem.metric.fstype || totalItem.metric.type || null,
              lastReportedAt: parseNumber(lastReportItem?.value),
              name: label,
              readBytesPerSec: parseNumber(readItem?.value),
              total: totalVal,
              usagePercent: (usedVal / totalVal) * 100,
              used: usedVal,
              writeBytesPerSec: parseNumber(writeItem?.value),
            },
          ];
        })
        .filter((disk) => {
          if (!serverUpdatedAt || !disk.lastReportedAt) {
            return true;
          }
          return disk.lastReportedAt >= serverUpdatedAt - 60;
        })
        .sort((a, b) => a.name.localeCompare(b.name));

      const serverMatchName = getServerDisplayName(server);
      const targetMetric = targetInfoRows.find((row) =>
        metricMatchesServer(row.metric, serverMatchName),
      )?.metric;
      const systemType = inferSystemType(
        server,
        targetMetric,
        diskUsage.map((disk) => disk.name),
      );

      const card: PrometheusServerCard = {
        cpuUsagePercent: cpuMetric.value,
        diskUsage,
        memoryUsagePercent,
        server,
        systemType,
        updatedAt: serverUpdatedAt,
      };

      return {
        card,
        offline: isOffline,
        server,
      };
    }),
  );

  serverCards.value = Object.fromEntries(
    rows.map((row) => [row.server, row.card]),
  );
  serverOfflineMap.value = Object.fromEntries(
    rows.map((row) => [row.server, row.offline]),
  );
}

async function loadServers(silent = false) {
  if (!silent) {
    listLoading.value = true;
  }
  listError.value = '';

  try {
    const now = Math.floor(Date.now() / 1000);
    const series = await client.series(
      ['system_cpu_usage_percent'],
      now - 24 * 60 * 60,
      now,
    );
    const scopeMap = new Map<string, ServerMetricScope>();
    const displayNameMap = new Map<string, string>();

    for (const metric of series) {
      const scope = buildServerScope(metric);
      if (!scope) {
        continue;
      }

      const displayName = getScopeDisplayName(metric, scope);
      const serverKey =
        scopeMap.has(displayName) &&
        scopeMap.get(displayName)?.instanceValue !== scope.instanceValue
          ? `${displayName} (${scope.instanceValue})`
          : displayName;

      if (!scopeMap.has(serverKey)) {
        scopeMap.set(serverKey, scope);
      }

      if (!displayNameMap.has(serverKey)) {
        displayNameMap.set(serverKey, displayName);
      }
    }

    if (scopeMap.size === 0) {
      const [exportedInstances, instances] = await Promise.all([
        client
          .labelValues('exported_instance', ['system_cpu_usage_percent'])
          .catch(() => []),
        client
          .labelValues('instance', ['system_cpu_usage_percent'])
          .catch(() => []),
      ]);

      for (const value of exportedInstances.filter(Boolean)) {
        scopeMap.set(value, {
          hostLabel: null,
          hostValue: null,
          instanceLabel: 'exported_instance',
          instanceValue: value,
          jobLabel: null,
          jobValue: null,
        });
        if (!displayNameMap.has(value)) {
          displayNameMap.set(value, value);
        }
      }

      for (const value of instances.filter(Boolean)) {
        if (!scopeMap.has(value)) {
          scopeMap.set(value, {
            hostLabel: null,
            hostValue: null,
            instanceLabel: 'instance',
            instanceValue: value,
            jobLabel: null,
            jobValue: null,
          });
        }
        if (!displayNameMap.has(value)) {
          displayNameMap.set(value, value);
        }
      }
    }

    if (scopeMap.size === 0) {
      throw new Error(
        '未发现 system_cpu_usage_percent 序列，请检查 Prometheus 是否已采集服务器指标。',
      );
    }

    const deduped = [...scopeMap.keys()]
      .filter(Boolean)
      .sort((a, b) => a.localeCompare(b));
    servers.value = deduped;
    serverDisplayNames.value = Object.fromEntries(displayNameMap.entries());
    serverScopes.value = Object.fromEntries(scopeMap.entries());
    if (!selectedServer.value || !deduped.includes(selectedServer.value)) {
      selectedServer.value = deduped[0] || '';
    }
    await loadServerCards();
  } catch (error) {
    listError.value = error instanceof Error ? error.message : String(error);
    if (!silent) {
      ElMessage.error(`加载服务器列表失败: ${listError.value}`);
    }
  } finally {
    if (!silent) {
      listLoading.value = false;
    }
  }
}

async function loadDetailMetrics(silent = false) {
  if (!selectedServer.value) {
    detailRaw.value = null;
    return;
  }

  if (!silent) {
    detailLoading.value = true;
  }
  detailError.value = '';

  try {
    const selector = buildServerSelector(selectedServer.value);
    const { end, start, step } = getRangeParams(selectedRange.value);

    const [
      cpuTotal,
      cpuCore,
      memoryUsed,
      memoryAvailable,
      diskUsed,
      diskTotal,
      diskIoRead,
      diskIoWrite,
      networkReceive,
      networkTransmit,
      appTargetInfo,
    ] = await Promise.all([
      client.queryRange({
        end,
        query: `system_cpu_usage_percent${selector}`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `system_cpu_core_usage_percent${selector}`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `system_memory_used_bytes${selector}`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `system_memory_available_bytes${selector}`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `system_disk_used_bytes${selector}`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `system_disk_total_bytes${selector}`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `rate(system_disk_io_read_bytes_total${selector}[1m])`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `rate(system_disk_io_write_bytes_total${selector}[1m])`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `rate(system_network_receive_bytes_total${selector}[1m])`,
        start,
        step,
      }),
      client.queryRange({
        end,
        query: `rate(system_network_transmit_bytes_total${selector}[1m])`,
        start,
        step,
      }),
      client.query('target_info{type="AppMetrics"}'),
    ]);

    detailRaw.value = {
      cpuCore,
      cpuTotal,
      diskIoRead,
      diskIoWrite,
      diskTotal,
      diskUsed,
      memoryAvailable,
      memoryUsed,
      networkReceive,
      networkTransmit,
    };

    const disks = [
      ...new Set(
        [...diskUsed, ...diskTotal, ...diskIoRead, ...diskIoWrite]
          .map((item) => getLabel(item.metric, DISK_LABEL_KEYS))
          .filter((value) => value !== 'unknown'),
      ),
    ].sort((a, b) => a.localeCompare(b));

    diskOptions.value = disks;
    if (!diskOptions.value.includes(selectedDisk.value)) {
      selectedDisk.value = diskOptions.value[0] || 'C:';
    }

    const interfaces = [
      ...new Set(
        [...networkReceive, ...networkTransmit]
          .map((item) => getLabel(item.metric, INTERFACE_LABEL_KEYS))
          .filter((value) => value !== 'unknown'),
      ),
    ].sort((a, b) => a.localeCompare(b));

    interfaceOptions.value = interfaces;
    if (!interfaceOptions.value.includes(selectedInterface.value)) {
      selectedInterface.value = interfaceOptions.value[0] || '';
    }

    const appMap = new Map<string, ServerApplicationItem>();
    for (const row of appTargetInfo) {
      const metric = row.metric;
      if (!metricMatchesServer(metric, selectedServerDisplayName.value)) {
        continue;
      }

      const jobName = metric.exported_job || metric.job || '';
      const appName = parseAppName(jobName);
      if (!appName) {
        continue;
      }

      const environment =
        metric.deployment_environment ||
        metric.environment ||
        metric.env ||
        '未设置';
      const instance = metric.exported_instance || metric.instance || '--';
      const key = `${appName}@@${environment}@@${instance}`;
      appMap.set(key, { appName, environment, instance });
    }

    serverApplications.value = [...appMap.values()].sort((a, b) => {
      const appOrder = a.appName.localeCompare(b.appName);
      if (appOrder !== 0) {
        return appOrder;
      }
      const envOrder = a.environment.localeCompare(b.environment);
      if (envOrder !== 0) {
        return envOrder;
      }
      return a.instance.localeCompare(b.instance);
    });

    if (activeMetricKey.value.startsWith('disk:') && selectedDisk.value) {
      activeMetricKey.value = `disk:${selectedDisk.value}`;
    }

    if (
      activeMetricKey.value.startsWith('network:') &&
      selectedInterface.value
    ) {
      activeMetricKey.value = `network:${selectedInterface.value}`;
    }
  } catch (error) {
    detailError.value = error instanceof Error ? error.message : String(error);
    if (!silent) {
      ElMessage.error(`加载服务器指标失败: ${detailError.value}`);
    }
  } finally {
    if (!silent) {
      detailLoading.value = false;
    }
  }
}

function selectMetric(metricKey: string) {
  activeMetricKey.value = metricKey;

  if (metricKey === 'cpu') {
    return;
  }

  if (metricKey.startsWith('disk:')) {
    selectedDisk.value = metricKey.slice(5);
    return;
  }

  if (metricKey.startsWith('network:')) {
    selectedInterface.value = metricKey.slice(8);
  }
}

function selectServer(server: string) {
  selectedServer.value = server;
  activeMetricKey.value = 'cpu';
  detailTab.value = 'performance';
}

watch(
  () => [selectedServer.value, selectedRange.value],
  async ([server]) => {
    if (!server) {
      return;
    }
    await loadDetailMetrics();
  },
  { immediate: false },
);

let pollingInterval: number | undefined;

onMounted(async () => {
  await loadServers();
  if (selectedServer.value) {
    await loadDetailMetrics();
  }

  pollingInterval = window.setInterval(async () => {
    await loadServers(true);
    if (selectedServer.value) {
      await loadDetailMetrics(true);
    }
  }, 15_000);
});

onUnmounted(() => {
  if (pollingInterval) {
    clearInterval(pollingInterval);
  }
});
</script>

<template>
  <div class="default-server-panel">
    <div class="server-list-panel">
      <div class="panel-title">服务器监测</div>
      <el-input v-model="searchText" clearable placeholder="按服务器名过滤" />

      <el-alert
        v-if="listError"
        :closable="false"
        :title="listError"
        class="mt-3"
        type="error"
      />

      <div v-loading="listLoading" class="server-list">
        <el-empty
          v-if="filteredServers.length === 0 && !listLoading"
          description="暂无服务器"
        />
        <el-card
          v-for="server in filteredServers"
          :key="server"
          :class="{ 'is-selected': selectedServer === server }"
          class="server-card"
          shadow="hover"
          @click="selectServer(server)"
        >
          <template #header>
            <div class="server-header">
              <div class="server-title-wrap">
                <div class="server-title" :title="getServerDisplayName(server)">
                  {{ getServerDisplayName(server) }}
                </div>
                <div
                  class="server-system-type"
                  :title="serverCards[server]?.systemType || '--'"
                >
                  <component
                    :is="getSystemTypeIcon(serverCards[server]?.systemType)"
                    class="server-system-type-icon"
                    :class="[
                      getSystemTypeClass(serverCards[server]?.systemType),
                    ]"
                  />
                </div>
                <span
                  v-if="isServerOffline(server)"
                  class="server-offline-icon"
                  title="离线"
                >
                  <svg
                    class="size-3.5"
                    fill="none"
                    stroke="currentColor"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    viewBox="0 0 24 24"
                    xmlns="http://www.w3.org/2000/svg"
                  >
                    <path d="M12 9v4" />
                    <path d="M12 17h.01" />
                    <path
                      d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"
                    />
                  </svg>
                </span>
              </div>
            </div>
          </template>
          <div class="metric-row metric-row-dual">
            <div class="metric-inline-item">
              <span>CPU</span>
              <span>{{
                formatPercent(serverCards[server]?.cpuUsagePercent ?? null)
              }}</span>
            </div>
            <div class="metric-inline-item">
              <span>内存</span>
              <span>{{
                formatPercent(serverCards[server]?.memoryUsagePercent ?? null)
              }}</span>
            </div>
          </div>

          <div class="metric-row mt-2">
            <span>最后上报</span>
            <span>{{ formatTimestamp(getServerLastReportAt(server)) }}</span>
          </div>
          <div class="metric-row">
            <span>最后更新</span>
            <span>{{
              formatTimestamp(serverCards[server]?.updatedAt ?? null)
            }}</span>
          </div>
        </el-card>
      </div>
    </div>

    <div class="server-detail-panel">
      <el-empty v-if="!selectedServer" description="请先选择服务器" />
      <template v-else>
        <div class="detail-header">
          <div class="detail-title-row">
            <div
              class="detail-title"
              :title="`服务器指标 - ${selectedServerDisplayName}`"
            >
              服务器指标 - {{ selectedServerDisplayName }}
            </div>
            <el-radio-group v-model="detailTab" size="small">
              <el-radio-button label="basic">基础信息</el-radio-button>
              <el-radio-button label="performance">性能</el-radio-button>
            </el-radio-group>
          </div>
          <div class="drawer-toolbar">
            <el-radio-group v-model="selectedRange" size="small">
              <el-radio-button
                v-for="option in rangeOptions"
                :key="option.value"
                :label="option.value"
              >
                {{ option.label }}
              </el-radio-button>
            </el-radio-group>
          </div>
        </div>

        <el-alert
          v-if="detailError"
          :closable="false"
          :title="detailError"
          class="mt-3"
          type="error"
        />

        <ServerPerformancePanel
          v-if="detailTab === 'performance'"
          :active-metric-key="activeMetricKey"
          :detail-loading="detailLoading"
          :detail-raw="detailRaw"
          :metric-nav-items="metricNavItems"
          :selected-disk="selectedDisk"
          :selected-interface="selectedInterface"
          :server-card="serverCards[selectedServer] || null"
          @select-metric="selectMetric"
        />

        <ServerBasicInfoPanel
          v-else
          :server-applications="serverApplications"
          :server-card="serverCards[selectedServer] || null"
        />
      </template>
    </div>
  </div>
</template>

<style scoped>
.default-server-panel {
  display: flex;
  height: 100%;
}

.panel-title {
  margin-bottom: 12px;
  font-size: 16px;
  font-weight: 600;
}

.server-list-panel {
  width: 360px;
  padding: 12px;
  border-right: 1px solid var(--el-border-color);
}

.server-list {
  height: calc(100% - 60px);
  margin-top: 12px;
  overflow: auto;
}

.server-card {
  margin-bottom: 10px;
  cursor: pointer;
}

.server-card :deep(.el-card__header) {
  padding: 12px 18px;
}

.server-card :deep(.el-card__body) {
  padding: 12px 20px;
}

.server-card.is-selected {
  border-color: var(--el-color-primary);
}

.server-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.server-title-wrap {
  display: flex;
  flex: 1;
  gap: 6px;
  align-items: flex-end;
  width: 100%;
  min-width: 0;
}

.server-title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-weight: 600;
  white-space: nowrap;
}

.server-system-type {
  align-self: flex-end;
  padding-bottom: 1px;
  margin-bottom: 0;
  margin-left: auto;
  font-size: 12px;
  line-height: 1;
  color: var(--el-text-color-secondary);
  text-align: right;
  white-space: nowrap;
}

.server-system-type-icon {
  display: block;
  font-size: 14px;
}

.server-system-type-icon.is-windows {
  color: #1890ff;
}

.server-system-type-icon.is-docker {
  color: #2496ed;
}

.server-system-type-icon.is-linux {
  color: #f59e0b;
}

.server-system-type-icon.is-kubernetes {
  color: #326ce5;
}

.server-system-type-icon.is-default {
  color: #8b5cf6;
}

.server-offline-icon {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  color: var(--el-color-danger);
}

.metric-row {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  line-height: 22px;
}

.metric-row-dual {
  gap: 12px;
}

.metric-inline-item {
  display: flex;
  flex: 1;
  align-items: center;
  justify-content: space-between;
  min-width: 0;
}

.server-detail-panel {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
  padding: 12px;
}

.detail-header {
  padding: 12px;
  margin-bottom: 12px;
  background: var(--el-bg-color);
}

.detail-title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 16px;
  font-weight: 600;
  white-space: nowrap;
}

.detail-title-row {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.drawer-toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

@media (max-width: 1200px) {
  .default-server-panel {
    flex-direction: column;
  }

  .server-list-panel {
    width: 100%;
    border-right: none;
    border-bottom: 1px solid var(--el-border-color);
  }
}
</style>
