<script lang="ts" setup>
type EChartsOption = Record<string, any>;
type SeriesOption = Record<string, any>;

import type { PrometheusMatrixResult, PrometheusServerCard } from '#/api/prometheus/types';

import type { DetailMetricsRaw, MetricNavItem } from './types';

import { EchartsUI, type EchartsUIType, useEcharts } from '@vben/plugins/echarts';
import dayjs from 'dayjs';
import { computed, nextTick, ref, watch } from 'vue';

const props = defineProps<{
  activeMetricKey: string;
  detailLoading?: boolean;
  detailRaw: DetailMetricsRaw | null;
  metricNavItems: MetricNavItem[];
  selectedDisk: string;
  selectedInterface: string;
  serverCard: null | PrometheusServerCard;
}>();

const emit = defineEmits<{
  (e: 'selectMetric', value: string): void;
}>();

const DISK_LABEL_KEYS = ['disk', 'device', 'volume', 'mountpoint'];
const INTERFACE_LABEL_KEYS = ['interface', 'nic', 'device'];

const cpuTotalRef = ref<EchartsUIType>();
const cpuCoreRef = ref<EchartsUIType>();
const memoryRef = ref<EchartsUIType>();
const diskRef = ref<EchartsUIType>();
const diskIoRef = ref<EchartsUIType>();
const networkRef = ref<EchartsUIType>();

const { renderEcharts: renderCpuTotal } = useEcharts(cpuTotalRef);
const { renderEcharts: renderCpuCore } = useEcharts(cpuCoreRef);
const { renderEcharts: renderMemory } = useEcharts(memoryRef);
const { renderEcharts: renderDisk } = useEcharts(diskRef);
const { renderEcharts: renderDiskIo } = useEcharts(diskIoRef);
const { renderEcharts: renderNetwork } = useEcharts(networkRef);

const selectedDiskInfo = computed(() =>
  (props.serverCard?.diskUsage || []).find((disk) => disk.name === props.selectedDisk) || null,
);

const isCpuMetric = computed(() => props.activeMetricKey === 'cpu');
const isMemoryMetric = computed(() => props.activeMetricKey === 'memory');
const isDiskMetric = computed(() => props.activeMetricKey.startsWith('disk:'));
const isNetworkMetric = computed(() => props.activeMetricKey.startsWith('network:'));

function getLabel(metric: Record<string, string>, keys: string[]) {
  for (const key of keys) {
    if (metric[key]) {
      return metric[key];
    }
  }
  return 'unknown';
}

function toLineData(results: PrometheusMatrixResult[]) {
  return results.map((result) => result.values.map(([timestamp, value]) => [timestamp * 1000, Number(value)] as [number, number]));
}

function sumMatrix(results: PrometheusMatrixResult[], filter?: (item: PrometheusMatrixResult) => boolean) {
  const bucket = new Map<number, number>();
  for (const item of results) {
    if (filter && !filter(item)) {
      continue;
    }

    for (const [timestamp, rawValue] of item.values) {
      const value = Number(rawValue);
      if (!Number.isFinite(value)) {
        continue;
      }
      bucket.set(timestamp, (bucket.get(timestamp) || 0) + value);
    }
  }

  return [...bucket.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([timestamp, value]) => [timestamp * 1000, value] as [number, number]);
}

function formatBytes(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let size = value;
  let index = 0;
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024;
    index += 1;
  }
  return `${size.toFixed(size >= 100 ? 0 : 2)} ${units[index]}`;
}

function formatSpeed(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '暂无数据';
  }
  return `${formatBytes(value)}/s`;
}

