<script lang="ts" setup>
import type { PrometheusMatrixResult } from '#/api/prometheus/types';
import type { AppLatestMetricValue, AppViewModel, RuntimeRange } from '../../types';

type EChartsOption = Record<string, any>;

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, ref, watch } from 'vue';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import { formatHeartbeat, formatSeconds, getRuntimeQueryTime, getRuntimeTargetLabels } from '../../appPanelUtils';
import OTARoomBuilderPanel from './OTARoomBuilderPanel.vue';
import PipelineStagesPanel from './PipelineStagesPanel.vue';
import SupStaticDataPanel from './SupStaticDataPanel.vue';

defineOptions({ name: 'OTARoomBuilderDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
}>();

interface MetricCard {
  hint?: string;
  label: string;
  value: string;
}

interface ParsedMetricRow {
  labels: Record<string, string>;
  metricName: string;
  numericValue: null | number;
}

interface BreakdownRow {
  count: string;
  key: string;
  sortValue: number;
}

interface LineSeries {
  category?: 'error' | 'skip';
  color: string;
  data: Array<[number, number]>;
  key?: string;
  name: string;
}

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
  createdGauge: ['ota_room_builder_created_hotels_rooms', 'ota_room_builder_created_hotels'],
  createdTotal: ['ota_room_builder_created_rooms_total', 'ota_room_builder_created_total'],
  errorTotal: ['ota_room_builder_error_errors_total', 'ota_room_builder_error_total'],
  hotelTotal: ['ota_room_builder_hotel_hotels_total', 'ota_room_builder_hotel_total'],
  lastRunCreated: ['ota_room_builder_last_run_created_rooms', 'ota_room_builder_last_run_created'],
  pendingHotels: ['ota_room_builder_pending_hotels'],
  runTotal: ['ota_room_builder_run_runs_total', 'ota_room_builder_run_total'],
  skippedTotal: ['ota_room_builder_skipped_rooms_total', 'ota_room_builder_skipped_total'],
} as const;

const ERROR_STAGE_ORDER = [
  'incremental_scan',
  'full_sync_task_load',
  'sup_load',
  'ota_load',
  'map_load',
  'match_stage',
  'create_stage',
];

const SKIP_REASON_ORDER = [
  'already_matched',
  'max_adt_missing',
  'no_new_rooms',
  'plugin_missing',
  'supplier_rooms_empty',
];

const SERIES_COLORS = ['#ef4444', '#f59e0b', '#3b82f6', '#10b981', '#8b5cf6', '#14b8a6'] as const;

const client = new PrometheusClient();
const selectedRange = ref<RuntimeRange>('1h');
const analyticsLoading = ref(false);
const analyticsError = ref('');
const backlogChartRef = ref<EchartsUIType>();
const breakdownChartRef = ref<EchartsUIType>();
const backlogSeries = ref<LineSeries[]>([]);
const breakdownSeries = ref<LineSeries[]>([]);
const backlogLegendSelected = ref<Record<string, boolean>>({});
const breakdownLegendSelected = ref<Record<string, boolean>>({});
const selectedBreakdownFilter = ref<null | {
  category: 'error' | 'skip';
  key: string;
  name: string;
}>(null);

const {
  getChartInstance: getBacklogChartInstance,
  renderEcharts: renderBacklogChart,
} = useEcharts(backlogChartRef);
const {
  getChartInstance: getBreakdownChartInstance,
  renderEcharts: renderBreakdownChart,
} = useEcharts(breakdownChartRef);

function parseLabels(labelsText: string) {
  const result: Record<string, string> = {};
  for (const part of labelsText.split(', ')) {
    if (!part) {
      continue;
    }
    const index = part.indexOf('=');
    if (index < 0) {
      continue;
    }
    const key = part.slice(0, index).trim();
    const value = part.slice(index + 1).trim();
    if (key) {
      result[key] = value;
    }
  }
  return result;
}

function parseMetricValue(item: AppLatestMetricValue): ParsedMetricRow {
  const numericValue = Number(item.valueText);
  return {
    labels: parseLabels(item.labelsText),
    metricName: item.metricName,
    numericValue: Number.isFinite(numericValue) ? numericValue : null,
  };
}

