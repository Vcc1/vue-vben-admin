<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { AppViewModel, RuntimeRange } from '../../types';

import type {
  PrometheusMatrixResult,
  PrometheusVectorResult,
} from '#/api/prometheus/types';

import { computed, nextTick, ref, watch } from 'vue';

import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import dayjs from 'dayjs';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import {
  getRuntimeQueryTime,
  getRuntimeTargetLabels,
} from '../../appPanelUtils';

defineOptions({ name: 'PipelineStagesPanel' });

const props = defineProps<{
  app: AppViewModel | null;
  range: RuntimeRange;
}>();

type StageKey = 'create' | 'load' | 'match' | 'static_data';

interface StageDefinition {
  color: string;
  key: StageKey;
  label: string;
  queue: string;
}

interface StageState {
  completedPerMinute: number;
  key: StageKey;
  label: string;
  queueDepth: number;
  running: number;
  totalCompleted: number;
}

type EChartsOption = Record<string, any>;

const STAGES: StageDefinition[] = [
  { color: '#64748b', key: 'load', label: '酒店加载', queue: 'job' },
  { color: '#2563eb', key: 'match', label: '房型匹配', queue: 'hotel_build' },
  {
    color: '#d97706',
    key: 'static_data',
    label: '静态数据补全',
    queue: 'static_data',
  },
  { color: '#059669', key: 'create', label: '房型创建', queue: 'room_create' },
];

const RANGE_SECONDS: Record<RuntimeRange, number> = {
  '15m': 15 * 60,
  '1h': 60 * 60,
  '6h': 6 * 60 * 60,
  '24h': 24 * 60 * 60,
};

const client = new PrometheusClient();
const loading = ref(false);
const errorText = ref('');
const completionSeries = ref<LineSeries[]>([]);
const runningSeries = ref<LineSeries[]>([]);
const queueSeries = ref<LineSeries[]>([]);
const stageStates = ref<StageState[]>([]);
const completionChartRef = ref<EchartsUIType>();
const pressureChartRef = ref<EchartsUIType>();
const { renderEcharts: renderCompletionChart } = useEcharts(completionChartRef);
const { renderEcharts: renderPressureChart } = useEcharts(pressureChartRef);

interface LineSeries {
  color: string;
  data: Array<[number, number]>;
  name: string;
}

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));
const hasCompletionData = computed(() =>
  completionSeries.value.some((series) =>
    series.data.some(([, value]) => value > 0),
  ),
);
const hasPressureData = computed(() =>
  [...runningSeries.value, ...queueSeries.value].some((series) =>
    series.data.some(([, value]) => value > 0),
  ),
);

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', String.raw`\"`);
}

function buildSelector(labels: Record<string, string>) {
  const parts = Object.entries(labels)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `{${parts.join(',')}}`;
}

function buildMetricSelector(
  metricName: string,
  labels: Record<string, string>,
) {
  return `${metricName}${buildSelector(labels)}`;
}

function findMetricName(names: readonly string[]) {
  const available = props.app?.availableMetrics || [];
  return names.find((name) => available.includes(name)) || names[0] || '';
}

function parseVectorValue(results: PrometheusVectorResult[]) {
  const value = Number(results[0]?.value?.[1]);
  return Number.isFinite(value) ? value : 0;
}

function mapVectorByLabel(results: PrometheusVectorResult[], label: string) {
  const values = new Map<string, number>();
  for (const row of results) {
    const key = row.metric[label];
    const value = Number(row.value?.[1]);
    if (key && Number.isFinite(value)) {
      values.set(key, (values.get(key) || 0) + value);
    }
  }
  return values;
}

function getRangeContext() {
  const step = 60;
  const baseTime =
    typeof runtimeQueryTime.value === 'number' &&
    Number.isFinite(runtimeQueryTime.value)
      ? runtimeQueryTime.value
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  return {
    end,
    start: end - RANGE_SECONDS[props.range],
    step,
  };
}

function buildTimeline(start: number, end: number, step: number) {
  return Array.from(
    { length: Math.floor((end - start) / step) + 1 },
    (_, index) => (start + index * step) * 1000,
  );
}

function formatNumber(value: number, digits = 0) {
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: digits,
    minimumFractionDigits: digits,
  }).format(value);
}

function buildStageSeries(
  results: PrometheusMatrixResult[],
  timeline: number[],
  valueTransform = (value: number) => value,
): LineSeries[] {
  return STAGES.map((stage) => {
    const row = results.find((item) => item.metric.stage === stage.key);
    const points = new Map<number, number>();
    for (const [timestamp, rawValue] of row?.values || []) {
      const value = Number(rawValue);
      if (Number.isFinite(value)) {
        points.set(timestamp * 1000, valueTransform(value));
      }
    }
    return {
      color: stage.color,
      data: timeline.map((timestamp) => [
        timestamp,
        points.get(timestamp) || 0,
      ]) as Array<[number, number]>,
      name: stage.label,
    };
  });
}