function buildLineOption(
  series: SeriesOption[],
  formatter: (value: number) => string,
  yAxisName: string,
): EChartsOption {
  return {
    grid: {
      bottom: 20,
      containLabel: true,
      left: 20,
      right: 20,
      top: 30,
    },
    legend: {
      type: 'scroll',
    },
    tooltip: {
      trigger: 'axis',
      formatter: (params: any) => {
        const list = Array.isArray(params) ? params : [params];
        if (!list.length) {
          return '';
        }

        const time = list[0]?.value?.[0];
        const rows = list
          .map((item: any) => {
            const val = Number(item?.value?.[1]);
            return `${item.marker}${item.seriesName}: ${formatter(val)}`;
          })
          .join('<br/>');

        return `${dayjs(time).format('YYYY-MM-DD HH:mm:ss')}<br/>${rows}`;
      },
    },
    xAxis: {
      type: 'time',
    },
    yAxis: {
      axisLabel: {
        formatter: (value: number) => formatter(value),
      },
      name: yAxisName,
      type: 'value',
    },
    series,
  };
}

function renderChartOptions() {
  if (!props.detailRaw) {
    return;
  }

  const raw = props.detailRaw;

  const cpuTotalSeries: SeriesOption[] = toLineData(raw.cpuTotal).map((data, index) => ({
    data,
    name: index === 0 ? 'CPU Total' : `CPU Total ${index + 1}`,
    showSymbol: false,
    smooth: true,
    type: 'line',
  }));

  const cpuCoreResults = [...raw.cpuCore]
    .map((item) => ({
      item,
      latest: Number(item.values.at(-1)?.[1] ?? 0),
      name: `Core ${item.metric.core || item.metric.cpu || 'unknown'}`,
    }))
    .sort((a, b) => b.latest - a.latest);

  const cpuCoreSeries: SeriesOption[] = cpuCoreResults.map(({ item, name }) => ({
    data: item.values.map(([timestamp, value]) => [timestamp * 1000, Number(value)] as [number, number]),
    name,
    showSymbol: false,
    smooth: true,
    type: 'line',
  }));

  const memoryUsed = sumMatrix(raw.memoryUsed);
  const memoryAvailable = sumMatrix(raw.memoryAvailable);
  const memorySeries: SeriesOption[] = [
    { data: memoryUsed, name: 'Memory Used', showSymbol: false, smooth: true, type: 'line' },
    { data: memoryAvailable, name: 'Memory Available', showSymbol: false, smooth: true, type: 'line' },
  ];

  const diskFilter = (item: PrometheusMatrixResult) =>
    getLabel(item.metric, DISK_LABEL_KEYS) === props.selectedDisk;

  const diskUsed = sumMatrix(raw.diskUsed, diskFilter);
  const diskTotal = sumMatrix(raw.diskTotal, diskFilter);
  const diskSeries: SeriesOption[] = [
    { data: diskUsed, name: `Disk ${props.selectedDisk} Used`, showSymbol: false, smooth: true, type: 'line' },
    { data: diskTotal, name: `Disk ${props.selectedDisk} Total`, showSymbol: false, smooth: true, type: 'line' },
  ];

  const diskRead = sumMatrix(raw.diskIoRead, diskFilter);
  const diskWrite = sumMatrix(raw.diskIoWrite, diskFilter);
  const diskIoSeries: SeriesOption[] = [
    { data: diskRead, name: `Disk ${props.selectedDisk} Read/s`, showSymbol: false, smooth: true, type: 'line' },
    { data: diskWrite, name: `Disk ${props.selectedDisk} Write/s`, showSymbol: false, smooth: true, type: 'line' },
  ];

  const networkFilter = (item: PrometheusMatrixResult) =>
    getLabel(item.metric, INTERFACE_LABEL_KEYS) === props.selectedInterface;

  const netReceive = sumMatrix(raw.networkReceive, networkFilter);
  const netTransmit = sumMatrix(raw.networkTransmit, networkFilter);
  const networkSeries: SeriesOption[] = [
    { data: netReceive, name: `Net ${props.selectedInterface} Receive/s`, showSymbol: false, smooth: true, type: 'line' },
    { data: netTransmit, name: `Net ${props.selectedInterface} Transmit/s`, showSymbol: false, smooth: true, type: 'line' },
  ];

  nextTick(() => {
    renderCpuTotal(buildLineOption(cpuTotalSeries, (value) => `${value.toFixed(2)}%`, 'Percent'));
    renderCpuCore(buildLineOption(cpuCoreSeries, (value) => `${value.toFixed(2)}%`, 'Percent'));
    renderMemory(buildLineOption(memorySeries, formatBytes, 'Bytes'));
    renderDisk(buildLineOption(diskSeries, formatBytes, 'Bytes'));
    renderDiskIo(buildLineOption(diskIoSeries, formatBytes, 'Bytes/s'));
    renderNetwork(buildLineOption(networkSeries, formatBytes, 'Bytes/s'));
  });
}