function formatInteger(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 0 }).format(value);
}

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

function getRangeParams(range: RuntimeRange, queryTime?: null | number) {
  const step = RANGE_STEP_MAP[range];
  const baseTime =
    typeof queryTime === 'number' && Number.isFinite(queryTime)
      ? queryTime
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  const start = end - RANGE_SECONDS_MAP[range];
  return { end, start, step, window: `${Math.max(step * 4, 60)}s` };
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

function buildSingleSeries(name: string, color: string, results: PrometheusMatrixResult[], timeline: number[]): LineSeries {
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
  order: string[],
  labelMap: Record<string, string>,
  timeline: number[],
  category: 'error' | 'skip',
): LineSeries[] {
  const grouped = new Map<string, Map<number, number>>();
  for (const row of results) {
    const groupKey = row.metric[labelKey];
    if (!groupKey) {
      continue;
    }
    const points = grouped.get(groupKey) || new Map<number, number>();
    for (const [timestamp, rawValue] of row.values) {
      const numericValue = Number(rawValue);
      if (!Number.isFinite(numericValue)) {
        continue;
      }
      const millis = timestamp * 1000;
      points.set(millis, (points.get(millis) || 0) + numericValue);
    }
    grouped.set(groupKey, points);
  }

  return order
    .map((key, index) => ({
      category,
      color: SERIES_COLORS[index % SERIES_COLORS.length] ?? '#3b82f6',
      data: timeline.map((timestamp) => [timestamp, grouped.get(key)?.get(timestamp) || 0]) as Array<[number, number]>,
      key,
      name: labelMap[key] || key,
    }))
    .filter((item) => item.data.some(([, value]) => value !== 0));
}

function buildLineOption(
  series: LineSeries[],
  yAxisName: string,
  selectedSeriesName?: null | string,
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
      lineStyle: {
        opacity: !selectedSeriesName || selectedSeriesName === item.name ? 1 : 0.32,
        width: selectedSeriesName === item.name ? 3 : 2,
      },
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
          lines.push(`${row.marker}${row.seriesName}: ${formatInteger(Number(row.value?.[1] ?? 0))}`);
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
        formatter: (value: number) => formatInteger(value),
      },
      name: yAxisName,
      type: 'value',
    },
  };
}

function findMetricName(metricNames: readonly string[]) {
  const fallbackMetricName = metricNames[0] ?? '';
  const available = props.app?.availableMetrics || [];
  for (const metricName of metricNames) {
    if (available.includes(metricName)) {
      return metricName;
    }
  }
  return fallbackMetricName;
}

const metricRows = computed(() =>
  (props.app?.latestMetricValues || []).map(parseMetricValue),
);

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));

function getMetricRows(metricNames: readonly string[], labels: Record<string, string> = {}) {
  return metricRows.value.filter((row) => {
    if (!metricNames.includes(row.metricName as never)) {
      return false;
    }
    return Object.entries(labels).every(([key, value]) => row.labels[key] === value);
  });
}

function getMetricNumber(metricNames: readonly string[], labels: Record<string, string> = {}) {
  return getMetricRows(metricNames, labels)[0]?.numericValue ?? null;
}

function sumMetric(metricNames: readonly string[], labels: Record<string, string> = {}) {
  const rows = getMetricRows(metricNames, labels);
  if (rows.length === 0) {
    return null;
  }
  return rows.reduce((sum, row) => sum + (row.numericValue || 0), 0);
}

const createdTotal = computed(() => sumMetric(METRIC_NAME_ALIASES.createdTotal, { output_mode: 'post' }));
const createdGauge = computed(() => getMetricNumber(METRIC_NAME_ALIASES.createdGauge));
const lastRunCreated = computed(() => getMetricNumber(METRIC_NAME_ALIASES.lastRunCreated));
const pendingHotels = computed(() => getMetricNumber(METRIC_NAME_ALIASES.pendingHotels));
const hotelTotal = computed(() => getMetricNumber(METRIC_NAME_ALIASES.hotelTotal));
const runTotal = computed(() => getMetricNumber(METRIC_NAME_ALIASES.runTotal));

