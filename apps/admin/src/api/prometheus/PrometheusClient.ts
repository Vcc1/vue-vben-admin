import type {
  PrometheusApiResponse,
  PrometheusMatrixData,
  PrometheusSeriesResult,
  PrometheusVectorData,
} from './types';

export interface QueryRangeParams {
  end: number;
  query: string;
  start: number;
  step: number | string;
}

export const PrometheusBaseUrl =
  import.meta.env.VITE_PROMETHEUS_BASE_URL ||
  import.meta.env.VITE_GLOB_PROMETHEUS_BASE_URL ||
  '';

export class PrometheusClient {
  private readonly baseUrl: string;

  constructor(baseUrl = PrometheusBaseUrl) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  async query(query: string, time?: number) {
    const searchParams = new URLSearchParams({ query });

    if (typeof time === 'number') {
      searchParams.set('time', `${time}`);
    }

    const data = await this.request<PrometheusVectorData>('/api/v1/query', searchParams);
    return data.result;
  }

  async queryRange(params: QueryRangeParams) {
    const searchParams = new URLSearchParams({
      end: `${params.end}`,
      query: params.query,
      start: `${params.start}`,
      step: `${params.step}`,
    });

    const data = await this.request<PrometheusMatrixData>('/api/v1/query_range', searchParams);
    return data.result;
  }

  async labelValues(label: string, matchers: string[] = []) {
    const searchParams = new URLSearchParams();

    for (const matcher of matchers) {
      searchParams.append('match[]', matcher);
    }

    return this.request<string[]>(`/api/v1/label/${encodeURIComponent(label)}/values`, searchParams);
  }

  async series(matchers: string[], start?: number, end?: number) {
    const searchParams = new URLSearchParams();

    for (const matcher of matchers) {
      searchParams.append('match[]', matcher);
    }

    if (typeof start === 'number') {
      searchParams.set('start', `${start}`);
    }

    if (typeof end === 'number') {
      searchParams.set('end', `${end}`);
    }

    return this.request<PrometheusSeriesResult[]>('/api/v1/series', searchParams);
  }

  private async request<T>(path: string, searchParams?: URLSearchParams): Promise<T> {
    if (!this.baseUrl) {
      throw new Error('PrometheusBaseUrl is empty. Set VITE_PROMETHEUS_BASE_URL in .env.');
    }

    const queryString = searchParams?.toString();
    const url = `${this.baseUrl}${path}${queryString ? `?${queryString}` : ''}`;

    const response = await fetch(url);

    if (!response.ok) {
      const body = await response.text();
      throw new Error(
        body
          ? `Prometheus HTTP ${response.status}: ${body}`
          : `Prometheus HTTP ${response.status}`,
      );
    }

    const payload = (await response.json()) as PrometheusApiResponse<T>;

    if (payload.status !== 'success') {
      throw new Error(payload.error || 'Prometheus request failed.');
    }

    return payload.data;
  }
}
