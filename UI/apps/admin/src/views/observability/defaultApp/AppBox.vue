<script lang="ts" setup>
import { ElMessage } from 'element-plus';
import { computed, ref, watch } from 'vue';

import { defaultAppPanelConfig, type AppPanelKey } from './appPanelConfig';
import {
  getAppDisplayName,
  getEnvironmentText,
  getInstanceIdText,
  getShortInstanceIdText,
  getStatusInfo,
} from './appPanelUtils';
import type { AppViewModel, MonitorTargetConfig } from './types';

const props = defineProps<{
  app: AppViewModel | null;
  autoRefreshEnabled?: boolean;
  refreshKey?: number;
  refreshing?: boolean;
  targetConfig?: MonitorTargetConfig | null;
}>();

const emit = defineEmits<{
  refresh: [];
  'update:autoRefreshEnabled': [value: boolean];
}>();

const activeView = ref<AppPanelKey>('basic');

const panels = computed(() => defaultAppPanelConfig.getPanels(props.app?.appName));
const currentPanel = computed(
  () => panels.value.find((item) => item.key === activeView.value) || panels.value[0] || null,
);
const displayName = computed(() =>
  props.app ? getAppDisplayName(props.app.jobDisplayName || props.app.appName) : '',
);
const appIdentity = computed(() =>
  props.app ? `${props.app.appName}@@${props.app.instance}` : '',
);

const environmentText = computed(() => getEnvironmentText(props.app));
const statusInfo = computed(() => getStatusInfo(props.app));
const instanceIdText = computed(() => getInstanceIdText(props.app));
const shortInstanceIdText = computed(() => getShortInstanceIdText(instanceIdText.value));
const autoRefreshModel = computed({
  get: () => props.autoRefreshEnabled ?? true,
  set: (value: boolean) => emit('update:autoRefreshEnabled', value),
});

watch(
  () => ({
    identity: appIdentity.value,
    panelKeys: panels.value.map((item) => item.key).join('|'),
  }),
  (current, previous) => {
    if (!previous || current.identity !== previous.identity) {
      activeView.value = panels.value[0]?.key || 'basic';
      return;
    }

    if (!panels.value.some((item) => item.key === activeView.value)) {
      activeView.value = panels.value[0]?.key || 'basic';
    }
  },
  { immediate: true },
);

async function copyInstanceId() {
  const value = instanceIdText.value;
  if (!value || value === '--') {
    ElMessage.warning('实例ID为空，无法复制');
    return;
  }
  try {
    await navigator.clipboard.writeText(value);
    ElMessage.success('实例ID已复制');
  } catch {
    ElMessage.error('复制失败，请手动复制');
  }
}

function handleRefresh() {
  emit('refresh');
}
</script>

<template>
  <div class="app-box">
    <el-empty v-if="!props.app" description="请先选择应用" />

    <template v-else>
      <div class="header">
        <div class="header-top">
          <div class="header-main">
            <div class="title-row">
              <div class="title">{{ displayName }}</div>
              <el-tag size="small" type="info">{{ environmentText }}</el-tag>
              <el-tag :type="statusInfo.type" size="small">
                <span class="status-dot">●</span>
                {{ statusInfo.text }}
              </el-tag>
            </div>
            <div class="header-meta-line">
              <span class="meta-item mono" :title="instanceIdText">实例ID: {{ shortInstanceIdText }}</span>
            </div>
          </div>
          <div class="header-actions">
            <el-button plain size="small" @click="copyInstanceId">复制实例ID</el-button>
            <div class="refresh-actions">
              <el-button :loading="props.refreshing" plain size="small" @click="handleRefresh">
                刷新
              </el-button>
              <div class="refresh-tools">
                <span class="refresh-label">自动刷新</span>
                <el-switch v-model="autoRefreshModel" inline-prompt active-text="开" inactive-text="关" />
              </div>
            </div>
          </div>
        </div>
        <el-radio-group v-model="activeView" class="view-switch" size="small">
          <el-radio-button v-for="panel in panels" :key="panel.key" :label="panel.key">
            {{ panel.label }}
          </el-radio-button>
        </el-radio-group>
      </div>

      <div class="content-scroll">
        <component
          :is="currentPanel.component"
          v-if="currentPanel"
          :app="props.app"
          :refresh-key="props.refreshKey || 0"
          :target-config="props.targetConfig || null"
        />
      </div>
    </template>
  </div>
</template>

<style scoped>
.app-box {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 16px;
  overflow: hidden;
}

.header {
  position: sticky;
  top: 0;
  z-index: 3;
  padding: 16px;
  margin-bottom: 12px;
  background: var(--el-bg-color);
}

.content-scroll {
  flex: 1;
  min-height: 0;
  padding-right: 2px;
  overflow: auto;
}

.header-top {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  justify-content: space-between;
}

.header-main {
  flex: 1;
  min-width: 0;
}

.title-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.title {
  font-size: 20px;
  font-weight: 700;
}

.view-switch {
  margin-top: 10px;
}

.header-meta-line {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
  margin-top: 6px;
}

.meta-item {
  max-width: 100%;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  word-break: break-all;
}

.mono {
  font-family: var(--vxe-font-family);
}

.status-dot {
  font-size: 12px;
}

.header-actions {
  display: flex;
  flex: 0 0 auto;
  flex-direction: column;
  gap: 8px;
  align-items: stretch;
  min-width: 220px;
}

.refresh-actions {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.refresh-tools {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.refresh-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

@media (max-width: 1280px) {
  .header-top {
    flex-direction: column;
    gap: 10px;
  }

  .header-actions {
    width: 100%;
  }

  .header-actions :deep(.el-button) {
    width: 100%;
  }
}
</style>
