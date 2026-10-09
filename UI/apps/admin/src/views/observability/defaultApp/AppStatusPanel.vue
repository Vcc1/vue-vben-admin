<script lang="ts" setup>
import { computed } from 'vue';

import type { AppViewModel } from './types';
import {
  formatHeartbeat,
  formatSeconds,
  getAppMetricsValues,
  getEnvironmentText,
  getHostNameText,
  getServiceNameText,
  getStatusInfo,
} from './appPanelUtils';

const props = defineProps<{
  app: AppViewModel | null;
}>();

const statusInfo = computed(() => getStatusInfo(props.app));
const environmentText = computed(() => getEnvironmentText(props.app));
const hostNameText = computed(() => getHostNameText(props.app));
const serviceNameText = computed(() => getServiceNameText(props.app));
const appMetricsValues = computed(() => getAppMetricsValues(props.app));
</script>

<template>
  <div class="app-status-panel">
    <div class="status-grid">
      <el-card shadow="never">
        <div class="status-label">应用状态</div>
        <div class="status-value">
          <el-tag :type="statusInfo.type" size="large">{{ statusInfo.text }}</el-tag>
        </div>
      </el-card>
      <el-card shadow="never">
        <div class="status-label">运行环境</div>
        <div class="status-text">{{ environmentText }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="status-label">服务版本</div>
        <div class="status-text">{{ props.app?.serviceVersion || '--' }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="status-label">宿主主机</div>
        <div class="status-text">{{ hostNameText }}</div>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>运行状态</template>
      <div class="kv-row">
        <span>最近心跳</span>
        <span>{{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}</span>
      </div>
      <div class="kv-row">
        <span>启动时间</span>
        <span>{{ formatHeartbeat(props.app?.startTimeSeconds ?? null) }}</span>
      </div>
      <div class="kv-row">
        <span>运行时长</span>
        <span>{{ formatSeconds(props.app?.uptimeSeconds ?? null) }}</span>
      </div>
      <div class="kv-row">
        <span>并发请求数</span>
        <span>{{ props.app?.inflight ?? '--' }}</span>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>AppMetrics 应用状况</template>
      <el-empty v-if="!appMetricsValues.length" description="暂无 AppMetrics 应用状况指标" />
      <div v-else class="status-metric-grid">
        <div
          v-for="item in appMetricsValues"
          :key="`${item.metricName}:${item.labelsText}`"
          class="status-metric-item"
        >
          <div class="metric-header">
            <div class="metric-name">{{ item.metricName }}</div>
            <div class="metric-value">{{ item.valueText }}</div>
          </div>
          <div class="metric-labels">{{ item.labelsText }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>服务上下文</template>
      <div class="kv-row">
        <span>service.name</span>
        <span class="wrap">{{ serviceNameText }}</span>
      </div>
      <div class="kv-row">
        <span>实例</span>
        <span class="wrap">{{ props.app?.instance || '--' }}</span>
      </div>
      <div class="kv-row">
        <span>Jobs</span>
        <span class="wrap">{{ props.app?.jobs.join(', ') || '--' }}</span>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.app-status-panel {
  width: 100%;
}

.status-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.status-label {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.status-value {
  margin-top: 10px;
}

.status-text {
  font-size: 18px;
  font-weight: 700;
  margin-top: 6px;
  word-break: break-all;
}

.kv-row {
  align-items: flex-start;
  display: flex;
  font-size: 13px;
  justify-content: space-between;
  line-height: 24px;
}

.wrap {
  margin-left: 16px;
  text-align: right;
  word-break: break-all;
}

.status-metric-grid {
  display: grid;
  gap: 10px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.status-metric-item {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 10px;
}

.metric-header {
  align-items: center;
  display: flex;
  gap: 12px;
  justify-content: space-between;
}

.metric-name {
  color: var(--el-text-color-primary);
  font-size: 13px;
  font-weight: 600;
  word-break: break-all;
}

.metric-value {
  color: var(--el-text-color-primary);
  font-family: var(--vxe-font-family);
  font-size: 13px;
  font-weight: 700;
  text-align: right;
  word-break: break-all;
}

.metric-labels {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  margin-top: 8px;
  word-break: break-all;
}

@media (max-width: 1280px) {
  .status-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .status-metric-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
