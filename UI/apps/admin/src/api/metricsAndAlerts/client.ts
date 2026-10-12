export interface MetricsAndAlertsGroupDto {
  id: number;
  groupName: string;
  isEnabled: boolean;
  sortOrder: number;
}

export interface MetricsAndAlertsMonitorTargetDto {
  aliveAlertEnabled: boolean;
  aliveAlerting: boolean;
  checkUrl?: null | string;
  checkIntervalSeconds?: null | number;
  checkUrlName?: null | string;
  checkUrlRuleId?: null | number;
  checkTimeoutSeconds?: null | number;
  durationMinutes?: null | number;
  expectedStatusCodes: number[];
  groupId?: null | number;
  groupName?: null | string;
  httpMethod?: null | string;
  id: number;
  isEnabled: boolean;
  minAliveCount?: null | number;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  offlineSeconds?: null | number;
  onlineCount?: null | number;
  queryError?: null | string;
  sortOrder: number;
  targetKey: string;
  targetName?: null | string;
  targetType: number;
  targetTypeName: string;
  urlAlertingCount: number;
  urlRuleCount: number;
}

export interface UpsertMetricsAndAlertsGroupRequest {
  groupName: string;
  id: number;
  isEnabled: boolean;
  sortOrder: number;
}

export interface UpsertMetricsAndAlertsMonitorTargetRequest {
  checkIntervalSeconds?: null | number;
  checkTimeoutSeconds?: null | number;
  checkUrl?: null | string;
  checkUrlName?: null | string;
  durationMinutes?: null | number;
  expectedStatusCodes?: number[];
  groupId?: null | number;
  httpMethod?: null | string;
  isEnabled: boolean;
  minAliveCount?: null | number;
  notifyMinuteQueue?: number[];
  notifyUsers: string[];
  offlineSeconds?: null | number;
  sortOrder: number;
  targetKey: string;
  targetName?: null | string;
  targetType: number;
}

export interface TargetAliveAlertRuleDto {
  createdAt: string;
  durationMinutes: number;
  id: number;
  isEnabled: boolean;
  minAliveCount: number;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  offlineSeconds: number;
  ruleName?: null | string;
  targetKey: string;
  targetType: number;
  updatedAt: string;
}

export interface UpsertTargetAliveAlertRuleRequest {
  durationMinutes: number;
  id: number;
  isEnabled: boolean;
  minAliveCount: number;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  offlineSeconds: number;
  ruleName?: null | string;
  targetKey: string;
  targetType: number;
}

export interface TargetUrlMonitorRuleDto {
  checkIntervalSeconds: number;
  createdAt: string;
  durationMinutes: number;
  expectedStatusCodes: number[];
  httpMethod: string;
  id: number;
  isEnabled: boolean;
  lastCheckAt?: null | string;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  ruleName?: null | string;
  targetKey: string;
  targetType: number;
  timeoutSeconds: number;
  updatedAt: string;
  url: string;
  urlName?: null | string;
}

export interface UpsertTargetUrlMonitorRuleRequest {
  checkIntervalSeconds: number;
  durationMinutes: number;
  expectedStatusCodes: number[];
  httpMethod: string;
  id: number;
  isEnabled: boolean;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  ruleName?: null | string;
  targetKey: string;
  targetType: number;
  timeoutSeconds: number;
  url: string;
  urlName?: null | string;
}

export interface MetricQueryItem {
  aggregateMode?: string;
  name: string;
  promql: string;
  valueMode: string;
}

export interface MetricAlertRuleDto {
  aggregateInstances: boolean;
  createdAt: string;
  durationMinutes: number;
  formula: string;
  id: number;
  isEnabled: boolean;
  messageTemplate?: null | string;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  queryItems: MetricQueryItem[];
  recoverTemplate?: null | string;
  ruleName: string;
  targetKey: string;
  targetType: number;
  updatedAt: string;
}

