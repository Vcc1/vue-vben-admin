import type { PrometheusMatrixResult } from '#/api/prometheus/types';

export type RangeKey = '15m' | '1h' | '6h' | '24h';
export type MetricNavItemType = 'cpu' | 'disk' | 'memory' | 'network';
export type DetailTab = 'basic' | 'performance';

export interface ServerApplicationItem {
  appName: string;
  environment: string;
  instance: string;
}

export interface DetailMetricsRaw {
  cpuCore: PrometheusMatrixResult[];
  cpuTotal: PrometheusMatrixResult[];
  diskIoRead: PrometheusMatrixResult[];
  diskIoWrite: PrometheusMatrixResult[];
  diskTotal: PrometheusMatrixResult[];
  diskUsed: PrometheusMatrixResult[];
  memoryAvailable: PrometheusMatrixResult[];
  memoryUsed: PrometheusMatrixResult[];
  networkReceive: PrometheusMatrixResult[];
  networkTransmit: PrometheusMatrixResult[];
}

export interface MetricNavItem {
  key: string;
  label: string;
  type: MetricNavItemType;
}
