<script lang="ts" setup>
import { computed } from 'vue';

import type { AppViewModel } from './types';
import {
  formatHeartbeat,
  formatSeconds,
  getHostNameText,
  getServiceNameText,
} from './appPanelUtils';

const props = defineProps<{
  app: AppViewModel | null;
}>();

const serviceNameText = computed(() => getServiceNameText(props.app));
const hostNameText = computed(() => getHostNameText(props.app));
const configEntries = computed(() => Object.entries(props.app?.config || {}));
</script>

<template>
  <div class="basic-info-panel">
    <div class="metrics-grid">
      <el-card shadow="never">
        <div class="metric-label">启动时间</div>
        <div class="metric-value">{{ formatHeartbeat(props.app?.startTimeSeconds ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">心跳时间</div>
        <div class="metric-value">{{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">运行时长</div>
        <div class="metric-value">{{ formatSeconds(props.app?.uptimeSeconds ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">并发请求数</div>
        <div class="metric-value">{{ props.app?.inflight ?? '--' }}</div>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>应用信息</template>
      <div class="kv-row">
        <span>服务版本</span>
        <span>{{ props.app?.serviceVersion || '--' }}</span>
      </div>
      <div class="kv-row">
        <span>service.name</span>
        <span class="wrap">{{ serviceNameText }}</span>
      </div>
      <div class="kv-row">
        <span>host.name</span>
        <span class="wrap">{{ hostNameText }}</span>
      </div>
      <div class="kv-row">
        <span>Jobs</span>
        <span class="wrap">{{ props.app?.jobs.join(', ') || '--' }}</span>
      </div>
    </el-card>

    <el-card v-if="configEntries.length" class="mt-3" shadow="never">
      <template #header>应用配置</template>
      <div class="config-grid">
        <div v-for="([key, value]) in configEntries" :key="key" class="config-item">
          <div class="config-key">{{ key }}</div>
          <div class="config-value">{{ value }}</div>
        </div>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>属性标签</template>
      <el-empty v-if="!Object.keys(props.app?.attributes || {}).length" description="暂无属性" />
      <div v-else class="attr-grid">
        <div v-for="(value, key) in props.app?.attributes" :key="key" class="attr-item">
          <div class="attr-key">{{ key }}</div>
          <div class="attr-value">{{ value }}</div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.basic-info-panel {
  width: 100%;
}

.metrics-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.metric-label {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.metric-value {
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

.config-grid {
  display: grid;
  gap: 10px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.config-item {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 10px;
}

.config-key {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.config-value {
  font-size: 14px;
  font-weight: 600;
  margin-top: 4px;
  word-break: break-all;
}

.attr-grid {
  display: grid;
  gap: 10px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.attr-item {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 10px;
}

.attr-key {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.attr-value {
  font-size: 14px;
  font-weight: 600;
  margin-top: 4px;
  word-break: break-all;
}

@media (max-width: 1280px) {
  .metrics-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .attr-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }

  .config-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