export interface UpsertMetricAlertRuleRequest {
  aggregateInstances: boolean;
  durationMinutes: number;
  formula: string;
  id: number;
  isEnabled: boolean;
  messageTemplate?: null | string;
  notifyMinuteQueue: number[];
  notifyUsers: string[];
  queryItems: MetricQueryItem[];
  recoverTemplate?: null | string;
  ruleName: string;
  targetKey: string;
  targetType: number;
}

export interface MetricInstanceFormulaResult {
  errorMessage?: null | string;
  instance: string;
  isAbnormal: boolean;
  previousValues: Record<string, number>;
  values: Record<string, number>;
}

export interface MetricAlertPreviewDto {
  abnormalInstances: string;
  aggregateInstances: boolean;
  currentValues: string;
  formula: string;
  instances: MetricInstanceFormulaResult[];
  isAbnormal: boolean;
  messageContent: string;
  recoverContent: string;
}

export interface ScheduledMetricMessageRuleDto {
  aggregateInstances: boolean;
  createdAt: string;
  cron: string;
  id: number;
  isEnabled: boolean;
  lastRunAt?: null | string;
  messageTemplate: string;
  nextRunAt?: null | string;
  notifyUsers: string[];
  queryItems: MetricQueryItem[];
  ruleName: string;
  targetKey: string;
  targetType: number;
  timeZoneId: string;
  updatedAt: string;
}

export interface UpsertScheduledMetricMessageRuleRequest {
  aggregateInstances: boolean;
  cron: string;
  id: number;
  isEnabled: boolean;
  messageTemplate: string;
  notifyUsers: string[];
  queryItems: MetricQueryItem[];
  ruleName: string;
  targetKey: string;
  targetType: number;
  timeZoneId: string;
}

export interface MetricInstanceQueryValues {
  instance: string;
  previousValues: Record<string, number>;
  values: Record<string, number>;
}

export interface ScheduledMetricMessagePreviewDto {
  aggregateInstances: boolean;
  currentValues: string;
  instances: MetricInstanceQueryValues[];
  messageContent: string;
}

export interface AlertStateDto {
  alertKey: string;
  alertType: number;
  currentValue?: null | string;
  firingAt?: null | string;
  firstAbnormalAt?: null | string;
  id: number;
  instanceName?: null | string;
  isActive: boolean;
  lastError?: null | string;
  lastNotifyAt?: null | string;
  nextNotifyIndex: number;
  recoveredAt?: null | string;
  ruleId: number;
  targetKey: string;
  targetType: number;
  thresholdValue?: null | string;
  updatedAt: string;
}

export interface AlertNotifyLogDto {
  alertStateId: number;
  alertType: number;
  content: string;
  createdAt: string;
  errorMessage?: null | string;
  id: number;
  isSuccess: boolean;
  notifyMinute?: null | number;
  notifyType: number;
  ruleId?: null | number;
  ruleName?: null | string;
  targetKey: string;
  targetType: number;
  title: string;
}

export interface PagedResultDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TestSendMessageDto {
  content: string;
  errorMessage?: null | string;
  isSuccess: boolean;
  title: string;
}

interface MetricsAndAlertsEnvelope<T> {
  data: T;
  errorCode: number;
  isSuccess: boolean;
  msg: string;
}

export const MetricsAndAlertsBaseUrl =
  import.meta.env.VITE_METRICS_AND_ALERTS_BASE_URL ||
  import.meta.env.VITE_GLOB_METRICS_AND_ALERTS_BASE_URL ||
  '';

export class MetricsAndAlertsClient {
  private readonly baseUrl: string;

  constructor(baseUrl = MetricsAndAlertsBaseUrl) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  async listGroups(includeDisabled = false) {
    const searchParams = new URLSearchParams({
      includeDisabled: includeDisabled ? 'true' : 'false',
    });
    return this.request<MetricsAndAlertsGroupDto[]>(
      `/api/monitor-target-group/list?${searchParams.toString()}`,
    );
  }

