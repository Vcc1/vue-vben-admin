<script lang="ts" setup>
import type { PrometheusMatrixResult, PrometheusValue, PrometheusVectorResult } from '#/api/prometheus/types';
import type { AppViewModel, RuntimeRange } from '../../types';

type EChartsOption = Record<string, any>;

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, ref, watch } from 'vue';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import { getRuntimeQueryTime, getRuntimeTargetLabels } from '../../appPanelUtils';

defineOptions({ name: 'SupStaticDataPanel' });

const props = withDefaults(defineProps<{
  app: AppViewModel | null;
  range?: RuntimeRange;
}>(), {
  range: '1h',
});

interface SupplierStaticDataRow {
  avgDuration: string;
  avgRoomCount: string;
  failed: string;
  maxOccupancyMissing: string;
  p50Duration: string;
  p95Duration: string;
  roomCodeNotFound: string;
  successWithRooms: string;
  successWithoutRooms: string;
  supId: string;
  total: string;
}

interface BreakdownRow {
  count: string;
  errorCode?: string;
  outcome?: string;
  sortValue: number;
  source?: string;
  supId: string;
}

interface LineSeries {
  color: string;
  data: Array<[number, number]>;
  name: string;
}

const SUPPLIER_IDS = ['51', '1', '32'] as const;
const SUPPLIER_MATCHER = 'sup_id=~"51|1|32"';
const DATA_KIND_MATCHER = 'data_kind=~"hotel|hotelAndRoom"';

const RANGE_SECONDS_MAP: Record<RuntimeRange, number> = {
  '15m': 15 * 60,
  '1h': 60 * 60,
  '6h': 6 * 60 * 60,
  '24h': 24 * 60 * 60,
};

const RANGE_STEP_MAP: Record<RuntimeRange, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

const METRIC_NAME_ALIASES = {
  lookupDuration: ['supplier_static_data_lookup_duration_ms', 'supplier_static_data_lookup_duration_ms_milliseconds'],
  lookupTotal: [
    'supplier_static_data_lookup_total',
    'supplier_static_data_lookup_total_requests_total',
    'supplier_static_data_lookup_requests_total',
  ],
  roomCount: ['supplier_static_data_room_count', 'supplier_static_data_room_count_rooms'],
  roomMatchTotal: [
    'ota_room_builder_static_data_room_match_total',
    'ota_room_builder_static_data_room_match_total_rooms_total',
    'ota_room_builder_static_data_room_match_rooms_total',
  ],
} as const;

const TREND_COLORS = ['#2563eb', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#14b8a6'] as const;

const client = new PrometheusClient();
const loading = ref(false);
const errorText = ref('');
const supplierRows = ref<SupplierStaticDataRow[]>([]);
const sourceRows = ref<BreakdownRow[]>([]);
const errorRows = ref<BreakdownRow[]>([]);
const outcomeTrendSeries = ref<LineSeries[]>([]);
const latencyTrendSeries = ref<LineSeries[]>([]);
const outcomeTrendRef = ref<EchartsUIType>();
const latencyTrendRef = ref<EchartsUIType>();
const outcomeLegendSelected = ref<Record<string, boolean>>({});
const latencyLegendSelected = ref<Record<string, boolean>>({});

const {
  getChartInstance: getOutcomeTrendInstance,
  renderEcharts: renderOutcomeTrend,
} = useEcharts(outcomeTrendRef);
const {
  getChartInstance: getLatencyTrendInstance,
  renderEcharts: renderLatencyTrend,
} = useEcharts(latencyTrendRef);

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));

const context = computed(() => {
  const step = RANGE_STEP_MAP[props.range];
  const baseTime =
    typeof runtimeQueryTime.value === 'number' && Number.isFinite(runtimeQueryTime.value)
      ? runtimeQueryTime.value
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  const start = end - RANGE_SECONDS_MAP[props.range];
  return {
    end,
    start,
    step,
    trendWindow: `${Math.max(step * 4, 60)}s`,
    wholeWindow: props.range,
  };
});

