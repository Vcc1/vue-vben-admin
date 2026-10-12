<script lang="ts" setup>
import type { AppLatestMetricValue, AppViewModel } from '../../types';

import type { PrometheusMatrixResult } from '#/api/prometheus/types';

type EChartsOption = Record<string, any>;
type HistoryRange = '7d' | '30d' | '90d';

import {
  EchartsUI,
  type EchartsUIType,
  useEcharts,
} from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, ref, watch } from 'vue';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import { getRuntimeTargetLabels } from '../../appPanelUtils';

defineOptions({ name: 'RoomMatchingStatisticsDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
  refreshKey?: number;
}>();

interface DistributionRow {
  count: number;
  label: string;
}

interface HistoryPoint {
  completedAt: number;
  durationMs: null | number;
  expiredRows: null | number;
  noExpiryRows: null | number;
  runVersion: null | number;
  succeeded: boolean;
  totalRows: null | number;
}

interface HistoryTableRow {
  completedAtText: string;
  durationText: string;
  expiredRowsText: string;
  noExpiryRowsText: string;
  runVersionText: string;
  status: string;
  totalRowsText: string;
}

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

interface SeriesSnapshot {
  timestamp: number;
  value: number;
}

const WORKER_STATE_MAP: Record<number, { text: string; type: string }> = {
  1: { text: '执行中', type: 'warning' },
  2: { text: '成功完成', type: 'success' },
  3: { text: '失败', type: 'danger' },
  4: { text: '等待下一次调度', type: 'info' },
};

const HISTORY_RANGE_MAP: Record<HistoryRange, { label: string; seconds: number; step: number }> = {
  '7d': { label: '7天', seconds: 7 * 24 * 60 * 60, step: 5 * 60 },
  '30d': { label: '30天', seconds: 30 * 24 * 60 * 60, step: 15 * 60 },
  '90d': { label: '90天', seconds: 90 * 24 * 60 * 60, step: 30 * 60 },
};

const client = new PrometheusClient();

const chartRef = ref<EchartsUIType>();
const historyChartRef = ref<EchartsUIType>();
const selectedHistoryRange = ref<HistoryRange>('30d');
const historyLoading = ref(false);
const historyError = ref('');
const historyPoints = ref<HistoryPoint[]>([]);

const { renderEcharts } = useEcharts(chartRef);
const { renderEcharts: renderHistoryEcharts } = useEcharts(historyChartRef);

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
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 0 }).format(
    value,
  );
}

function formatDuration(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  if (value >= 1000) {
    return `${new Intl.NumberFormat('zh-CN', {
      maximumFractionDigits: 2,
      minimumFractionDigits: 0,
    }).format(value / 1000)} s`;
  }
  return `${new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: 0,
    minimumFractionDigits: 0,
  }).format(value)} ms`;
}

function formatUnixTime(value: null | number) {
  if (value === null || Number.isNaN(value) || value <= 0) {
    return '--';
  }
  return dayjs.unix(value).format('YYYY-MM-DD HH:mm:ss');
}

function formatCompletedFlag(value: null | number, state: null | number) {
  if (value === 1) {
    return '已完成';
  }
  if (state === 1) {
    return '执行中';
  }
  return '未完成';
}

function formatSucceededFlag(value: null | number, completed: null | number) {
  if (value === 1) {
    return '成功';
  }
  if (completed === 0) {
    return '未完成';
  }
  return '失败';
}

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function buildSelector(labels: Record<string, string>) {
  const parts = Object.entries(labels)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `{${parts.join(',')}}`;
}

function buildMetricSelector(metricName: string, labels: Record<string, string>) {
  return `${metricName}${buildSelector(labels)}`;
}

function collapseSeries(results: PrometheusMatrixResult[]) {
  const map = new Map<number, number>();

  for (const row of results) {
    for (const [timestamp, rawValue] of row.values) {
      const numericValue = Number(rawValue);
      if (!Number.isFinite(numericValue)) {
        continue;
      }
      const previous = map.get(timestamp);
      if (previous === undefined || numericValue > previous) {
        map.set(timestamp, numericValue);
      }
    }
  }

  return [...map.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([timestamp, value]) => ({ timestamp, value }));
}