function buildGaugeSeries(
  results: PrometheusMatrixResult[][],
  timeline: number[],
  definitions: Array<{ color: string; key: string; label: string }>,
) {
  return definitions.map((definition, index) => {
    const row = results[index]?.[0];
    const points = new Map<number, number>();
    for (const [timestamp, rawValue] of row?.values || []) {
      const value = Number(rawValue);
      if (Number.isFinite(value)) {
        points.set(timestamp * 1000, value);
      }
    }
    return {
      color: definition.color,
      data: timeline.map((timestamp) => [
        timestamp,
        points.get(timestamp) || 0,
      ]) as Array<[number, number]>,
      name: definition.label,
    };
  });
}

function buildLineOption(
  series: LineSeries[],
  yAxisName: string,
): EChartsOption {
  return {
    animation: false,
    color: series.map((item) => item.color),
    grid: { bottom: 24, containLabel: true, left: 12, right: 18, top: 28 },
    legend: { icon: 'roundRect', itemHeight: 8, itemWidth: 12, type: 'scroll' },
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
      backgroundColor: 'rgba(18, 18, 18, 0.9)',
      borderWidth: 0,
      formatter: (params: any[]) => {
        const rows = Array.isArray(params) ? params : [];
        const lines = [
          rows[0]?.axisValue
            ? dayjs(rows[0].axisValue).format('MM-DD HH:mm')
            : '--',
        ];
        for (const row of rows) {
          lines.push(
            `${row.marker}${row.seriesName}: ${formatNumber(Number(row.value?.[1] || 0))}`,
          );
        }
        return lines.join('<br />');
      },
      textStyle: { color: '#f5f7fa' },
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
        formatter: (value: number) => formatNumber(value),
      },
      name: yAxisName,
      type: 'value',
    },
  };
}

async function load() {
  if (!props.app || Object.keys(targetLabels.value).length === 0) {
    completionSeries.value = [];
    runningSeries.value = [];
    queueSeries.value = [];
    stageStates.value = [];
    return;
  }

  loading.value = true;
  errorText.value = '';
  try {
    const context = getRangeContext();
    const timeline = buildTimeline(context.start, context.end, context.step);
    const labels = targetLabels.value;
    const completedMetric = findMetricName([
      'ota_room_builder_stage_completed_hotels_total',
      'ota_room_builder_stage_completed_total',
    ]);

    const runningDefinitions = STAGES.map((stage) => {
      const metric = `ota_room_builder_stage_${stage.key}_running_hotels`;
      return {
        color: stage.color,
        key: stage.key,
        label: stage.label,
        metric: findMetricName([metric, `${metric}_hotels`]),
      };
    });
    const queueDefinitions = STAGES.map((stage) => {
      const metric = `ota_room_builder_queue_${stage.queue}_depth`;
      return {
        color: stage.color,
        key: stage.key,
        label: `${stage.label}队列`,
        metric: findMetricName([metric, `${metric}_items`]),
      };
    });

    const [
      completedResults,
      runningResults,
      queueResults,
      completedPerMinuteCurrent,
      completedCurrent,
      runningCurrent,
      queueCurrent,
    ] = await Promise.all([
      client.queryRange({
        end: context.end,
        query: `sum by (stage) (increase(${buildMetricSelector(completedMetric, labels)}[1m]))`,
        start: context.start,
        step: context.step,
      }),
      Promise.all(
        runningDefinitions.map((definition) =>
          client.queryRange({
            end: context.end,
            query: `max(${buildMetricSelector(definition.metric, labels)})`,
            start: context.start,
            step: context.step,
          }),
        ),
      ),
      Promise.all(
        queueDefinitions.map((definition) =>
          client.queryRange({
            end: context.end,
            query: `max(${buildMetricSelector(definition.metric, labels)})`,
            start: context.start,
            step: context.step,
          }),
        ),
      ),
      client.query(
        `sum by (stage) (increase(${buildMetricSelector(completedMetric, labels)}[1m]))`,
        context.end,
      ),
      client.query(
        `sum by (stage) (${buildMetricSelector(completedMetric, labels)})`,
        context.end,
      ),
      Promise.all(
        runningDefinitions.map((definition) =>
          client.query(
            `max(${buildMetricSelector(definition.metric, labels)})`,
            context.end,
          ),
        ),
      ),
      Promise.all(
        queueDefinitions.map((definition) =>
          client.query(
            `max(${buildMetricSelector(definition.metric, labels)})`,
            context.end,
          ),
        ),
      ),
    ]);

    completionSeries.value = buildStageSeries(completedResults, timeline);
    runningSeries.value = buildGaugeSeries(
      runningResults,
      timeline,
      runningDefinitions,
    );
    queueSeries.value = buildGaugeSeries(
      queueResults,
      timeline,
      queueDefinitions,
    );
    const completedPerMinute = mapVectorByLabel(
      completedPerMinuteCurrent,
      'stage',
    );
    const completedTotals = mapVectorByLabel(completedCurrent, 'stage');
    stageStates.value = STAGES.map((stage, index) => ({
      completedPerMinute: completedPerMinute.get(stage.key) || 0,
      key: stage.key,
      label: stage.label,
      queueDepth: parseVectorValue(queueCurrent[index] || []),
      running: parseVectorValue(runningCurrent[index] || []),
      totalCompleted: completedTotals.get(stage.key) || 0,
    }));

    await nextTick();
    if (hasCompletionData.value) {
      await renderCompletionChart(
        buildLineOption(completionSeries.value, '酒店 / 分钟'),
      );
    }
    if (hasPressureData.value) {
      await renderPressureChart(
        buildLineOption(
          [...runningSeries.value, ...queueSeries.value],
          '酒店数',
        ),
      );
    }
  } catch (error) {
    completionSeries.value = [];
    runningSeries.value = [];
    queueSeries.value = [];
    stageStates.value = [];
    errorText.value =
      error instanceof Error ? error.message : 'Prometheus 查询失败';
  } finally {
    loading.value = false;
  }
}