const hasOutcomeTrend = computed(() => hasSeriesData(outcomeTrendSeries.value));
const hasLatencyTrend = computed(() => hasSeriesData(latencyTrendSeries.value));

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function buildSelector(labels: Record<string, string>, extraMatchers: string[] = []) {
  const parts = Object.entries(labels)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `{${[...parts, ...extraMatchers].join(',')}}`;
}

function buildMetricSelector(metricName: string, labels: Record<string, string>, extraMatchers: string[] = []) {
  return `${metricName}${buildSelector(labels, extraMatchers)}`;
}

function buildHistogramMetricSelector(
  metricName: string,
  suffix: 'bucket' | 'count' | 'sum',
  labels: Record<string, string>,
  extraMatchers: string[] = [],
) {
  return `${metricName}_${suffix}${buildSelector(labels, extraMatchers)}`;
}

function parsePrometheusNumber(value?: null | PrometheusValue) {
  if (!value) {
    return null;
  }
  const numericValue = Number(value[1]);
  return Number.isFinite(numericValue) ? numericValue : null;
}

function formatInteger(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 0 }).format(value);
}

function formatNumber(value: null | number, digits = 2) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: digits,
    minimumFractionDigits: 0,
  }).format(value);
}

function formatDuration(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  if (value >= 1000) {
    return `${formatNumber(value / 1000, 2)} s`;
  }
  return `${formatNumber(value, 2)} ms`;
}

function resolveMetricName(metricNames: readonly string[], histogram = false) {
  const available = props.app?.availableMetrics || [];
  for (const metricName of metricNames) {
    if (available.includes(metricName)) {
      return metricName;
    }
    if (
      histogram &&
      (available.includes(`${metricName}_bucket`) ||
        available.includes(`${metricName}_count`) ||
        available.includes(`${metricName}_sum`))
    ) {
      return metricName;
    }
  }
  return metricNames[0] ?? '';
}

function mapVectorByLabel(results: PrometheusVectorResult[], labelKey: string) {
  const map = new Map<string, number>();
  for (const row of results) {
    const key = row.metric[labelKey] || '--';
    const value = parsePrometheusNumber(row.value);
    if (value === null) {
      continue;
    }
    map.set(key, (map.get(key) || 0) + value);
  }
  return map;
}

function mapVectorByLabels(results: PrometheusVectorResult[], labels: string[]) {
  const map = new Map<string, number>();
  for (const row of results) {
    const key = labels.map((label) => row.metric[label] || '--').join('@@');
    const value = parsePrometheusNumber(row.value);
    if (value === null) {
      continue;
    }
    map.set(key, (map.get(key) || 0) + value);
  }
  return map;
}

function sumMatrixSeries(results: PrometheusMatrixResult[]) {
  const bucket = new Map<number, number>();
  for (const row of results) {
    for (const [timestamp, rawValue] of row.values) {
      const numericValue = Number(rawValue);
      if (!Number.isFinite(numericValue)) {
        continue;
      }
      const millis = timestamp * 1000;
      bucket.set(millis, (bucket.get(millis) || 0) + numericValue);
    }
  }
  return [...bucket.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([timestamp, value]) => [timestamp, value] as [number, number]);
}

function buildSingleSeries(
  name: string,
  color: string,
  results: PrometheusMatrixResult[],
  timeline: number[],
): LineSeries {
  const pointMap = new Map(sumMatrixSeries(results));
  return {
    color,
    data: timeline.map((timestamp) => [timestamp, pointMap.get(timestamp) || 0]) as Array<[number, number]>,
    name,
  };
}

function buildGroupedSeries(
  results: PrometheusMatrixResult[],
  labelKey: string,
  labelNames: Record<string, string>,
  timeline: number[],
) {
  const grouped = new Map<string, Map<number, number>>();
  for (const row of results) {
    const key = row.metric[labelKey] || '--';
    const points = grouped.get(key) || new Map<number, number>();
    for (const [timestamp, rawValue] of row.values) {
      const numericValue = Number(rawValue);
      if (!Number.isFinite(numericValue)) {
        continue;
      }
      const millis = timestamp * 1000;
      points.set(millis, (points.get(millis) || 0) + numericValue);
    }
    grouped.set(key, points);
  }

  return [...grouped.entries()]
    .map(([key, points], index) => ({
      color: TREND_COLORS[index % TREND_COLORS.length] ?? '#2563eb',
      data: timeline.map((timestamp) => [timestamp, points.get(timestamp) || 0]) as Array<[number, number]>,
      name: labelNames[key] || key,
    }))
    .filter((item) => item.data.some(([, value]) => value !== 0));
}

