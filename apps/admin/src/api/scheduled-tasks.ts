import { baseRequestClient } from '#/api/request';

export interface ScheduledTaskDefinition {
  configuration: any;
  createdAtUtc: string;
  createdBy: string;
  cronExpression: string;
  description: string | null;
  id: string;
  isEnabled: boolean;
  misfirePolicy: string;
  name: string;
  retryCount: number;
  retryIntervalSeconds: number;
  retryOnTimeout: boolean;
  rowVersion: string;
  taskType: string;
  timeZoneId: string;
  timeoutSeconds: number;
  updatedAtUtc: string;
  updatedBy: string;
}

export interface ScheduledTaskInput {
  configuration: any;
  cronExpression: string;
  description: string | null;
  isEnabled: boolean;
  misfirePolicy: string;
  name: string;
  retryCount: number;
  retryIntervalSeconds: number;
  retryOnTimeout: boolean;
  taskType: string;
  timeZoneId: string;
  timeoutSeconds: number;
}

export interface ScheduledTaskRun {
  attemptNumber: number;
  cancellationRequestedAtUtc: string | null;
  errorSummary: string | null;
  finishedAtUtc: string | null;
  hostInstanceId: string | null;
  id: string;
  isLogDeleted: boolean;
  isLogTruncated: boolean;
  lastHeartbeatAtUtc: string | null;
  logLengthBytes: number;
  logRelativePath: string;
  queuedAtUtc: string;
  resultSummary: string | null;
  rootRunId: string;
  scheduledAtUtc: string | null;
  startedAtUtc: string | null;
  status: string;
  taskId: string;
  triggerType: string;
  triggeredBy: string | null;
}

export interface ScheduledTaskLogEntry {
  category: string;
  eventId: number;
  exception: string | null;
  level: string;
  message: string;
  properties: Record<string, any>;
  sequence: number;
  timestampUtc: string;
}

export interface ScheduledTaskLogPage {
  isAvailable: boolean;
  isComplete: boolean;
  items: ScheduledTaskLogEntry[];
  nextSequence: number;
  runId: string;
}

export interface ScheduledTaskCatalog {
  databases: { dbContextType: string; hasExecutionConnection: boolean; key: string }[];
  handlers: { description: string | null; displayName: string; key: string; typeName: string }[];
}

export interface ScheduledTaskAudit {
  action: string;
  actorId: string;
  actorName: string;
  actorType: string;
  after: Record<string, any> | null;
  authenticationType: string;
  before: Record<string, any> | null;
  correlationId: string | null;
  id: number;
  ipAddress: string | null;
  occurredAtUtc: string;
  runId: string | null;
  taskId: string | null;
  userAgent: string | null;
}

export async function listScheduledTasksApi() {
  return baseRequestClient.get<ScheduledTaskDefinition[]>(
    '/admin/scheduled-tasks',
  );
}

export async function getScheduledTaskCatalogApi() {
  return baseRequestClient.get<ScheduledTaskCatalog>(
    '/admin/scheduled-tasks/catalog',
  );
}

export async function getScheduledTaskApi(taskId: string) {
  return baseRequestClient.get<ScheduledTaskDefinition>(
    `/admin/scheduled-tasks/${taskId}`,
  );
}

export async function createScheduledTaskApi(data: ScheduledTaskInput) {
  return baseRequestClient.post<ScheduledTaskDefinition>(
    '/admin/scheduled-tasks',
    data,
  );
}

export async function updateScheduledTaskApi(
  taskId: string,
  task: ScheduledTaskInput,
  rowVersion: string,
) {
  return baseRequestClient.put<ScheduledTaskDefinition>(
    `/admin/scheduled-tasks/${taskId}`,
    { rowVersion, task },
  );
}

export async function setScheduledTaskEnabledApi(
  taskId: string,
  isEnabled: boolean,
  rowVersion: string,
) {
  return baseRequestClient.put<ScheduledTaskDefinition>(
    `/admin/scheduled-tasks/${taskId}/enabled`,
    { isEnabled, rowVersion },
  );
}

export async function deleteScheduledTaskApi(
  taskId: string,
  rowVersion: string,
) {
  return baseRequestClient.delete(`/admin/scheduled-tasks/${taskId}`, {
    params: { rowVersion },
  });
}

export async function runScheduledTaskNowApi(taskId: string) {
  return baseRequestClient.post<{ runId: string }>(
    `/admin/scheduled-tasks/${taskId}/run`,
  );
}

export async function listScheduledTaskRunsApi(
  taskId: string,
  take = 50,
) {
  return baseRequestClient.get<ScheduledTaskRun[]>(
    `/admin/scheduled-tasks/${taskId}/runs`,
    { params: { take } },
  );
}

export async function listAllTaskRunsApi(taskId?: string, take = 50) {
  return baseRequestClient.get<ScheduledTaskRun[]>(
    '/admin/scheduled-task-runs',
    { params: { take, taskId } },
  );
}

export async function getTaskRunLogsApi(
  runId: string,
  afterSequence = 0,
  take = 200,
) {
  return baseRequestClient.get<ScheduledTaskLogPage>(
    `/admin/scheduled-task-runs/${runId}/logs`,
    { params: { afterSequence, take } },
  );
}

export async function cancelTaskRunApi(runId: string) {
  return baseRequestClient.post(`/admin/scheduled-task-runs/${runId}/cancel`);
}

export async function retryTaskRunApi(runId: string) {
  return baseRequestClient.post<{ runId: string }>(
    `/admin/scheduled-task-runs/${runId}/retry`,
  );
}

export async function listScheduledTaskAuditsApi(
  taskId: string,
  take = 100,
) {
  return baseRequestClient.get<ScheduledTaskAudit[]>(
    `/admin/scheduled-tasks/${taskId}/audits`,
    { params: { take } },
  );
}
