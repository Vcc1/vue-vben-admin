export type RuntimeRange = '15m' | '1h' | '6h' | '24h';

export interface AppLatestMetricValue {
  labelsText: string;
  metricName: string;
  valueText: string;
}

export interface AppSummaryItem {
  appName: string;
  environmentName: string;
  hasActiveInstanceAlert: boolean;
  heartbeatTimestamp: null | number;
  hostName: string;
  instance: string;
  itemKey: string;
  jobName: string;
  serviceVersion: string;
}

export interface AppSummaryGroup {
  appName: string;
  groupKey: string;
  items: AppSummaryItem[];
  latestHeartbeatTimestamp: null | number;
  onlineCount: number;
}

export interface MonitorTargetAppGroup {
  appName: string;
  appTarget: null | MonitorTargetConfig;
  groupKey: string;
  hasActiveAppAlert: boolean;
  items: AppSummaryItem[];
  latestHeartbeatTimestamp: null | number;
  onlineCount: number;
}

export interface MonitorTargetConfig {
  checkIntervalSeconds: null | number;
  checkTimeoutSeconds: null | number;
  checkUrl: null | string;
  checkUrlName: null | string;
  durationMinutes: null | number;
  expectedStatusCodes: number[];
  groupId: null | number;
  groupName: null | string;
  httpMethod: null | string;
  id: number;
  isEnabled: boolean;
  minAliveCount: null | number;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  offlineSeconds: null | number;
  sortOrder: number;
  targetKey: string;
  targetName: null | string;
}

export interface MonitorTargetSidebarGroup {
  activeAlertCount: number;
  apps: MonitorTargetAppGroup[];
  groupId: number;
  groupKey: string;
  groupName: string;
  isDefault: boolean;
  isEnabled: boolean;
  onlineCount: number;
  sortOrder: number;
}

export interface MonitorTargetGroupOption {
  id: number;
  groupName: string;
  isDefault?: boolean;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AppViewModel {
  appName: string;
  attributes: Record<string, string>;
  availableMetrics: string[];
  config: Record<string, string>;
  heartbeatTimestamp: null | number;
  instance: string;
  jobDisplayName: string;
  latestMetricValues: AppLatestMetricValue[];
  inflight: null | number;
  instances: string[];
  jobs: string[];
  startTimeSeconds: null | number;
  serviceVersion: string;
  uptimeSeconds: null | number;
}