const allErrorRows = computed<BreakdownRow[]>(() =>
  getMetricRows(METRIC_NAME_ALIASES.errorTotal)
    .map((row) => ({
      count: formatInteger(row.numericValue),
      key: row.labels.stage || 'unknown',
      sortValue: row.numericValue || 0,
    }))
    .sort((a, b) => b.sortValue - a.sortValue),
);

const allSkippedRows = computed<BreakdownRow[]>(() =>
  getMetricRows(METRIC_NAME_ALIASES.skippedTotal)
    .map((row) => ({
      count: formatInteger(row.numericValue),
      key: row.labels.reason || 'unknown',
      sortValue: row.numericValue || 0,
    }))
    .sort((a, b) => b.sortValue - a.sortValue),
);

const latestErrorRows = computed<BreakdownRow[]>(() => {
  const filter = selectedBreakdownFilter.value;
  return filter?.category === 'error'
    ? allErrorRows.value.filter((row) => row.key === filter.key)
    : allErrorRows.value;
});

const latestSkippedRows = computed<BreakdownRow[]>(() => {
  const filter = selectedBreakdownFilter.value;
  return filter?.category === 'skip'
    ? allSkippedRows.value.filter((row) => row.key === filter.key)
    : allSkippedRows.value;
});

const overviewCards = computed<MetricCard[]>(() => [
  {
    hint: '累计创建母房型总数',
    label: '已创建母房型',
    value: formatInteger(createdTotal.value),
  },
  {
    hint: '该 gauge 名字带 hotels，但实际单位是 rooms',
    label: '当前创建计数',
    value: formatInteger(createdGauge.value),
  },
  {
    label: '最近一批创建数',
    value: formatInteger(lastRunCreated.value),
  },
  {
    label: '待处理酒店数',
    value: formatInteger(pendingHotels.value),
  },
  {
    label: '累计装载酒店数',
    value: formatInteger(hotelTotal.value),
  },
  {
    label: '运行次数',
    value: formatInteger(runTotal.value),
  },
]);

const hasBacklogTrend = computed(() => backlogSeries.value.some((item) => item.data.some(([, value]) => value !== 0)));
const hasBreakdownTrend = computed(() => breakdownSeries.value.some((item) => item.data.some(([, value]) => value !== 0)));
const selectedBreakdownSeriesName = computed(() => selectedBreakdownFilter.value?.name ?? null);

function toggleBreakdownFilter(seriesName: string) {
  const series = breakdownSeries.value.find((item) => item.name === seriesName);
  if (!series?.category || !series.key) {
    return;
  }

  const current = selectedBreakdownFilter.value;
  selectedBreakdownFilter.value =
    current?.category === series.category && current.key === series.key
      ? null
      : {
          category: series.category,
          key: series.key,
          name: series.name,
        };

  const chart = getBreakdownChartInstance();
  if (chart && hasBreakdownTrend.value) {
    chart.setOption(
      buildLineOption(
        breakdownSeries.value,
        'count/window',
        selectedBreakdownSeriesName.value,
        breakdownLegendSelected.value,
      ),
    );
  }
}

function bindBreakdownChartClick() {
  const chart = getBreakdownChartInstance();
  if (!chart) {
    return;
  }
  chart.off('click');
  chart.on('click', (params: any) => {
    if (typeof params?.seriesName === 'string') {
      toggleBreakdownFilter(params.seriesName);
    }
  });
}

