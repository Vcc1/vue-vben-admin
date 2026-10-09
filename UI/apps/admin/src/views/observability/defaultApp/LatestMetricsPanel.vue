<script lang="ts" setup>
import { computed, ref } from 'vue';

import type { AppLatestMetricValue, AppViewModel } from './types';
import { getLatestMetricGroups } from './appPanelUtils';

const props = defineProps<{
  app: AppViewModel | null;
}>();

const latestMetricGroups = computed(() => getLatestMetricGroups(props.app));
const collapsedScopes = ref(new Set<string>());

interface BucketChart {
  key: string;
  labelsText: string;
  maxValueText: string;
  metricName: string;
  points: Array<{
    key: string;
    label: string;
    title: string;
    valueText: string;
    x: number;
    y: number;
  }>;
  polyline: string;
}

interface DisplayMetricGroup {
  bucketCharts: BucketChart[];
  items: AppLatestMetricValue[];
  scope: string;
}

const displayMetricGroups = computed<DisplayMetricGroup[]>(() =>
  latestMetricGroups.value.map((group) => {
    const bucketItems = group.items.filter(isBucketMetric);
    return {
      bucketCharts: buildBucketCharts(group.scope, bucketItems),
      items: group.items.filter((item) => !isBucketMetric(item)),
      scope: group.scope,
    };
  }),
);

function isScopeCollapsed(scope: string) {
  return collapsedScopes.value.has(scope);
}

function toggleScope(scope: string) {
  const next = new Set(collapsedScopes.value);
  if (next.has(scope)) {
    next.delete(scope);
  } else {
    next.add(scope);
  }
  collapsedScopes.value = next;
}

function isBucketMetric(item: AppLatestMetricValue) {
  return item.metricName.endsWith('_bucket') && parseLabels(item.labelsText).le !== undefined;
}

function parseLabels(labelsText: string) {
  const labels: Record<string, string> = {};
  for (const part of labelsText.split(', ')) {
    const index = part.indexOf('=');
    if (index < 0) {
      continue;
    }
    const key = part.slice(0, index).trim();
    const value = part.slice(index + 1).trim();
    if (key) {
      labels[key] = value;
    }
  }
  return labels;
}

function formatLabels(labels: Record<string, string>) {
  const text = Object.entries(labels)
    .filter(([key]) => key !== 'le')
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([key, value]) => `${key}=${value}`)
    .join(', ');
  return text || '--';
}

function parseLe(value: string) {
  if (value === '+Inf' || value.toLowerCase() === 'inf') {
    return Number.POSITIVE_INFINITY;
  }
  const numericValue = Number(value);
  return Number.isFinite(numericValue) ? numericValue : Number.POSITIVE_INFINITY;
}

function parseMetricNumber(value: string) {
  const numericValue = Number(value);
  return Number.isFinite(numericValue) ? numericValue : 0;
}

function formatMetricNumber(value: number) {
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: 2,
    minimumFractionDigits: 0,
  }).format(value);
}

function buildBucketCharts(scope: string, items: AppLatestMetricValue[]) {
  const groupMap = new Map<
    string,
    {
      labels: Record<string, string>;
      metricName: string;
      rows: Array<{ item: AppLatestMetricValue; le: string; leValue: number; value: number }>;
    }
  >();

  for (const item of items) {
    const labels = parseLabels(item.labelsText);
    const le = labels.le;
    if (le === undefined) {
      continue;
    }
    const labelsText = formatLabels(labels);
    const key = `${scope}:${item.metricName}:${labelsText}`;
    const current = groupMap.get(key) || {
      labels,
      metricName: item.metricName,
      rows: [],
    };
    current.rows.push({
      item,
      le,
      leValue: parseLe(le),
      value: parseMetricNumber(item.valueText),
    });
    groupMap.set(key, current);
  }

  return [...groupMap.entries()]
    .map(([key, group]) => {
      const rows = group.rows.sort((a, b) => a.leValue - b.leValue || a.le.localeCompare(b.le));
      const maxValue = Math.max(...rows.map((row) => row.value), 0);
      const xStep = rows.length > 1 ? 84 / (rows.length - 1) : 0;
      const points = rows.map((row, index) => {
        const x = rows.length > 1 ? 8 + index * xStep : 50;
        const y = maxValue > 0 ? 36 - (row.value / maxValue) * 28 : 36;
        return {
          key: `${key}:${row.le}`,
          label: row.le,
          title: `le=${row.le}, value=${formatMetricNumber(row.value)}`,
          valueText: formatMetricNumber(row.value),
          x,
          y,
        };
      });
      return {
        key,
        labelsText: formatLabels(group.labels),
        maxValueText: formatMetricNumber(maxValue),
        metricName: group.metricName,
        points,
        polyline: points.map((point) => `${point.x},${point.y}`).join(' '),
      };
    })
    .sort((a, b) => a.metricName.localeCompare(b.metricName) || a.labelsText.localeCompare(b.labelsText));
}
</script>

