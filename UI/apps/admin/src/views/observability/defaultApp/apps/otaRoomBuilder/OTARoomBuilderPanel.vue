<script lang="ts" setup>
import type { PrometheusValue, PrometheusVectorResult } from '#/api/prometheus/types';
import type { AppLatestMetricValue, AppViewModel, RuntimeRange } from '../../types';

import { computed, ref, watch } from 'vue';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import { getRuntimeQueryTime, getRuntimeTargetLabels } from '../../appPanelUtils';

defineOptions({ name: 'OTARoomBuilderPanel' });

const props = defineProps<{
  app: AppViewModel | null;
  range: RuntimeRange;
}>();

interface MetricCard {
  label: string;
  value: string;
}

interface ParsedMetricRow {
  labels: Record<string, string>;
  metricName: string;
  numericValue: null | number;
}

interface LatestMetricRow {
  labels: string;
  metricName: string;
  value: string;
}

interface OutcomeRow {
  count: string;
  outcome: string;
  sortValue: number;
  supId: string;
}

const RANGE_STEP_MAP: Record<RuntimeRange, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

const METRIC_NAME_ALIASES = {
  duration: ['ota_room_builder_duration_ms_milliseconds', 'ota_room_builder_duration_ms'],
  roomMatchTotal: ['ota_room_builder_static_data_room_match_rooms_total', 'ota_room_builder_static_data_room_match_total'],
} as const;

const client = new PrometheusClient();
const loading = ref(false);
const errorText = ref('');
const durationCards = ref<MetricCard[]>([]);
const roomMatchRows = ref<OutcomeRow[]>([]);

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));

const context = computed(() => {
  const step = RANGE_STEP_MAP[props.range];
  const baseTime =
    typeof runtimeQueryTime.value === 'number' && Number.isFinite(runtimeQueryTime.value)
      ? runtimeQueryTime.value
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  return {
    end,
    wholeWindow: props.range,
  };
});

const metricRows = computed(() =>
  (props.app?.latestMetricValues || []).map(parseMetricValue),
);

const latestMetricRows = computed<LatestMetricRow[]>(() =>
  metricRows.value
    .filter((row) => row.metricName.startsWith('ota_room_builder_'))
    .map((row) => ({
      labels: formatLabels(row.labels),
      metricName: row.metricName,
      value: formatNumber(row.numericValue, 2),
    }))
    .sort((a, b) => a.metricName.localeCompare(b.metricName) || a.labels.localeCompare(b.labels)),
);

