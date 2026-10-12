import type { AppLatestMetricValue, AppViewModel } from './types';

export interface LatestMetricGroup {
  items: AppLatestMetricValue[];
  scope: string;
}

export function formatSeconds(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  const seconds = Math.max(0, Math.floor(value));
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return `${h}h ${m}m ${s}s`;
}

export function formatHeartbeat(ts: null | number) {
  if (!ts) {
    return '--';
  }
  return new Date(ts * 1000).toLocaleString();
}

export function getAppDisplayName(appName: string) {
  if (appName === 'RoomMatching') {
    return '房型匹配';
  }
  if (appName === 'RoomMatchingStatistics') {
    return '房型映射统计';
  }
  if (appName === 'OTARoomBuilder') {
    return 'OTA 建房';
  }
  return appName;
}

export function getAppMetricsValues(app: AppViewModel | null) {
  if (!app) {
    return [];
  }
  return app.latestMetricValues.filter((item) =>
    item.labelsText.includes('otel_scope_name=AppMetrics'),
  );
}

export function getLatestMetricGroups(
  app: AppViewModel | null,
): LatestMetricGroup[] {
  if (!app) {
    return [];
  }

  const groupMap = new Map<string, AppLatestMetricValue[]>();
  for (const item of app.latestMetricValues) {
    const match = item.labelsText.match(/(?:^|,\s*)otel_scope_name=([^,]+)/);
    const scope = item.metricName === 'target_info' ? '资源' : match?.[1] || 'unknown';
    const current = groupMap.get(scope) || [];
    current.push(item);
    groupMap.set(scope, current);
  }

  return [...groupMap.entries()]
    .sort((a, b) => {
      if (a[0] === '资源' && b[0] !== '资源') {
        return -1;
      }
      if (a[0] !== '资源' && b[0] === '资源') {
        return 1;
      }
      return a[0].localeCompare(b[0]);
    })
    .map(([scope, items]) => ({
      items,
      scope,
    }));
}

export function getRuntimeQueryTime(app: AppViewModel | null) {
  const heartbeat = app?.heartbeatTimestamp;
  if (!heartbeat) {
    return null;
  }
  const nowSeconds = Math.floor(Date.now() / 1000);
  return nowSeconds - heartbeat > 300 ? heartbeat : null;
}

export function getRuntimeTargetLabels(app: AppViewModel | null) {
  if (!app) {
    return {} as Record<string, string>;
  }

  const job = app.jobs[0] || '';
  const instance = app.instances[0] || '';

  return {
    exported_instance: instance,
    exported_job: job,
  };
}

export function getEnvironmentText(app: AppViewModel | null) {
  if (!app) {
    return '未配置';
  }
  const value =
    app.attributes.deployment_environment ||
    app.attributes.environment ||
    app.attributes.env ||
    '';
  return !value || value.toLowerCase() === 'unknown' ? '未配置' : value;
}

export function getStatusInfo(app: AppViewModel | null) {
  if (!app?.heartbeatTimestamp) {
    return { text: '告警', type: 'danger' as const };
  }
  const diffSeconds = Math.max(
    0,
    Math.floor(Date.now() / 1000) - app.heartbeatTimestamp,
  );
  if (diffSeconds > 120) {
    return { text: '告警', type: 'warning' as const };
  }
  return { text: '健康', type: 'success' as const };
}

export function getServiceNameText(app: AppViewModel | null) {
  if (!app) {
    return '--';
  }
  return app.attributes['service.name'] || app.attributes.service_name || '--';
}

export function getHostNameText(app: AppViewModel | null) {
  if (!app) {
    return '--';
  }
  return app.attributes['host.name'] || app.attributes.host_name || '--';
}

export function getInstanceIdText(app: AppViewModel | null) {
  return app?.instance || '--';
}

export function getShortInstanceIdText(value: string) {
  if (!value || value === '--') {
    return '--';
  }
  if (value.length <= 16) {
    return value;
  }
  return `${value.slice(0, 8)}...${value.slice(-4)}`;
}