watch(
  () => [
    props.range,
    runtimeQueryTime.value,
    JSON.stringify(targetLabels.value),
    props.app?.availableMetrics.join('|'),
  ],
  () => void load(),
  { immediate: true },
);
</script>

<template>
  <el-card class="pipeline-card" shadow="never">
    <template #header>
      <div class="section-header">
        <div>
          <div class="section-title">OTARoomBuilder 流水线</div>
          <div class="section-subtitle">阶段吞吐、运行中酒店和队列积压</div>
        </div>
        <el-tag effect="plain" type="info">每分钟采样</el-tag>
      </div>
    </template>

    <el-alert
      v-if="errorText"
      :closable="false"
      :title="errorText"
      class="mb-3"
      type="error"
    />

    <div v-loading="loading" class="stage-grid">
      <div v-for="stage in stageStates" :key="stage.key" class="stage-card">
        <div class="stage-heading">
          <span
            class="stage-dot"
            :style="{
              backgroundColor: STAGES.find((item) => item.key === stage.key)
                ?.color,
            }"
          ></span>
          <span>{{ stage.label }}</span>
        </div>
        <div class="stage-rate">
          {{ formatNumber(stage.completedPerMinute) }}
        </div>
        <div class="stage-rate-label">最近一分钟完成酒店</div>
        <div class="stage-meta">
          <span>累计 {{ formatNumber(stage.totalCompleted) }}</span>
          <span>运行中 {{ formatNumber(stage.running) }}</span>
          <span>队列 {{ formatNumber(stage.queueDepth) }}</span>
        </div>
      </div>
    </div>

    <div v-loading="loading" class="charts-grid mt-3">
      <div class="chart-panel">
        <div class="chart-title">每分钟完成酒店数</div>
        <el-empty
          v-if="!hasCompletionData"
          description="暂无阶段完成数据"
          :image-size="64"
        />
        <EchartsUI v-else ref="completionChartRef" class="chart" />
      </div>
      <div class="chart-panel">
        <div class="chart-title">运行中酒店与队列积压</div>
        <el-empty
          v-if="!hasPressureData"
          description="暂无运行或积压数据"
          :image-size="64"
        />
        <EchartsUI v-else ref="pressureChartRef" class="chart" />
      </div>
    </div>

    <el-table class="mt-3" :data="stageStates" size="small" stripe>
      <el-table-column label="阶段" min-width="150" prop="label" />
      <el-table-column align="right" label="最近一分钟完成" min-width="140">
        <template #default="{ row }">
          {{ formatNumber(row.completedPerMinute) }}
        </template>
      </el-table-column>
      <el-table-column align="right" label="累计完成" min-width="110">
        <template #default="{ row }">
          {{ formatNumber(row.totalCompleted) }}
        </template>
      </el-table-column>
      <el-table-column align="right" label="当前运行" min-width="110">
        <template #default="{ row }">{{ formatNumber(row.running) }}</template>
      </el-table-column>
      <el-table-column align="right" label="队列积压" min-width="110">
        <template #default="{ row }">
          {{ formatNumber(row.queueDepth) }}
        </template>
      </el-table-column>
    </el-table>
  </el-card>
</template>

<style scoped>
.pipeline-card {
  width: 100%;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.section-title {
  font-size: 15px;
  font-weight: 700;
}

.section-subtitle,
.stage-rate-label {
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.stage-grid,
.charts-grid {
  display: grid;
  gap: 12px;
}

.stage-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.charts-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.stage-card,
.chart-panel {
  padding: 12px;
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.stage-heading {
  display: flex;
  align-items: center;
  gap: 7px;
  color: var(--el-text-color-regular);
}

.stage-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.stage-rate {
  margin-top: 12px;
  font-size: 24px;
  font-weight: 700;
  color: var(--el-text-color-primary);
}

.stage-meta {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.stage-meta span {
  white-space: nowrap;
}

.chart-title {
  margin-bottom: 8px;
  font-size: 14px;
  font-weight: 600;
}

.chart {
  height: 300px;
}

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1280px) {
  .stage-grid,
  .charts-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .stage-grid,
  .charts-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .section-header {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
