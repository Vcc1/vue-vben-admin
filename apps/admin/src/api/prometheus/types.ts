export type PrometheusValue = [number, string];

export interface PrometheusMetric {
  [label: string]: string;
}

export interface PrometheusVectorResult {
  metric: PrometheusMetric;
  value: PrometheusValue;
}

export interface PrometheusMatrixResult {
  metric: PrometheusMetric;
  values: PrometheusValue[];
}

export interface PrometheusSeriesResult {
  [label: string]: string;
}

export interface PrometheusSuccessResponse<T> {
  data: T;
  status: 'success';
}

export interface PrometheusErrorResponse {
  error?: string;
  errorType?: string;
  status: 'error';
}

export type PrometheusApiResponse<T> =
  | PrometheusSuccessResponse<T>
  | PrometheusErrorResponse;

export interface PrometheusVectorData {
  result: PrometheusVectorResult[];
  resultType: 'vector';
}

export interface PrometheusMatrixData {
  result: PrometheusMatrixResult[];
  resultType: 'matrix';
}

export interface DiskUsageInfo {
  fsType: null | string;
  lastReportedAt: number | null;
  name: string;
  readBytesPerSec: number | null;
  total: number | null;
  usagePercent: number | null;
  used: number | null;
  writeBytesPerSec: number | null;
}

export interface PrometheusServerCard {
  cpuUsagePercent: number | null;
  diskUsage: DiskUsageInfo[];
  memoryUsagePercent: number | null;
  server: string;
  systemType: null | string;
  updatedAt: number | null;
}