function readSnapshotValue(series: SeriesSnapshot[], timestamp: number) {
  let result: null | number = null;
  for (const point of series) {
    if (point.timestamp > timestamp) {
      break;
    }
    result = point.value;
  }
  return result;
}

function buildDistributionChartOption(rows: DistributionRow[]): EChartsOption {
  return {
    color: ['#3b82f6'],
    grid: {
      bottom: 20,
      containLabel: true,
      left: 16,
      right: 20,
      top: 24,
    },
    tooltip: {
      appendToBody: true,
      backgroundColor: 'rgba(18, 18, 18, 0.88)',
      borderWidth: 0,
      formatter: (params: any) => {
        const value = Array.isArray(params) ? params[0] : params;
        return `${value?.name || '--'}<br/>映射条目数: ${formatInteger(
          Number(value?.value ?? 0),
        )}`;
      },
      textStyle: {
        color: '#f5f7fa',
      },
      trigger: 'axis',
    },
    xAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
      },
      data: rows.map((row) => row.label),
      type: 'category',
    },
    yAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter: (value: number) => formatInteger(value),
      },
      name: '映射条目数',
      type: 'value',
    },
    series: [
      {
        barMaxWidth: 42,
        data: rows.map((row) => row.count),
        itemStyle: {
          borderRadius: [6, 6, 0, 0],
        },
        type: 'bar',
      },
    ],
  };
}

function buildHistoryChartOption(points: HistoryPoint[]): EChartsOption {
  const totalRowsSeries = points.map((point) => [
    point.completedAt * 1000,
    point.totalRows,
  ]);
  const expiredRowsSeries = points.map((point) => [
    point.completedAt * 1000,
    point.expiredRows,
  ]);
  const noExpiryRowsSeries = points.map((point) => [
    point.completedAt * 1000,
    point.noExpiryRows,
  ]);
  const failedPoints = points
    .filter((point) => !point.succeeded)
    .map((point) => [point.completedAt * 1000, point.totalRows ?? 0]);

  return {
    color: ['#2563eb', '#f59e0b', '#14b8a6', '#ef4444'],
    grid: {
      bottom: 20,
      containLabel: true,
      left: 16,
      right: 20,
      top: 40,
    },
    legend: {
      icon: 'roundRect',
      itemHeight: 8,
      itemWidth: 12,
      type: 'scroll',
    },
    series: [
      {
        data: totalRowsSeries,
        lineStyle: { width: 2 },
        name: '映射条目总数',
        showSymbol: true,
        symbolSize: 8,
        type: 'line',
      },
      {
        data: expiredRowsSeries,
        lineStyle: { width: 2 },
        name: '已过期条目数',
        showSymbol: true,
        symbolSize: 8,
        type: 'line',
      },
      {
        data: noExpiryRowsSeries,
        lineStyle: { width: 2 },
        name: '无过期时间条目数',
        showSymbol: true,
        symbolSize: 8,
        type: 'line',
      },
      {
        data: failedPoints,
        itemStyle: {
          color: '#ef4444',
        },
        name: '失败完成轮次',
        symbol: 'diamond',
        symbolSize: 10,
        type: 'scatter',
      },
    ],
    tooltip: {
      appendToBody: true,
      backgroundColor: 'rgba(18, 18, 18, 0.88)',
      borderWidth: 0,
      formatter: (params: any[]) => {
        const rows = Array.isArray(params) ? params : [];
        const completedAt = rows[0]?.axisValue
          ? dayjs(rows[0].axisValue).format('YYYY-MM-DD HH:mm:ss')
          : '--';
        const point = points.find(
          (item) => item.completedAt * 1000 === Number(rows[0]?.axisValue),
        );
        const lines = [`完成时间: ${completedAt}`];
        if (point) {
          lines.push(`状态: ${point.succeeded ? '成功' : '失败'}`);
          lines.push(`轮次: ${formatInteger(point.runVersion)}`);
          lines.push(`最近耗时: ${formatDuration(point.durationMs)}`);
        }
        for (const row of rows) {
          if (row.seriesName === '失败完成轮次') {
            continue;
          }
          lines.push(
            `${row.marker}${row.seriesName}: ${formatInteger(
              Number(row.value?.[1] ?? 0),
            )}`,
          );
        }
        if (point && !point.succeeded) {
          lines.push('该点为失败完成轮次，红色菱形仅用于标记完成事件。');
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
        formatter: (value: number) => dayjs(value).format('MM-DD HH:mm'),
        hideOverlap: true,
      },
      type: 'time',
    },
    yAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter: (value: number) => formatInteger(value),
      },
      name: '映射条目数',
      type: 'value',
    },
  };
}

