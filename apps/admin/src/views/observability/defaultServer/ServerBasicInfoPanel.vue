<script lang="ts" setup>
import type { PrometheusServerCard } from '#/api/prometheus/types';

import type { ServerApplicationItem } from './types';

const props = defineProps<{
  serverApplications: ServerApplicationItem[];
  serverCard: null | PrometheusServerCard;
}>();

function formatPercent(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return `${value.toFixed(2)}%`;
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
</script>

<template>
  <div class="basic-info-wrap">
    <el-card shadow="never">
      <template #header>Server Info</template>
      <div class="info-group">
        <div class="metric-row">
          <span>系统类型</span>
          <span>{{ props.serverCard?.systemType || '--' }}</span>
        </div>
      </div>

      <div class="info-group mt-3">
        <div class="info-group-title">CPU</div>
        <div class="metric-row">
          <span>总使用率</span>
          <span>{{ formatPercent(props.serverCard?.cpuUsagePercent ?? null) }}</span>
        </div>
      </div>

      <div class="info-group mt-3">
        <div class="info-group-title">内存</div>
        <div class="metric-row">
          <span>使用率</span>
          <span>{{ formatPercent(props.serverCard?.memoryUsagePercent ?? null) }}</span>
        </div>
      </div>

      <div class="info-group mt-3">
        <div class="info-group-title">硬盘</div>
        <el-empty v-if="!(props.serverCard?.diskUsage?.length)" description="暂无硬盘数据" />
        <div v-else class="basic-disk-list">
          <div v-for="disk in props.serverCard?.diskUsage ?? []" :key="disk.name" class="metric-row">
            <span>{{ disk.name }}</span>
            <span>{{ formatBytes(disk.used) }} / {{ formatBytes(disk.total) }}</span>
          </div>
        </div>
      </div>

      <div class="info-group mt-3">
        <div class="info-group-title">应用</div>
        <el-empty v-if="!props.serverApplications.length" description="暂无应用数据" />
        <div v-else class="server-app-list">
          <div
            v-for="app in props.serverApplications"
            :key="`${app.appName}:${app.environment}:${app.instance}`"
            class="server-app-item"
          >
            <div class="server-app-name">{{ app.appName }}</div>
            <div class="server-app-meta">
              <span>环境：{{ app.environment }}</span>
              <span>实例：{{ app.instance }}</span>
            </div>
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.basic-info-wrap {
  flex: 1;
  min-width: 0;
}

.info-group-title {
  color: var(--el-text-color-primary);
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 6px;
}

.metric-row {
  display: flex;
  font-size: 13px;
  justify-content: space-between;
  line-height: 24px;
}

.basic-disk-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.server-app-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.server-app-item {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 8px 10px;
}

.server-app-name {
  font-weight: 600;
  margin-bottom: 4px;
}

.server-app-meta {
  color: var(--el-text-color-secondary);
  display: flex;
  flex-wrap: wrap;
  font-size: 12px;
  gap: 12px;
  word-break: break-all;
}
</style>