function bindLegendState(
  getChartInstance: () => any,
  legendSelected: typeof backlogLegendSelected,
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

function clearBreakdownFilter() {
  selectedBreakdownFilter.value = null;
  const chart = getBreakdownChartInstance();
  if (chart && hasBreakdownTrend.value) {
    chart.setOption(buildLineOption(breakdownSeries.value, 'count/window', null, breakdownLegendSelected.value));
  }
}

async function queryRange(query: string) {
  const context = getRangeParams(selectedRange.value, runtimeQueryTime.value);
  return client.queryRange({
    end: context.end,
    query,
    start: context.start,
    step: context.step,
  });
}

async function loadAnalytics() {
  if (!props.app) {
    backlogSeries.value = [];
    breakdownSeries.value = [];
    analyticsError.value = '';
    return;
  }

  const labels = targetLabels.value;
  if (Object.keys(labels).length === 0) {
    backlogSeries.value = [];
    breakdownSeries.value = [];
    analyticsError.value = '';
    return;
  }

  analyticsLoading.value = true;
  analyticsError.value = '';

  try {
    const context = getRangeParams(selectedRange.value, runtimeQueryTime.value);
    const timeline = Array.from(
      { length: Math.floor((context.end - context.start) / context.step) + 1 },
      (_, index) => (context.start + index * context.step) * 1000,
    );

    const createdMetric = findMetricName(METRIC_NAME_ALIASES.createdTotal);
    const pendingMetric = findMetricName(METRIC_NAME_ALIASES.pendingHotels);
    const hotelMetric = findMetricName(METRIC_NAME_ALIASES.hotelTotal);
    const skippedMetric = findMetricName(METRIC_NAME_ALIASES.skippedTotal);
    const errorMetric = findMetricName(METRIC_NAME_ALIASES.errorTotal);

    const [pendingResults, createdResults, hotelResults, skippedResults, errorResults] = await Promise.all([
      queryRange(`max(${buildMetricSelector(pendingMetric, labels)})`),
      queryRange(`sum(increase(${buildMetricSelector(createdMetric, labels, ['output_mode="post"'])}[${context.window}]))`),
      queryRange(`sum(increase(${buildMetricSelector(hotelMetric, labels)}[${context.window}]))`),
      queryRange(`sum by (reason) (increase(${buildMetricSelector(skippedMetric, labels)}[${context.window}]))`),
      queryRange(`sum by (stage) (increase(${buildMetricSelector(errorMetric, labels)}[${context.window}]))`),
    ]);

    backlogSeries.value = [
      buildSingleSeries('待处理酒店', '#ef4444', pendingResults, timeline),
      buildSingleSeries('新增母房型', '#10b981', createdResults, timeline),
      buildSingleSeries('装载酒店', '#3b82f6', hotelResults, timeline),
    ];

    breakdownSeries.value = [
      ...buildGroupedSeries(
        skippedResults,
        'reason',
        SKIP_REASON_ORDER,
        {
          already_matched: '跳过-已匹配',
          max_adt_missing: '跳过-MaxAdt缺失',
          no_new_rooms: '跳过-无新房型',
          plugin_missing: '跳过-插件缺失',
          supplier_rooms_empty: '跳过-sup为空',
        },
        timeline,
        'skip',
      ),
      ...buildGroupedSeries(
        errorResults,
        'stage',
        ERROR_STAGE_ORDER,
        {
          incremental_scan: '错误-incremental_scan',
          full_sync_task_load: '错误-fullsync任务',
          sup_load: '错误-sup_load',
          ota_load: '错误-ota_load',
          map_load: '错误-map_load',
          match_stage: '错误-match_stage',
          create_stage: '错误-create_stage',
        },
        timeline,
        'error',
      ),
    ];

    await nextTick();
    if (hasBacklogTrend.value) {
      await renderBacklogChart(buildLineOption(backlogSeries.value, 'count', null, backlogLegendSelected.value));
      bindLegendState(getBacklogChartInstance, backlogLegendSelected);
    }
    if (hasBreakdownTrend.value) {
      await renderBreakdownChart(
        buildLineOption(
          breakdownSeries.value,
          'count/window',
          selectedBreakdownSeriesName.value,
          breakdownLegendSelected.value,
        ),
      );
      bindLegendState(getBreakdownChartInstance, breakdownLegendSelected);
      bindBreakdownChartClick();
    }
  } catch (error) {
    backlogSeries.value = [];
    breakdownSeries.value = [];
    analyticsError.value = error instanceof Error ? error.message : 'Prometheus 查询失败';
  } finally {
    analyticsLoading.value = false;
  }
}

watch(
  () => [selectedRange.value, runtimeQueryTime.value, JSON.stringify(targetLabels.value)],
  () => {
    void loadAnalytics();
  },
  { immediate: true },
);
</script>

<template>
  <div class="ota-room-builder-dashboard-panel">
    <div class="metrics-grid">
      <el-card shadow="never">
        <div class="metric-label">最近心跳</div>
        <div class="metric-value">{{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">运行时长</div>
        <div class="metric-value">{{ formatSeconds(props.app?.uptimeSeconds ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">当前 Inflight</div>
        <div class="metric-value">{{ formatInteger(props.app?.inflight ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">指标条数</div>
        <div class="metric-value">{{ formatInteger(metricRows.length) }}</div>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>核心指标</template>
      <div class="summary-grid">
        <div v-for="item in overviewCards" :key="item.label" class="summary-item">
          <div class="summary-label">{{ item.label }}</div>
          <div class="summary-value">{{ item.value }}</div>
          <div v-if="item.hint" class="summary-hint">{{ item.hint }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>
        <div class="section-header">
          <span>趋势</span>
          <el-radio-group v-model="selectedRange" size="small">
            <el-radio-button label="15m">15m</el-radio-button>
            <el-radio-button label="1h">1h</el-radio-button>
            <el-radio-button label="6h">6h</el-radio-button>
            <el-radio-button label="24h">24h</el-radio-button>
          </el-radio-group>
        </div>
      </template>

      <el-alert
        v-if="analyticsError"
        :closable="false"
        :title="analyticsError"
        class="mb-3"
        type="error"
      />

      <div v-loading="analyticsLoading" class="charts-grid">
        <div class="chart-panel">
          <div class="chart-title">创建 / 积压趋势</div>
          <el-empty v-if="!hasBacklogTrend" description="暂无趋势数据" :image-size="72" />
          <EchartsUI v-else ref="backlogChartRef" class="chart" />
        </div>
        <div class="chart-panel">
          <div class="chart-title">跳过 / 错误趋势</div>
          <el-empty v-if="!hasBreakdownTrend" description="暂无趋势数据" :image-size="72" />
          <EchartsUI v-else ref="breakdownChartRef" class="chart" />
        </div>
      </div>
    </el-card>

    <PipelineStagesPanel class="mt-3" :app="props.app" :range="selectedRange" />

    <SupStaticDataPanel class="mt-3" :app="props.app" :range="selectedRange" />

    <OTARoomBuilderPanel class="mt-3" :app="props.app" :range="selectedRange" />

    <div v-if="selectedBreakdownFilter" class="active-filter mt-3">
      <span>当前过滤：{{ selectedBreakdownFilter.name }}</span>
      <el-button link size="small" type="primary" @click="clearBreakdownFilter">清除</el-button>
    </div>

    <div class="tables-grid mt-3">
      <el-card shadow="never">
        <template #header>错误分布</template>
        <el-table :data="latestErrorRows" stripe>
          <el-table-column label="阶段" min-width="180" prop="key" />
          <el-table-column align="right" label="累计次数" min-width="120" prop="count" />
        </el-table>
      </el-card>

      <el-card shadow="never">
        <template #header>跳过分布</template>
        <el-table :data="latestSkippedRows" stripe>
          <el-table-column label="原因" min-width="180" prop="key" />
          <el-table-column align="right" label="累计次数" min-width="120" prop="count" />
        </el-table>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.ota-room-builder-dashboard-panel {
  width: 100%;
}

.metrics-grid,
.summary-grid,
.charts-grid,
.tables-grid {
  display: grid;
  gap: 12px;
}

.metrics-grid,
.summary-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.charts-grid,
.tables-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.metric-label,
.summary-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-value,
.summary-value {
  margin-top: 6px;
  font-size: 18px;
  font-weight: 700;
  word-break: break-all;
}

.summary-item {
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.summary-hint {
  margin-top: 6px;
  font-size: 12px;
  line-height: 1.5;
  color: var(--el-text-color-secondary);
}

.chart-panel {
  padding: 12px;
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
}

.chart-title {
  margin-bottom: 10px;
  font-size: 14px;
  font-weight: 600;
}

.chart {
  height: 320px;
}

.section-header {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.active-filter {
  display: flex;
  gap: 8px;
  align-items: center;
  padding: 8px 12px;
  font-size: 13px;
  color: var(--el-text-color-regular);
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1280px) {
  .metrics-grid,
  .summary-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .charts-grid,
  .tables-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .metrics-grid,
  .summary-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