function buildLineOption(
  series: LineSeries[],
  yAxisName: string,
  formatter: (value: number) => string,
  legendSelected: Record<string, boolean> = {},
): EChartsOption {
  const existingLegendSelected = Object.fromEntries(
    series
      .map((item) => [item.name, legendSelected[item.name]])
      .filter(([, value]) => value !== undefined),
  );
  return {
    color: series.map((item) => item.color),
    grid: {
      bottom: 20,
      containLabel: true,
      left: 16,
      right: 20,
      top: 30,
    },
    legend: {
      icon: 'roundRect',
      itemHeight: 8,
      itemWidth: 12,
      selected: existingLegendSelected,
      type: 'scroll',
    },
    series: series.map((item) => ({
      data: item.data,
      lineStyle: { width: 2 },
      name: item.name,
      showSymbol: false,
      smooth: true,
      type: 'line',
    })),
    tooltip: {
      appendToBody: true,
      backgroundColor: 'rgba(18, 18, 18, 0.88)',
      borderWidth: 0,
      formatter: (params: any[]) => {
        const rows = Array.isArray(params) ? params : [];
        const timeText = rows[0]?.axisValue
          ? dayjs(rows[0].axisValue).format('YYYY-MM-DD HH:mm:ss')
          : '--';
        const lines = [timeText];
        for (const row of rows) {
          lines.push(`${row.marker}${row.seriesName}: ${formatter(Number(row.value?.[1] ?? 0))}`);
        }
        return lines.join('<br />');
      },
      textStyle: {
        color: '#f5f7fa',
      },
      trigger: 'axis',
    },
    xAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter: (value: number) => dayjs(value).format('HH:mm'),
        hideOverlap: true,
      },
      type: 'time',
    },
    yAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter,
      },
      name: yAxisName,
      type: 'value',
    },
  };
}

function hasSeriesData(series: LineSeries[]) {
  return series.some((item) => item.data.some(([, value]) => value !== 0));
}

function bindLegendState(
  getChartInstance: () => any,
  legendSelected: typeof outcomeLegendSelected,
) {
  const chart = getChartInstance();
  if (!chart) {
    return;
  }
  chart.off('legendselectchanged');
  chart.on('legendselectchanged', (params: any) => {
    if (params?.selected && typeof params.selected === 'object') {
      legendSelected.value = { ...params.selected };
    }
  });
}

async function queryInstant(query: string) {
  return client.query(query, context.value.end);
}

async function queryRange(query: string) {
  return client.queryRange({
    end: context.value.end,
    query,
    start: context.value.start,
    step: context.value.step,
  });
}

async function renderCharts() {
  await nextTick();
  if (hasOutcomeTrend.value) {
    await renderOutcomeTrend(
      buildLineOption(outcomeTrendSeries.value, 'count/window', formatInteger, outcomeLegendSelected.value),
    );
    bindLegendState(getOutcomeTrendInstance, outcomeLegendSelected);
  }
  if (hasLatencyTrend.value) {
    await renderLatencyTrend(
      buildLineOption(latencyTrendSeries.value, 'ms', formatDuration, latencyLegendSelected.value),
    );
    bindLegendState(getLatencyTrendInstance, latencyLegendSelected);
  }
}

let analyticsRequestId = 0;