<template>
  <el-card shadow="never">
    <template #header>指标最新值</template>
    <el-empty v-if="!props.app?.latestMetricValues.length" description="暂无指标值" />
    <div v-else class="group-list">
      <div v-for="group in displayMetricGroups" :key="group.scope" class="scope-group">
        <div class="scope-title" @click="toggleScope(group.scope)">{{ group.scope }}</div>
        <div v-show="!isScopeCollapsed(group.scope)" class="metric-value-list">
          <div
            v-for="chart in group.bucketCharts"
            :key="chart.key"
            class="bucket-chart-item"
          >
            <div class="metric-name">{{ chart.metricName }}</div>
            <div class="metric-labels">{{ chart.labelsText }}</div>
            <div class="bucket-chart-wrap">
              <svg class="bucket-chart" preserveAspectRatio="none" viewBox="0 0 100 42">
                <line class="bucket-axis" x1="8" x2="92" y1="36" y2="36" />
                <polyline class="bucket-line" :points="chart.polyline" />
                <circle
                  v-for="point in chart.points"
                  :key="point.key"
                  class="bucket-point"
                  :cx="point.x"
                  :cy="point.y"
                  r="1.8"
                >
                  <title>{{ point.title }}</title>
                </circle>
              </svg>
              <div class="bucket-chart-meta">
                <span>max {{ chart.maxValueText }}</span>
                <span>{{ chart.points.length }} buckets</span>
              </div>
            </div>
          </div>
          <div
            v-for="item in group.items"
            :key="`${group.scope}:${item.metricName}:${item.labelsText}`"
            class="metric-value-item"
          >
            <div class="metric-name">{{ item.metricName }}</div>
            <div class="metric-labels">{{ item.labelsText }}</div>
            <div class="metric-raw">{{ item.valueText }}</div>
          </div>
        </div>
      </div>
    </div>
  </el-card>
</template>

<style scoped>
.group-list {
  display: grid;
  gap: 14px;
}

.scope-group {
  border-top: 1px dashed var(--el-border-color-lighter);
  padding-top: 8px;
}

.scope-group:first-child {
  border-top: none;
  padding-top: 0;
}

.scope-title {
  color: var(--el-text-color-primary);
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 8px;
}

.metric-value-list {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(1, minmax(0, 1fr));
}

.bucket-chart-item,
.metric-value-item {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  display: grid;
  gap: 6px;
  grid-template-columns: minmax(0, 220px) minmax(0, 1fr) minmax(0, 200px);
  padding: 10px;
}

.bucket-chart-wrap {
  min-width: 0;
}

.bucket-chart {
  display: block;
  width: 100%;
  height: 64px;
}

.bucket-axis {
  stroke: var(--el-border-color);
  stroke-width: 0.8;
}

.bucket-line {
  fill: none;
  stroke: var(--el-color-primary);
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-width: 1.8;
}

.bucket-point {
  fill: var(--el-color-primary);
}

.bucket-chart-meta {
  display: flex;
  justify-content: space-between;
  margin-top: 4px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.metric-name {
  color: var(--el-text-color-primary);
  font-size: 13px;
  font-weight: 600;
  word-break: break-all;
}

.metric-labels {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  word-break: break-all;
}

.metric-raw {
  color: var(--el-text-color-primary);
  font-family: var(--vxe-font-family);
  font-size: 13px;
  font-weight: 600;
  text-align: right;
  word-break: break-all;
}

@media (max-width: 1280px) {
  .bucket-chart-item,
  .metric-value-item {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .metric-raw {
    text-align: left;
  }
}
</style>
