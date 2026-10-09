<script lang="ts" setup>
import type { ExpediaLineSeries } from '../types';

type EChartsOption = Record<string, any>;

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, ref, watch } from 'vue';

import { formatCompactNumber, formatDurationMs } from '../formatters';

const props = withDefaults(
  defineProps<{
    emptyDescription?: string;
    loading?: boolean;
    series: ExpediaLineSeries[];
    stack?: boolean;
    title: string;
    valueType?: 'count' | 'duration';
  }>(),
  {
    emptyDescription: '暂无数据',
    loading: false,
    stack: false,
    valueType: 'count',
  },
);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

const hasRenderableSeries = computed(() => props.series.some((item) => item.data.length > 0));

function formatValue(value: number) {
  return props.valueType === 'duration'
    ? formatDurationMs(value)
    : formatCompactNumber(value);
}

function buildChartOption(): EChartsOption {
  return {
    color: props.series.map((item) => item.color),
    grid: {
      bottom: 18,
      containLabel: true,
      left: 16,
      right: 20,
      top: 46,
    },
    legend: {
      icon: 'roundRect',
      itemHeight: 8,
      itemWidth: 12,
      top: 8,
    },
    series: props.series.map((item) => ({
      areaStyle: props.stack ? { opacity: 0.12 } : undefined,
      data: item.data,
      lineStyle: { width: 2 },
      name: item.name,
      showSymbol: false,
      smooth: true,
      stack: props.stack ? 'total' : undefined,
      type: 'line',
    })),
    tooltip: {
      appendToBody: true,
      backgroundColor: 'rgba(18, 18, 18, 0.88)',
      borderWidth: 0,
      className: 'expedia-tooltip',
      formatter: (params: any[]) => {
        const rows = Array.isArray(params) ? params : [];
        const timeText = rows[0]?.axisValue
          ? dayjs(rows[0].axisValue).format('YYYY-MM-DD HH:mm:ss')
          : '--';
        const lines = [timeText];

        for (const row of rows) {
          lines.push(`${row.marker}${row.seriesName}: ${formatValue(Number(row.data?.[1] ?? row.value?.[1] ?? 0))}`);
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
      axisLine: {
        lineStyle: {
          color: 'var(--el-border-color)',
        },
      },
      boundaryGap: false,
      type: 'time',
    },
    yAxis: {
      axisLabel: {
        color: 'var(--el-text-color-secondary)',
        formatter: (value: number) => formatValue(Number(value)),
      },
      splitLine: {
        lineStyle: {
          color: 'rgba(144, 147, 153, 0.16)',
        },
      },
      type: 'value',
    },
  };
}

watch(
  () => [props.loading, props.stack, props.valueType, props.series],
  async () => {
    if (!hasRenderableSeries.value) {
      return;
    }

    await nextTick();
    renderEcharts(buildChartOption());
  },
  { deep: true, immediate: true },
);
</script>

<template>
  <div class="trend-panel">
    <div class="panel-title">{{ props.title }}</div>
    <div v-loading="props.loading" class="panel-body">
      <el-empty v-if="!hasRenderableSeries" :description="props.emptyDescription" :image-size="72" />
      <EchartsUI v-else ref="chartRef" class="chart" />
    </div>
  </div>
</template>

<style scoped>
.trend-panel {
  display: flex;
  flex-direction: column;
  min-height: 320px;
}

.panel-title {
  color: var(--el-text-color-primary);
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 10px;
}

.panel-body {
  flex: 1;
  min-height: 280px;
}

.chart {
  height: 280px;
}
</style>