async function renderDistributionChart() {
  await nextTick();
  renderEcharts(buildDistributionChartOption(expiringDistribution.value));
}

async function renderHistoryChart() {
  await nextTick();
  if (historyPoints.value.length === 0) {
    return;
  }
  renderHistoryEcharts(buildHistoryChartOption(historyPoints.value));
}

const metricRows = computed(() =>
  (props.app?.latestMetricValues || []).map(parseMetricValue),
);

const targetLabels = computed(() => getRuntimeTargetLabels(props.app));

function getMetricRows(metricName: string, labels: Record<string, string> = {}) {
  return metricRows.value.filter((row) => {
    if (row.metricName !== metricName) {
      return false;
    }
    return Object.entries(labels).every(
      ([key, value]) => row.labels[key] === value,
    );
  });
}

function sumMetric(metricName: string, labels: Record<string, string> = {}) {
  const rows = getMetricRows(metricName, labels);
  if (rows.length === 0) {
    return null;
  }
  return rows.reduce((sum, row) => sum + (row.numericValue || 0), 0);
}

function getMetricNumber(metricName: string, labels: Record<string, string> = {}) {
  return getMetricRows(metricName, labels)[0]?.numericValue ?? null;
}

const workerState = computed(() =>
  Math.trunc(getMetricNumber('room_matching_statistics_worker_state') ?? 0),
);
const runVersion = computed(() =>
  getMetricNumber('room_matching_statistics_run_version'),
);
const currentRunStartedAt = computed(() =>
  getMetricNumber('room_matching_statistics_current_run_started_at'),
);
const lastRunCompletedAt = computed(() =>
  getMetricNumber('room_matching_statistics_last_run_completed_at'),
);
const lastSuccessCompletedAt = computed(() =>
  getMetricNumber('room_matching_statistics_last_success_completed_at'),
);
const lastRunCompleted = computed(() =>
  getMetricNumber('room_matching_statistics_last_run_completed'),
);
const lastRunSucceeded = computed(() =>
  getMetricNumber('room_matching_statistics_last_run_succeeded'),
);
const lastErrorAt = computed(() =>
  getMetricNumber('room_matching_statistics_last_error_at'),
);
const lastDurationMs = computed(() =>
  getMetricNumber('room_matching_statistics_last_duration_ms_milliseconds'),
);

const stateInfo = computed(
  () => WORKER_STATE_MAP[workerState.value] || { text: '未知状态', type: 'info' },
);

const statusCards = computed<MetricCard[]>(() => [
  {
    hint: `worker_state = ${workerState.value || 0}`,
    label: '当前状态',
    value: stateInfo.value.text,
  },
  {
    label: '当前轮次',
    value: formatInteger(runVersion.value),
  },
  {
    label: '本轮开始时间',
    value: formatUnixTime(currentRunStartedAt.value),
  },
  {
    hint: formatCompletedFlag(lastRunCompleted.value, workerState.value),
    label: '最近完成时间',
    value: formatUnixTime(lastRunCompletedAt.value),
  },
  {
    label: '最近成功时间',
    value: formatUnixTime(lastSuccessCompletedAt.value),
  },
  {
    hint:
      lastErrorAt.value && lastErrorAt.value > 0
        ? '最近一次失败结束时间'
        : '尚无失败记录',
    label: '最近错误时间',
    value: formatUnixTime(lastErrorAt.value),
  },
  {
    hint: formatSucceededFlag(lastRunSucceeded.value, lastRunCompleted.value),
    label: '最近耗时',
    value: formatDuration(lastDurationMs.value),
  },
  {
    label: '最近一轮结果',
    value: formatSucceededFlag(lastRunSucceeded.value, lastRunCompleted.value),
  },
]);