function parseLabels(labelsText: string) {
  const result: Record<string, string> = {};
  for (const part of labelsText.split(', ')) {
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

function formatLabels(labels: Record<string, string>) {
  const ignored = new Set(['exported_instance', 'exported_job', 'instance', 'job', 'otel_scope_name']);
  const text = Object.entries(labels)
    .filter(([key]) => !ignored.has(key))
    .map(([key, value]) => `${key}=${value}`)
    .join(', ');
  return text || '--';
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

function buildHistogramMetricSelector(
  metricName: string,
  suffix: 'bucket' | 'count' | 'sum',
  labels: Record<string, string>,
  extraMatchers: string[] = [],
) {
  return `${metricName}_${suffix}${buildSelector(labels, extraMatchers)}`;
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

async function queryInstant(query: string) {
  return client.query(query, context.value.end);
}

let analyticsRequestId = 0;

async function loadAnalytics() {
  if (!props.app) {
    durationCards.value = [];
    roomMatchRows.value = [];
    errorText.value = '';
    return;
  }

  const labels = targetLabels.value;
  if (Object.keys(labels).length === 0) {
    durationCards.value = [];
    roomMatchRows.value = [];
    errorText.value = '';
    return;
  }

  const requestId = ++analyticsRequestId;
  loading.value = true;
  errorText.value = '';

  try {
    const durationMetricName = resolveMetricName(METRIC_NAME_ALIASES.duration, true);
    const roomMatchMetricName = resolveMetricName(METRIC_NAME_ALIASES.roomMatchTotal);
    const durationBucketSelector = buildHistogramMetricSelector(durationMetricName, 'bucket', labels);
    const durationSumSelector = buildHistogramMetricSelector(durationMetricName, 'sum', labels);
    const durationCountSelector = buildHistogramMetricSelector(durationMetricName, 'count', labels);

    const [avgDurationResults, p50DurationResults, p95DurationResults, roomMatchResults] = await Promise.all([
      queryInstant(`sum(rate(${durationSumSelector}[5m])) / sum(rate(${durationCountSelector}[5m]))`),
      queryInstant(`histogram_quantile(0.50, sum by (le) (rate(${durationBucketSelector}[5m])))`),
      queryInstant(`histogram_quantile(0.95, sum by (le) (rate(${durationBucketSelector}[5m])))`),
      queryInstant(
        `sum by (sup_id, outcome) (increase(${buildMetricSelector(roomMatchMetricName, labels)}[${context.value.wholeWindow}]))`,
      ),
    ]);

    if (requestId !== analyticsRequestId) {
      return;
    }

    durationCards.value = [
      {
        label: '运行平均耗时',
        value: formatDuration(parsePrometheusNumber(avgDurationResults[0]?.value)),
      },
      {
        label: '运行 P50',
        value: formatDuration(parsePrometheusNumber(p50DurationResults[0]?.value)),
      },
      {
        label: '运行 P95',
        value: formatDuration(parsePrometheusNumber(p95DurationResults[0]?.value)),
      },
      {
        label: '最新指标条数',
        value: formatInteger(latestMetricRows.value.length),
      },
    ];

    const roomMatchMap = mapVectorByLabels(roomMatchResults, ['sup_id', 'outcome']);
    roomMatchRows.value = [...roomMatchMap.entries()]
      .map(([key, value]) => {
        const [supId, outcome] = key.split('@@');
        return {
          count: formatInteger(value),
          outcome: outcome || '--',
          sortValue: value,
          supId: supId || '--',
        };
      })
      .sort((a, b) => b.sortValue - a.sortValue || a.supId.localeCompare(b.supId));
  } catch (error) {
    if (requestId !== analyticsRequestId) {
      return;
    }
    durationCards.value = [];
    roomMatchRows.value = [];
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
    void loadAnalytics();
  },
  { immediate: true },
);
</script>

<template>
  <div class="ota-room-builder-panel">
    <el-card shadow="never">
      <template #header>OTARoomBuilder 其他指标</template>

      <el-alert
        v-if="errorText"
        :closable="false"
        :title="errorText"
        class="mb-3"
        type="error"
      />

      <div v-loading="loading" class="summary-grid">
        <div v-for="item in durationCards" :key="item.label" class="summary-item">
          <div class="summary-label">{{ item.label }}</div>
          <div class="summary-value">{{ item.value }}</div>
        </div>
      </div>
    </el-card>

    <div class="tables-grid mt-3">
      <el-card shadow="never">
        <template #header>静态房型匹配结果</template>
        <el-table v-loading="loading" :data="roomMatchRows" stripe>
          <el-table-column label="sup_id" min-width="90" prop="supId" />
          <el-table-column label="outcome" min-width="180" prop="outcome" />
          <el-table-column align="right" label="次数" min-width="110" prop="count" />
        </el-table>
      </el-card>

      <el-card shadow="never">
        <template #header>最新 OTARoomBuilder 指标</template>
        <el-table :data="latestMetricRows" max-height="360" stripe>
          <el-table-column label="metric" min-width="260" prop="metricName" show-overflow-tooltip />
          <el-table-column label="labels" min-width="260" prop="labels" show-overflow-tooltip />
          <el-table-column align="right" label="value" min-width="120" prop="value" />
        </el-table>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.ota-room-builder-panel {
  width: 100%;
}

.summary-grid,
.tables-grid {
  display: grid;
  gap: 12px;
}

.summary-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.tables-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.summary-item {
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.summary-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.summary-value {
  margin-top: 6px;
  font-size: 18px;
  font-weight: 700;
  word-break: break-all;
}

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1280px) {
  .summary-grid,
  .tables-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .summary-grid,
  .tables-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