watch(
  () => [props.detailRaw, props.selectedDisk, props.selectedInterface, props.activeMetricKey],
  () => {
    renderChartOptions();
  },
  { deep: true, immediate: true },
);
</script>

<template>
  <div v-loading="props.detailLoading" class="detail-body">
    <div class="metric-nav">
      <button
        v-for="item in props.metricNavItems"
        :key="item.key"
        class="metric-nav-item"
        :class="{ active: props.activeMetricKey === item.key }"
        type="button"
        @click="emit('selectMetric', item.key)"
      >
        {{ item.label }}
      </button>
    </div>

    <div class="metric-content charts-wrap">
      <el-card v-show="isDiskMetric" shadow="never">
        <template #header>磁盘概览 - {{ props.selectedDisk }}</template>
        <el-empty v-if="!selectedDiskInfo">暂无磁盘数据</el-empty>
        <div v-else class="disk-list single">
          <div class="disk-item single">
            <div class="disk-header">
              <div class="disk-title-group">
                <div class="disk-icon">
                  <svg class="size-4" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                    <line x1="22" x2="2" y1="12" y2="12" />
                    <path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" />
                    <line x1="6" x2="6.01" y1="16" y2="16" />
                    <line x1="10" x2="10.01" y1="16" y2="16" />
                  </svg>
                </div>
                <span class="disk-name">{{ selectedDiskInfo?.name }}</span>
              </div>
              <span class="disk-summary">{{ formatBytes(selectedDiskInfo?.used ?? null) }} / {{ formatBytes(selectedDiskInfo?.total ?? null) }}</span>
            </div>

            <div class="disk-io-row">
              <div class="disk-io-box">
                <span class="io-label">读/s</span>
                <span class="io-value">{{ formatSpeed(selectedDiskInfo?.readBytesPerSec ?? null) }}</span>
              </div>
              <div class="disk-io-box">
                <span class="io-label">写/s</span>
                <span class="io-value">{{ formatSpeed(selectedDiskInfo?.writeBytesPerSec ?? null) }}</span>
              </div>
            </div>

            <div class="disk-capacity-row">
              <div class="disk-capacity-item">
                <span class="label">总容量</span>
                <span class="value">{{ formatBytes(selectedDiskInfo?.total ?? null) }}</span>
              </div>
              <div class="disk-capacity-item">
                <span class="label">已用</span>
                <span class="value">{{ formatBytes(selectedDiskInfo?.used ?? null) }}</span>
              </div>
              <div class="disk-capacity-item">
                <span class="label">剩余</span>
                <span class="value">{{ formatBytes(Math.max((selectedDiskInfo?.total || 0) - (selectedDiskInfo?.used || 0), 0)) }}</span>
              </div>
            </div>

            <div class="disk-progress-bg">
              <div
                class="disk-progress-bar"
                :class="{ high: (selectedDiskInfo?.usagePercent || 0) > 90, medium: (selectedDiskInfo?.usagePercent || 0) > 75 }"
                :style="{ width: `${Math.min(selectedDiskInfo?.usagePercent || 0, 100)}%` }"
              ></div>
            </div>
          </div>
        </div>
      </el-card>

      <el-card v-show="isCpuMetric" shadow="never">
        <template #header>CPU 总使用率（system_cpu_usage_percent）</template>
        <EchartsUI ref="cpuTotalRef" class="chart" />
      </el-card>

      <el-card v-show="isCpuMetric" class="mt-3" shadow="never">
        <template #header>CPU 每核使用率（system_cpu_core_usage_percent）</template>
        <EchartsUI ref="cpuCoreRef" class="chart" />
      </el-card>

      <el-card v-show="isMemoryMetric" class="mt-3" shadow="never">
        <template #header>内存 used/available</template>
        <EchartsUI ref="memoryRef" class="chart" />
      </el-card>

      <el-card v-show="isDiskMetric" class="mt-3" shadow="never">
        <template #header>磁盘 used/total</template>
        <EchartsUI ref="diskRef" class="chart" />
      </el-card>

      <el-card v-show="isDiskMetric" class="mt-3" shadow="never">
        <template #header>磁盘 IO 读写速率</template>
        <EchartsUI ref="diskIoRef" class="chart" />
      </el-card>

      <el-card v-show="isNetworkMetric" class="mt-3" shadow="never">
        <template #header>网卡 IO 收发速率</template>
        <EchartsUI ref="networkRef" class="chart" />
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.detail-body {
  display: flex;
  flex: 1;
  min-height: 0;
}