const summaryCards = computed<MetricCard[]>(() => [
  {
    hint: '所有 Redis RM_* key 下 SupRooms 映射条目数总和',
    label: '映射条目总数',
    value: formatInteger(sumMetric('room_matching_statistics_total_rows')),
  },
  {
    hint: 'TTL 已过期的 key 下全部映射条目数',
    label: '已过期条目数',
    value: formatInteger(
      sumMetric('room_matching_statistics_expired_total_rows'),
    ),
  },
  {
    hint: '历史指标名 unknown_create_date，当前真实含义是无 TTL',
    label: '无过期时间条目数',
    value: formatInteger(
      sumMetric('room_matching_statistics_unknown_create_date_total_rows'),
    ),
  },
]);

const runCounterCards = computed<MetricCard[]>(() => [
  {
    label: 'started',
    value: formatInteger(
      sumMetric('room_matching_statistics_run_runs_total', {
        outcome: 'started',
      }),
    ),
  },
  {
    label: 'completed',
    value: formatInteger(
      sumMetric('room_matching_statistics_run_runs_total', {
        outcome: 'completed',
      }),
    ),
  },
  {
    label: 'error',
    value: formatInteger(
      sumMetric('room_matching_statistics_run_runs_total', {
        outcome: 'error',
      }),
    ),
  },
]);

const expiringDistribution = computed<DistributionRow[]>(() =>
  Array.from({ length: 7 }, (_, index) => {
    const days = `${index + 1}`;
    return {
      count:
        sumMetric('room_matching_statistics_expiring_days_rows', { days }) ?? 0,
      label: `${days} 天内`,
    };
  }),
);

const runningHint = computed(() => {
  if (workerState.value !== 1 || lastRunCompleted.value !== 0) {
    return '';
  }
  return '本轮仍在执行中。started 已增加但 last_run_completed = 0、last_run_completed_at = 0 属于正常中间状态，不表示统计结果为 0。';
});

const historyCardTitle = computed(
  () => `历史完成记录（${HISTORY_RANGE_MAP[selectedHistoryRange.value].label}）`,
);

const historyTableRows = computed<HistoryTableRow[]>(() =>
  [...historyPoints.value]
    .sort((a, b) => b.completedAt - a.completedAt)
    .map((point) => ({
      completedAtText: formatUnixTime(point.completedAt),
      durationText: formatDuration(point.durationMs),
      expiredRowsText: formatInteger(point.expiredRows),
      noExpiryRowsText: formatInteger(point.noExpiryRows),
      runVersionText: formatInteger(point.runVersion),
      status: point.succeeded ? '成功' : '失败',
      totalRowsText: formatInteger(point.totalRows),
    })),
);

async function queryHistoryMetric(metricName: string, start: number, end: number, step: number) {
  const labels = targetLabels.value;
  if (Object.keys(labels).length === 0) {
    return [] as PrometheusMatrixResult[];
  }

  const selector = buildMetricSelector(metricName, labels);
  const query = `max(${selector})`;

  return client.queryRange({
    end,
    query,
    start,
    step,
  });
}

