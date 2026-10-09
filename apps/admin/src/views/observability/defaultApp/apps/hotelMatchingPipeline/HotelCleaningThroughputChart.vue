<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import type { AppViewModel, RuntimeRange } from '../../types';

import type { PrometheusMatrixResult } from '#/api/prometheus/types';

import { computed, nextTick, ref, watch } from 'vue';

import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import dayjs from 'dayjs';

import { PrometheusClient } from '#/api/prometheus/PrometheusClient';

import {
  getRuntimeQueryTime,
  getRuntimeTargetLabels,
} from '../../appPanelUtils';

defineOptions({ name: 'HotelCleaningThroughputChart' });

const props = defineProps<{
  app: AppViewModel | null;
  refreshKey?: number;
}>();

type EChartsOption = Record<string, any>;
type StageKey = 'cleaner' | 'reader' | 'writer';

interface ThroughputSeries {
  color: string;
  data: Array<[number, number]>;
  key: StageKey;
  name: string;
}

const STAGES: Array<{
  color: string;
  key: StageKey;
  name: string;
}> = [
  { color: '#2563eb', key: 'reader', name: '详情入队' },
  { color: '#d97706', key: 'cleaner', name: '清洗' },
  { color: '#059669', key: 'writer', name: '写入' },
];

const RANGE_OPTIONS: Array<{ label: string; value: RuntimeRange }> = [
  { label: '15 分钟', value: '15m' },
  { label: '1 小时', value: '1h' },
  { label: '6 小时', value: '6h' },
  { label: '24 小时', value: '24h' },
];

const RANGE_SECONDS: Record<RuntimeRange, number> = {
  '15m': 15 * 60,
  '1h': 60 * 60,
  '6h': 6 * 60 * 60,
  '24h': 24 * 60 * 60,
};

const RANGE_STEP: Record<RuntimeRange, number> = {
  '15m': 15,
  '1h': 30,
  '6h': 120,
  '24h': 300,
};

const client = new PrometheusClient();
const selectedRange = ref<RuntimeRange>('1h');
const loading = ref(false);
const errorText = ref('');
const throughputSeries = ref<ThroughputSeries[]>([]);
const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const targetLabels = computed(() => getRuntimeTargetLabels(props.app));
const hasData = computed(() =>
  throughputSeries.value.some((series) => series.data.length > 0),
);

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', String.raw`\"`);
}

function buildMetricSelector(metricName: string) {
  const matchers = Object.entries(targetLabels.value)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `${metricName}{${matchers.join(',')}}`;
}

function findCompletedMetric() {
  const names = [
    'hotel_cleaning_node_completed_total',
    'hotel_cleaning_node_completed',
  ];
  const available = props.app?.availableMetrics || [];
  return names.find((name) => available.includes(name)) || names[0];
}

function getRangeContext() {
  const range = selectedRange.value;
  const step = RANGE_STEP[range];
  const baseTime =
    typeof runtimeQueryTime.value === 'number' &&
    Number.isFinite(runtimeQueryTime.value)
      ? runtimeQueryTime.value
      : Math.floor(Date.now() / 1000);
  const end = Math.floor(baseTime / step) * step;
  return {
    end,
    start: end - RANGE_SECONDS[range],
    step,
  };
}

function buildSeries(results: PrometheusMatrixResult[]) {
  return STAGES.map((stage) => {
    const points = new Map<number, number>();
    for (const row of results.filter(
      (result) => result.metric.node === stage.key,
    )) {
      for (const [timestamp, rawValue] of row.values || []) {
        const value = Number(rawValue);
        if (Number.isFinite(value)) {
          const time = timestamp * 1000;
          points.set(time, (points.get(time) || 0) + Math.max(0, value));
        }
      }
    }
    return {
      color: stage.color,
      data: [...points.entries()].sort(([left], [right]) => left - right),
      key: stage.key,
      name: stage.name,
    } satisfies ThroughputSeries;
  });
}

function formatCount(value: number) {
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: value < 10 ? 1 : 0,
  }).format(value);
}

function buildChartOption(): EChartsOption {
  return {
    animation: false,
    color: throughputSeries.value.map((item) => item.color),
    grid: { bottom: 20, containLabel: true, left: 12, right: 18, top: 34 },
    legend: {
      icon: 'roundRect',
      itemHeight: 8,
      itemWidth: 14,
      top: 0,
    },
    series: throughputSeries.value.map((item) => ({
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
            ? dayjs(rows[0].axisValue).format('MM-DD HH:mm:ss')
            : '--',
        ];
        for (const row of rows) {
          lines.push(
            `${row.marker}${row.seriesName}: ${formatCount(Number(row.value?.[1] || 0))} 家/分钟`,
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
        formatter: (value: number) => formatCount(value),
      },
      min: 0,
      name: '酒店 / 分钟',
      type: 'value',
    },
  };
}

async function load() {
  if (!props.app || Object.keys(targetLabels.value).length === 0) {
    throughputSeries.value = [];
    return;
  }

  loading.value = true;
  errorText.value = '';
  try {
    const context = getRangeContext();
    const selector = buildMetricSelector(findCompletedMetric() || '');
    const results = await client.queryRange({
      end: context.end,
      query: `sum by (node) (increase(${selector}[1m]))`,
      start: context.start,
      step: context.step,
    });
    throughputSeries.value = buildSeries(results);

    await nextTick();
    if (hasData.value) {
      await renderEcharts(buildChartOption());
    }
  } catch (error) {
    throughputSeries.value = [];
    errorText.value =
      error instanceof Error ? error.message : '吞吐指标查询失败';
  } finally {
    loading.value = false;
  }
}

watch(
  () => [
    selectedRange.value,
    runtimeQueryTime.value,
    JSON.stringify(targetLabels.value),
    props.app?.availableMetrics.join('|'),
    props.refreshKey || 0,
  ],
  () => void load(),
  { immediate: true },
);
</script>

<template>
  <el-card shadow="never">
    <template #header>
      <div class="chart-header">
        <div>
          <div class="chart-title">每分钟完成酒店数</div>
          <div class="chart-subtitle">
            每个点统计向前一分钟内详情入队、清洗和写入完成的酒店数量
          </div>
        </div>
        <el-segmented
          v-model="selectedRange"
          :options="RANGE_OPTIONS"
          size="small"
        />
      </div>
    </template>

    <el-alert
      v-if="errorText"
      :closable="false"
      :title="errorText"
      class="mb-3"
      type="error"
    />

    <div v-loading="loading" class="chart-body">
      <el-empty
        v-if="!hasData"
        description="暂无每分钟吞吐数据"
        :image-size="64"
      />
      <EchartsUI v-else ref="chartRef" class="chart" />
    </div>
  </el-card>
</template>

<style scoped>
.chart-header {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.chart-title {
  font-size: 15px;
  font-weight: 700;
}

.chart-subtitle {
  margin-top: 3px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.chart-body {
  min-height: 320px;
}

.chart {
  height: 320px;
}

.mb-3 {
  margin-bottom: 12px;
}

@media (max-width: 768px) {
  .chart-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .chart-header :deep(.el-segmented) {
    width: 100%;
  }
}
</style>