.metric-nav {
  border-right: 1px solid var(--el-border-color);
  display: flex;
  flex: 0 0 220px;
  flex-direction: column;
  gap: 8px;
  margin-right: 12px;
  overflow: auto;
  padding-right: 10px;
}

.metric-nav-item {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  color: var(--el-text-color-primary);
  cursor: pointer;
  font-size: 13px;
  line-height: 18px;
  padding: 8px 10px;
  text-align: left;
}

.metric-nav-item.active {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.metric-content {
  margin-top: 0;
}

.charts-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding-right: 2px;
}

.chart {
  height: 260px;
}

.disk-list {
  display: flex;
  flex-wrap: nowrap;
  gap: 8px;
  overflow-x: auto;
  padding: 0 10px 4px 2px;
  scrollbar-gutter: stable;
  width: 100%;
}

.disk-list.single {
  overflow-x: visible;
  padding-right: 0;
}

.disk-item {
  background-color: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  box-sizing: border-box;
  display: flex;
  flex: 0 0 320px;
  flex-direction: column;
  gap: 4px;
  max-width: calc(100% - 12px);
  min-width: 320px;
  padding: 8px;
}

.disk-item.single {
  flex: 1 1 auto;
  max-width: none;
  min-width: 0;
}

.disk-list::after {
  content: '';
  flex: 0 0 2px;
}

.disk-title-group {
  align-items: center;
  display: flex;
  gap: 4px;
}

.disk-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  line-height: 1.2;
}

.disk-icon {
  align-items: center;
  color: var(--el-color-primary);
  display: flex;
  margin-right: 4px;
}

.disk-name {
  font-weight: 600;
}

.disk-summary {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  white-space: nowrap;
}

.disk-io-row {
  display: grid;
  gap: 6px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.disk-io-box {
  background-color: var(--el-fill-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  display: flex;
  flex-direction: column;
  padding: 6px;
}

.io-label {
  color: var(--el-text-color-secondary);
  font-size: 11px;
}

.io-value {
  color: var(--el-text-color-primary);
  font-size: 13px;
  font-weight: 600;
  white-space: nowrap;
}

.disk-capacity-row {
  display: grid;
  gap: 6px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.disk-capacity-item {
  background-color: var(--el-fill-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  display: flex;
  flex-direction: column;
  padding: 6px;
}

.label {
  color: var(--el-text-color-secondary);
}

.value {
  color: var(--el-text-color-primary);
  font-weight: 500;
}

.disk-progress-bg {
  background-color: var(--el-border-color-lighter);
  border-radius: 2px;
  height: 4px;
  margin-top: 4px;
  overflow: hidden;
  width: 100%;
}

.disk-progress-bar {
  background-color: var(--el-color-primary);
  height: 100%;
  transition: width 0.3s ease;
}

.disk-progress-bar.medium {
  background-color: var(--el-color-warning);
}

.disk-progress-bar.high {
  background-color: var(--el-color-danger);
}

@media (max-width: 1200px) {
  .detail-body {
    flex-direction: column;
  }

  .metric-nav {
    border-right: none;
    border-bottom: 1px solid var(--el-border-color);
    flex: 0 0 auto;
    margin-bottom: 12px;
    margin-right: 0;
    max-height: 180px;
    padding-bottom: 10px;
    padding-right: 0;
  }
}
</style>