async function loadHistory() {
  if (!props.app) {
    historyPoints.value = [];
    historyError.value = '';
    return;
  }

  const labels = targetLabels.value;
  if (Object.keys(labels).length === 0) {
    historyPoints.value = [];
    historyError.value = '';
    return;
  }

  historyLoading.value = true;
  historyError.value = '';

  const rangeConfig = HISTORY_RANGE_MAP[selectedHistoryRange.value];
  const end = Math.floor(Date.now() / 1000);
  const start = end - rangeConfig.seconds;

  try {
    const [
      completedAtResults,
      succeededResults,
      totalRowsResults,
      expiredRowsResults,
      noExpiryRowsResults,
      durationResults,
      runVersionResults,
    ] = await Promise.all([
      queryHistoryMetric(
        'room_matching_statistics_last_run_completed_at',
        start,
        end,
        rangeConfig.step,
      ),
      queryHistoryMetric(
        'room_matching_statistics_last_run_succeeded',
        start,
        end,
        rangeConfig.step,
      ),
      queryHistoryMetric(
        'room_matching_statistics_total_rows',
        start,
        end,
        rangeConfig.step,
      ),
      queryHistoryMetric(
        'room_matching_statistics_expired_total_rows',
        start,
        end,
        rangeConfig.step,
      ),
      queryHistoryMetric(
        'room_matching_statistics_unknown_create_date_total_rows',
        start,
        end,
        rangeConfig.step,
      ),
      queryHistoryMetric(
        'room_matching_statistics_last_duration_ms_milliseconds',
        start,
        end,
        rangeConfig.step,
      ),
      queryHistoryMetric(
        'room_matching_statistics_run_version',
        start,
        end,
        rangeConfig.step,
      ),
    ]);

    const completedAtSeries = collapseSeries(completedAtResults);
    const succeededSeries = collapseSeries(succeededResults);
    const totalRowsSeries = collapseSeries(totalRowsResults);
    const expiredRowsSeries = collapseSeries(expiredRowsResults);
    const noExpiryRowsSeries = collapseSeries(noExpiryRowsResults);
    const durationSeries = collapseSeries(durationResults);
    const runVersionSeries = collapseSeries(runVersionResults);

    const startMillis = start * 1000;
    const endMillis = end * 1000;
    const seenCompletedAt = new Set<number>();
    const nextPoints: HistoryPoint[] = [];

    for (const point of completedAtSeries) {
      const completedAtValue = Math.trunc(point.value);
      if (completedAtValue <= 0) {
        continue;
      }

      const completedAtMillis = completedAtValue * 1000;
      if (completedAtMillis < startMillis || completedAtMillis > endMillis) {
        continue;
      }

      if (seenCompletedAt.has(completedAtValue)) {
        continue;
      }
      seenCompletedAt.add(completedAtValue);

      nextPoints.push({
        completedAt: completedAtValue,
        durationMs: readSnapshotValue(durationSeries, point.timestamp),
        expiredRows: readSnapshotValue(expiredRowsSeries, point.timestamp),
        noExpiryRows: readSnapshotValue(noExpiryRowsSeries, point.timestamp),
        runVersion: readSnapshotValue(runVersionSeries, point.timestamp),
        succeeded: readSnapshotValue(succeededSeries, point.timestamp) === 1,
        totalRows: readSnapshotValue(totalRowsSeries, point.timestamp),
      });
    }

    historyPoints.value = nextPoints.sort((a, b) => a.completedAt - b.completedAt);
    await renderHistoryChart();
  } catch (error) {
    historyPoints.value = [];
    historyError.value =
      error instanceof Error ? error.message : 'Prometheus 历史查询失败';
  } finally {
    historyLoading.value = false;
  }
}

watch(
  () => [JSON.stringify(expiringDistribution.value), props.refreshKey || 0],
  () => {
    void renderDistributionChart();
  },
  { immediate: true },
);

watch(
  () => [
    props.app?.instance || '',
    props.app?.jobs[0] || '',
    selectedHistoryRange.value,
    props.refreshKey || 0,
  ],
  () => {
    void loadHistory();
  },
  { immediate: true },
);
</script>