async function loadStaticDataAnalytics() {
  if (!props.app) {
    supplierRows.value = [];
    sourceRows.value = [];
    errorRows.value = [];
    outcomeTrendSeries.value = [];
    latencyTrendSeries.value = [];
    errorText.value = '';
    return;
  }

  const labels = targetLabels.value;
  if (Object.keys(labels).length === 0) {
    supplierRows.value = [];
    sourceRows.value = [];
    errorRows.value = [];
    outcomeTrendSeries.value = [];
    latencyTrendSeries.value = [];
    errorText.value = '';
    return;
  }

  const requestId = ++analyticsRequestId;
  loading.value = true;
  errorText.value = '';

  try {
    const lookupMetricName = resolveMetricName(METRIC_NAME_ALIASES.lookupTotal);
    const durationMetricName = resolveMetricName(METRIC_NAME_ALIASES.lookupDuration, true);
    const roomCountMetricName = resolveMetricName(METRIC_NAME_ALIASES.roomCount, true);
    const roomMatchMetricName = resolveMetricName(METRIC_NAME_ALIASES.roomMatchTotal);
    const lookupBaseMatchers = [SUPPLIER_MATCHER, DATA_KIND_MATCHER];
    const roomMatchBaseMatchers = [SUPPLIER_MATCHER];
    const durationBucketSelector = buildHistogramMetricSelector(durationMetricName, 'bucket', labels, lookupBaseMatchers);
    const durationSumSelector = buildHistogramMetricSelector(durationMetricName, 'sum', labels, lookupBaseMatchers);
    const durationCountSelector = buildHistogramMetricSelector(durationMetricName, 'count', labels, lookupBaseMatchers);
    const roomCountSumSelector = buildHistogramMetricSelector(roomCountMetricName, 'sum', labels, lookupBaseMatchers);
    const roomCountCountSelector = buildHistogramMetricSelector(roomCountMetricName, 'count', labels, lookupBaseMatchers);
    const timeline = Array.from(
      { length: Math.floor((context.value.end - context.value.start) / context.value.step) + 1 },
      (_, index) => (context.value.start + index * context.value.step) * 1000,
    );

    const [
      totalBySupResults,
      withRoomsBySupResults,
      withoutRoomsBySupResults,
      failedBySupResults,
      roomCodeNotFoundBySupResults,
      maxOccupancyMissingBySupResults,
      avgDurationBySupResults,
      avgRoomCountBySupResults,
      p50DurationBySupResults,
      p95DurationBySupResults,
      sourceBreakdownResults,
      errorBreakdownResults,
      withRoomsTrendResults,
      withoutRoomsTrendResults,
      roomCodeNotFoundTrendResults,
      failedTrendResults,
      latencyTrendResults,
    ] = await Promise.all([
      queryInstant(
        `sum by (sup_id) (increase(${buildMetricSelector(lookupMetricName, labels, lookupBaseMatchers)}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id) (increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="success_with_rooms"'])}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id) (increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="success_without_rooms"'])}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id) (increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="failed"'])}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id) (increase(${buildMetricSelector(roomMatchMetricName, labels, [...roomMatchBaseMatchers, 'outcome="room_code_not_found"'])}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id) (increase(${buildMetricSelector(roomMatchMetricName, labels, [...roomMatchBaseMatchers, 'outcome="max_occupancy_missing"'])}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id) (rate(${durationSumSelector}[5m])) / sum by (sup_id) (rate(${durationCountSelector}[5m]))`,
      ),
      queryInstant(
        `sum by (sup_id) (rate(${roomCountSumSelector}[5m])) / sum by (sup_id) (rate(${roomCountCountSelector}[5m]))`,
      ),
      queryInstant(
        `histogram_quantile(0.50, sum by (le, sup_id) (rate(${durationBucketSelector}[5m])))`,
      ),
      queryInstant(
        `histogram_quantile(0.95, sum by (le, sup_id) (rate(${durationBucketSelector}[5m])))`,
      ),
      queryInstant(
        `sum by (sup_id, source, outcome) (increase(${buildMetricSelector(lookupMetricName, labels, lookupBaseMatchers)}[${context.value.wholeWindow}]))`,
      ),
      queryInstant(
        `sum by (sup_id, source, error_code) (increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="failed"'])}[${context.value.wholeWindow}]))`,
      ),
      queryRange(
        `sum(increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="success_with_rooms"'])}[${context.value.trendWindow}]))`,
      ),
      queryRange(
        `sum(increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="success_without_rooms"'])}[${context.value.trendWindow}]))`,
      ),
      queryRange(
        `sum(increase(${buildMetricSelector(roomMatchMetricName, labels, [...roomMatchBaseMatchers, 'outcome="room_code_not_found"'])}[${context.value.trendWindow}]))`,
      ),
      queryRange(
        `sum(increase(${buildMetricSelector(lookupMetricName, labels, [...lookupBaseMatchers, 'outcome="failed"'])}[${context.value.trendWindow}]))`,
      ),
      queryRange(
        `histogram_quantile(0.95, sum by (le, sup_id) (rate(${durationBucketSelector}[5m])))`,
      ),
    ]);

    if (requestId !== analyticsRequestId) {
      return;
    }

    const totalBySup = mapVectorByLabel(totalBySupResults, 'sup_id');
    const withRoomsBySup = mapVectorByLabel(withRoomsBySupResults, 'sup_id');
    const withoutRoomsBySup = mapVectorByLabel(withoutRoomsBySupResults, 'sup_id');
    const failedBySup = mapVectorByLabel(failedBySupResults, 'sup_id');
    const roomCodeNotFoundBySup = mapVectorByLabel(roomCodeNotFoundBySupResults, 'sup_id');
    const maxOccupancyMissingBySup = mapVectorByLabel(maxOccupancyMissingBySupResults, 'sup_id');
    const avgDurationBySup = mapVectorByLabel(avgDurationBySupResults, 'sup_id');
    const avgRoomCountBySup = mapVectorByLabel(avgRoomCountBySupResults, 'sup_id');
    const p50DurationBySup = mapVectorByLabel(p50DurationBySupResults, 'sup_id');
    const p95DurationBySup = mapVectorByLabel(p95DurationBySupResults, 'sup_id');

    supplierRows.value = SUPPLIER_IDS.map((supId) => ({
      avgDuration: formatDuration(avgDurationBySup.get(supId) ?? null),
      avgRoomCount: formatNumber(avgRoomCountBySup.get(supId) ?? null, 1),
      failed: formatInteger(failedBySup.get(supId) ?? 0),
      maxOccupancyMissing: formatInteger(maxOccupancyMissingBySup.get(supId) ?? 0),
      p50Duration: formatDuration(p50DurationBySup.get(supId) ?? null),
      p95Duration: formatDuration(p95DurationBySup.get(supId) ?? null),
      roomCodeNotFound: formatInteger(roomCodeNotFoundBySup.get(supId) ?? 0),
      successWithRooms: formatInteger(withRoomsBySup.get(supId) ?? 0),
      successWithoutRooms: formatInteger(withoutRoomsBySup.get(supId) ?? 0),
      supId,
      total: formatInteger(totalBySup.get(supId) ?? 0),
    }));

    const sourceBreakdown = mapVectorByLabels(sourceBreakdownResults, ['sup_id', 'source', 'outcome']);
    sourceRows.value = [...sourceBreakdown.entries()]
      .map(([key, value]) => {
        const [supId, source, outcome] = key.split('@@');
        return {
          count: formatInteger(value),
          outcome: outcome || '--',
          sortValue: value,
          source: source || '--',
          supId: supId || '--',
        };
      })
      .sort((a, b) => b.sortValue - a.sortValue || a.supId.localeCompare(b.supId));

    const errorBreakdown = mapVectorByLabels(errorBreakdownResults, ['sup_id', 'source', 'error_code']);
    errorRows.value = [...errorBreakdown.entries()]
      .map(([key, value]) => {
        const [supId, source, errorCode] = key.split('@@');
        return {
          count: formatInteger(value),
          errorCode: errorCode || '--',
          sortValue: value,
          source: source || '--',
          supId: supId || '--',
        };
      })
      .sort((a, b) => b.sortValue - a.sortValue || a.supId.localeCompare(b.supId));

    outcomeTrendSeries.value = [
      buildSingleSeries('返回房型', '#10b981', withRoomsTrendResults, timeline),
      buildSingleSeries('未返回房型', '#f59e0b', withoutRoomsTrendResults, timeline),
      buildSingleSeries('SRoomCode 未匹配', '#ef4444', roomCodeNotFoundTrendResults, timeline),
      buildSingleSeries('静态数据失败', '#8b5cf6', failedTrendResults, timeline),
    ].filter((item) => item.data.some(([, value]) => value !== 0));

    latencyTrendSeries.value = buildGroupedSeries(
      latencyTrendResults,
      'sup_id',
      {
        '1': 'sup 1 P95',
        '32': 'sup 32 P95',
        '51': 'sup 51 P95',
      },
      timeline,
    );

    await renderCharts();
  } catch (error) {
    if (requestId !== analyticsRequestId) {
      return;
    }
    supplierRows.value = [];
    sourceRows.value = [];
    errorRows.value = [];
    outcomeTrendSeries.value = [];
    latencyTrendSeries.value = [];
    errorText.value = error instanceof Error ? error.message : 'Prometheus 查询失败';
  } finally {
    if (requestId === analyticsRequestId) {
      loading.value = false;
    }
  }
}

