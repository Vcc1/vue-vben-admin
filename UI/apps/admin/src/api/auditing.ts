import { baseRequestClient } from '#/api/request';

export interface RequestAuditLogItem {
  actionName: null | string;
  actorId: string;
  actorName: string;
  actorType: string;
  authenticationType: string;
  controllerName: null | string;
  correlationId: null | string;
  durationMs: number;
  /** 功能名称（中文），由后端 [RequestAudit(Feature = "...")] 声明。 */
  featureName: null | string;
  httpMethod: string;
  id: number;
  ipAddress: null | string;
  isRequestBodyTruncated: boolean;
  isResponseBodyTruncated: boolean;
  occurredAtUtc: string;
  /** 操作名称（中文），由后端 [AuditOperation("...")] 声明。 */
  operationName: null | string;
  path: string;
  queryString: null | string;
  requestBody: null | string;
  responseBody: null | string;
  statusCode: number;
  userAgent: null | string;
}

export interface SecurityEventItem {
  actorId: string;
  actorName: string;
  actorType: string;
  authenticationType: string;
  correlationId: null | string;
  detailJson: null | string;
  eventType: string;
  id: number;
  ipAddress: null | string;
  occurredAtUtc: string;
  severity: string;
  summary: null | string;
  userAgent: null | string;
}

export interface AuditQuery {
  actorName?: string;
  eventType?: string;
  feature?: string;
  fromUtc?: string;
  httpMethod?: string;
  path?: string;
  severity?: string;
  take?: number;
  toUtc?: string;
}

export async function listRequestAuditLogsApi(params: AuditQuery = {}) {
  return baseRequestClient.get<RequestAuditLogItem[]>(
    '/admin/auditing/request-logs',
    { params },
  );
}

export async function listSecurityEventsApi(params: AuditQuery = {}) {
  return baseRequestClient.get<SecurityEventItem[]>(
    '/admin/auditing/security-events',
    { params },
  );
}