<template>
  <div class="room-matching-statistics-panel">
    <el-alert
      :closable="false"
      class="mb-3"
      title="本页所有 total_rows / expired_total_rows / expiring_days_rows 统一表示映射条目数，不是 Redis key 数量、酒店数量或 MSSQL 行数。"
      type="info"
    />

    <el-alert
      v-if="runningHint"
      :closable="false"
      :title="runningHint"
      class="mb-3"
      type="warning"
    />

    <el-card shadow="never">
      <template #header>运行状态</template>
      <div class="cards-grid status-grid">
        <div v-for="item in statusCards" :key="item.label" class="metric-card">
          <div class="metric-label">{{ item.label }}</div>
          <div
            class="metric-value"
            :class="{
              'metric-value--warning':
                item.label === '当前状态' && stateInfo.type === 'warning',
              'metric-value--danger':
                item.label === '当前状态' && stateInfo.type === 'danger',
              'metric-value--success':
                item.label === '当前状态' && stateInfo.type === 'success',
            }"
          >
            {{ item.value }}
          </div>
          <div v-if="item.hint" class="metric-hint">{{ item.hint }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>汇总卡片</template>
      <div class="cards-grid summary-grid">
        <div v-for="item in summaryCards" :key="item.label" class="metric-card">
          <div class="metric-label">{{ item.label }}</div>
          <div class="metric-value">{{ item.value }}</div>
          <div v-if="item.hint" class="metric-hint">{{ item.hint }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>即将过期分布</template>
      <div class="chart-panel">
        <EchartsUI ref="chartRef" class="chart" />
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>运行次数统计</template>
      <div class="cards-grid counter-grid">
        <div
          v-for="item in runCounterCards"
          :key="item.label"
          class="metric-card"
        >
          <div class="metric-label">{{ item.label }}</div>
          <div class="metric-value">{{ item.value }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>
        <div class="section-header">
          <span>{{ historyCardTitle }}</span>
          <el-radio-group v-model="selectedHistoryRange" size="small">
            <el-radio-button label="7d">7天</el-radio-button>
            <el-radio-button label="30d">30天</el-radio-button>
            <el-radio-button label="90d">90天</el-radio-button>
          </el-radio-group>
        </div>
      </template>

      <el-alert
        :closable="false"
        class="mb-3"
        title="历史面板按 last_run_completed_at 去重，只保留完成态快照，不展示执行过程的中间值。轮次仅作参考，程序重启后可能从 0 重新开始。"
        type="info"
      />

      <el-alert
        v-if="historyError"
        :closable="false"
        :title="historyError"
        class="mb-3"
        type="error"
      />

      <div v-loading="historyLoading">
        <div v-if="historyPoints.length > 0" class="chart-panel">
          <EchartsUI ref="historyChartRef" class="chart" />
        </div>
        <el-empty
          v-else-if="!historyLoading"
          description="当前时间范围内没有已完成的历史记录"
          :image-size="72"
        />

        <el-table
          v-if="historyTableRows.length > 0"
          :data="historyTableRows"
          class="mt-3"
          stripe
        >
          <el-table-column label="完成时间" min-width="180" prop="completedAtText" />
          <el-table-column label="状态" min-width="100" prop="status" />
          <el-table-column
            align="right"
            label="轮次"
            min-width="100"
            prop="runVersionText"
          />
          <el-table-column
            align="right"
            label="映射条目总数"
            min-width="140"
            prop="totalRowsText"
          />
          <el-table-column
            align="right"
            label="已过期条目数"
            min-width="140"
            prop="expiredRowsText"
          />
          <el-table-column
            align="right"
            label="无过期时间条目数"
            min-width="160"
            prop="noExpiryRowsText"
          />
          <el-table-column
            align="right"
            label="最近耗时"
            min-width="120"
            prop="durationText"
          />
        </el-table>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.room-matching-statistics-panel {
  width: 100%;
}

.cards-grid {
  display: grid;
  gap: 12px;
}

.status-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.summary-grid,
.counter-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.metric-card {
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
}

.metric-label,
.metric-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-value {
  margin-top: 6px;
  font-size: 18px;
  font-weight: 700;
  word-break: break-all;
}

.metric-value--success {
  color: var(--el-color-success);
}

.metric-value--warning {
  color: var(--el-color-warning);
}

.metric-value--danger {
  color: var(--el-color-danger);
}

.metric-hint {
  margin-top: 6px;
  line-height: 1.5;
}

.chart-panel {
  padding: 12px;
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
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

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1280px) {
  .status-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .summary-grid,
  .counter-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .status-grid,
  .summary-grid,
  .counter-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .section-header {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