  async listApplications() {
    return this.request<MetricsAndAlertsMonitorTargetDto[]>(
      '/api/monitor-target/applications',
    );
  }

  async upsertGroup(payload: UpsertMetricsAndAlertsGroupRequest) {
    return this.request<MetricsAndAlertsGroupDto>(
      '/api/monitor-target-group/upsert',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async upsertMonitorTarget(payload: UpsertMetricsAndAlertsMonitorTargetRequest) {
    return this.request<MetricsAndAlertsMonitorTargetDto>(
      '/api/monitor-target/upsert',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async listTargetAliveAlertRules(targetKey: string, targetType: number) {
    const searchParams = new URLSearchParams({
      targetKey,
      targetType: String(targetType),
    });
    return this.request<TargetAliveAlertRuleDto[]>(
      `/api/target-alive-alert-rule/list?${searchParams.toString()}`,
    );
  }

  async upsertTargetAliveAlertRule(payload: UpsertTargetAliveAlertRuleRequest) {
    return this.request<TargetAliveAlertRuleDto>(
      '/api/target-alive-alert-rule/upsert',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async deleteTargetAliveAlertRule(id: number) {
    return this.request<{ message: string }>(
      `/api/target-alive-alert-rule/delete?id=${encodeURIComponent(id)}`,
      { method: 'DELETE' },
    );
  }

  async listTargetUrlMonitorRules(targetKey: string, targetType: number) {
    const searchParams = new URLSearchParams({
      targetKey,
      targetType: String(targetType),
    });
    return this.request<TargetUrlMonitorRuleDto[]>(
      `/api/target-url-monitor-rule/list?${searchParams.toString()}`,
    );
  }

  async upsertTargetUrlMonitorRule(payload: UpsertTargetUrlMonitorRuleRequest) {
    return this.request<TargetUrlMonitorRuleDto>(
      '/api/target-url-monitor-rule/upsert',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async deleteTargetUrlMonitorRule(id: number) {
    return this.request<{ message: string }>(
      `/api/target-url-monitor-rule/delete?id=${encodeURIComponent(id)}`,
      { method: 'DELETE' },
    );
  }

  async listMetricAlertRules(options: {
    includeDisabled?: boolean;
    targetKey?: string;
    targetType?: number;
  }) {
    const searchParams = new URLSearchParams();
    if (options.targetKey) {
      searchParams.set('targetKey', options.targetKey);
    }
    if (options.targetType !== undefined) {
      searchParams.set('targetType', String(options.targetType));
    }
    searchParams.set('includeDisabled', options.includeDisabled ? 'true' : 'false');
    return this.request<MetricAlertRuleDto[]>(
      `/api/metric-alert-rule/list?${searchParams.toString()}`,
    );
  }

  async upsertMetricAlertRule(payload: UpsertMetricAlertRuleRequest) {
    return this.request<MetricAlertRuleDto>('/api/metric-alert-rule/upsert', {
      body: JSON.stringify(payload),
      method: 'POST',
    });
  }

  async deleteMetricAlertRule(id: number) {
    return this.request<{ message: string }>(
      `/api/metric-alert-rule/delete?id=${encodeURIComponent(id)}`,
      { method: 'DELETE' },
    );
  }

  async previewMetricAlertRule(payload: UpsertMetricAlertRuleRequest) {
    return this.request<MetricAlertPreviewDto>('/api/metric-alert-rule/preview', {
      body: JSON.stringify(payload),
      method: 'POST',
    });
  }

  async testSendMetricAlertRule(payload: UpsertMetricAlertRuleRequest) {
    return this.request<TestSendMessageDto>('/api/metric-alert-rule/test-send', {
      body: JSON.stringify(payload),
      method: 'POST',
    });
  }

  async listScheduledMessageRules(options: {
    includeDisabled?: boolean;
    targetKey?: string;
    targetType?: number;
  }) {
    const searchParams = new URLSearchParams();
    if (options.targetKey) {
      searchParams.set('targetKey', options.targetKey);
    }
    if (options.targetType !== undefined) {
      searchParams.set('targetType', String(options.targetType));
    }
    searchParams.set('includeDisabled', options.includeDisabled ? 'true' : 'false');
    return this.request<ScheduledMetricMessageRuleDto[]>(
      `/api/scheduled-message-rule/list?${searchParams.toString()}`,
    );
  }

  async upsertScheduledMessageRule(payload: UpsertScheduledMetricMessageRuleRequest) {
    return this.request<ScheduledMetricMessageRuleDto>(
      '/api/scheduled-message-rule/upsert',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async deleteScheduledMessageRule(id: number) {
    return this.request<{ message: string }>(
      `/api/scheduled-message-rule/delete?id=${encodeURIComponent(id)}`,
      { method: 'DELETE' },
    );
  }

  async previewScheduledMessageRule(payload: UpsertScheduledMetricMessageRuleRequest) {
    return this.request<ScheduledMetricMessagePreviewDto>(
      '/api/scheduled-message-rule/preview',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async testSendScheduledMessageRule(payload: UpsertScheduledMetricMessageRuleRequest) {
    return this.request<TestSendMessageDto>(
      '/api/scheduled-message-rule/test-send',
      {
        body: JSON.stringify(payload),
        method: 'POST',
      },
    );
  }

  async listAlertStates(options: {
    alertType?: number;
    isActive?: boolean;
    targetKey?: string;
    targetType?: number;
  }) {
    const searchParams = new URLSearchParams();
    if (options.targetKey) {
      searchParams.set('targetKey', options.targetKey);
    }
    if (options.targetType !== undefined) {
      searchParams.set('targetType', String(options.targetType));
    }
    if (options.alertType !== undefined) {
      searchParams.set('alertType', String(options.alertType));
    }
    if (options.isActive !== undefined) {
      searchParams.set('isActive', options.isActive ? 'true' : 'false');
    }
    return this.request<AlertStateDto[]>(
      `/api/alert-state/list?${searchParams.toString()}`,
    );
  }

  async listAlertNotifyLogs(options: {
    alertType?: number;
    endTime?: string;
    page?: number;
    pageSize?: number;
    ruleId?: number;
    startTime?: string;
    targetKey?: string;
    targetType?: number;
  }) {
    const searchParams = new URLSearchParams();
    if (options.targetKey) {
      searchParams.set('targetKey', options.targetKey);
    }
    if (options.targetType !== undefined) {
      searchParams.set('targetType', String(options.targetType));
    }
    if (options.alertType !== undefined) {
      searchParams.set('alertType', String(options.alertType));
    }
    if (options.ruleId !== undefined) {
      searchParams.set('ruleId', String(options.ruleId));
    }
    if (options.startTime) {
      searchParams.set('startTime', options.startTime);
    }
    if (options.endTime) {
      searchParams.set('endTime', options.endTime);
    }
    searchParams.set('page', String(options.page ?? 1));
    searchParams.set('pageSize', String(options.pageSize ?? 20));
    return this.request<PagedResultDto<AlertNotifyLogDto>>(
      `/api/alert-notify-log/list?${searchParams.toString()}`,
    );
  }

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    if (!this.baseUrl) {
      throw new Error(
        'MetricsAndAlertsBaseUrl is empty. Set VITE_METRICS_AND_ALERTS_BASE_URL in .env.',
      );
    }

    const headers = new Headers(init?.headers);
    if (init?.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers,
    });

    if (!response.ok) {
      throw new Error(`MetricsAndAlerts HTTP ${response.status}`);
    }

    const payload = (await response.json()) as MetricsAndAlertsEnvelope<T>;
    if (!payload.isSuccess) {
      throw new Error(payload.msg || 'MetricsAndAlerts request failed.');
    }

    return payload.data;
  }
}