watch(
  () => [props.range, runtimeQueryTime.value, JSON.stringify(targetLabels.value), props.app?.availableMetrics.join('|')],
  () => {
    void loadStaticDataAnalytics();
  },
  { immediate: true },
);
</script>

<template>
  <div class="sup-static-data-panel">
    <el-card shadow="never">
      <template #header>
        <div class="section-header">
          <span>静态数据质量</span>
          <span class="section-meta">sup_id: 51 / 1 / 32</span>
        </div>
      </template>

      <el-alert
        v-if="errorText"
        :closable="false"
        :title="errorText"
        class="mb-3"
        type="error"
      />

      <el-table v-loading="loading" :data="supplierRows" stripe>
        <el-table-column fixed label="sup_id" min-width="90" prop="supId" />
        <el-table-column align="right" label="查询次数" min-width="110" prop="total" />
        <el-table-column align="right" label="返回房型" min-width="110" prop="successWithRooms" />
        <el-table-column align="right" label="未返回房型" min-width="120" prop="successWithoutRooms" />
        <el-table-column align="right" label="SRoomCode未匹配" min-width="150" prop="roomCodeNotFound" />
        <el-table-column align="right" label="MaxAdt缺失" min-width="120" prop="maxOccupancyMissing" />
        <el-table-column align="right" label="失败" min-width="90" prop="failed" />
        <el-table-column align="right" label="平均房型数" min-width="120" prop="avgRoomCount" />
        <el-table-column align="right" label="平均耗时" min-width="120" prop="avgDuration" />
        <el-table-column align="right" label="P50" min-width="100" prop="p50Duration" />
        <el-table-column align="right" label="P95" min-width="100" prop="p95Duration" />
      </el-table>
    </el-card>

    <div class="charts-grid mt-3">
      <el-card shadow="never">
        <template #header>静态数据状态趋势</template>
        <div v-loading="loading" class="chart-panel">
          <el-empty v-if="!hasOutcomeTrend" description="暂无趋势数据" :image-size="72" />
          <EchartsUI v-else ref="outcomeTrendRef" class="chart" />
        </div>
      </el-card>

      <el-card shadow="never">
        <template #header>静态数据 P95 耗时</template>
        <div v-loading="loading" class="chart-panel">
          <el-empty v-if="!hasLatencyTrend" description="暂无耗时数据" :image-size="72" />
          <EchartsUI v-else ref="latencyTrendRef" class="chart" />
        </div>
      </el-card>
    </div>

    <div class="tables-grid mt-3">
      <el-card shadow="never">
        <template #header>来源 / 结果分布</template>
        <el-table v-loading="loading" :data="sourceRows" stripe>
          <el-table-column label="sup_id" min-width="90" prop="supId" />
          <el-table-column label="source" min-width="110" prop="source" />
          <el-table-column label="outcome" min-width="180" prop="outcome" />
          <el-table-column align="right" label="次数" min-width="100" prop="count" />
        </el-table>
      </el-card>

      <el-card shadow="never">
        <template #header>失败错误码</template>
        <el-table v-loading="loading" :data="errorRows" stripe>
          <el-table-column label="sup_id" min-width="90" prop="supId" />
          <el-table-column label="source" min-width="110" prop="source" />
          <el-table-column label="error_code" min-width="180" prop="errorCode" />
          <el-table-column align="right" label="次数" min-width="100" prop="count" />
        </el-table>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.sup-static-data-panel {
  width: 100%;
}

.charts-grid,
.tables-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.section-header {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.section-meta {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.chart-panel {
  min-height: 320px;
}

.chart {
  height: 320px;
}

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1280px) {
  .charts-grid,
  .tables-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .section-header {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
