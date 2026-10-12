<script lang="ts" setup>
import type {
  MonitorTargetAppGroup,
  MonitorTargetSidebarGroup,
} from './types';

import { ref, watch } from 'vue';

import { getAppDisplayName } from './appPanelUtils';

const props = withDefaults(defineProps<{
  groups: MonitorTargetSidebarGroup[];
  loading?: boolean;
  metricsConfigured?: boolean;
  modelValue: string;
  showHistory?: boolean;
}>(), {
  metricsConfigured: false,
  showHistory: false,
});

const emit = defineEmits<{
  (e: 'create-group'): void;
  (e: 'edit-app', value: MonitorTargetAppGroup): void;
  (e: 'edit-group', value: MonitorTargetSidebarGroup): void;
  (e: 'refresh'): void;
  (e: 'update:showHistory', value: boolean): void;
  (e: 'update:modelValue', value: string): void;
}>();

const expandedAppKeys = ref<string[]>([]);
const expandedGroupKeys = ref<string[]>([]);

function selectApp(itemKey: string) {
  emit('update:modelValue', itemKey);
}

function formatHeartbeat(ts: null | number) {
  if (!ts) {
    return '无心跳';
  }
  const diffSeconds = Math.max(0, Math.floor(Date.now() / 1000) - ts);
  if (diffSeconds < 60) {
    return `${diffSeconds} 秒前`;
  }
  if (diffSeconds < 3600) {
    return `${Math.floor(diffSeconds / 60)} 分钟前`;
  }
  return `${Math.floor(diffSeconds / 3600)} 小时前`;
}

function truncateMiddle(value: string, start = 8, end = 6) {
  if (!value || value.length <= start + end + 1) {
    return value || '--';
  }
  return `${value.slice(0, start)}...${value.slice(-end)}`;
}

function isAppExpanded(groupKey: string) {
  return expandedAppKeys.value.includes(groupKey);
}

function isGroupExpanded(groupKey: string) {
  return expandedGroupKeys.value.includes(groupKey);
}

function toggleApp(groupKey: string) {
  if (isAppExpanded(groupKey)) {
    expandedAppKeys.value = expandedAppKeys.value.filter((key) => key !== groupKey);
    return;
  }
  expandedAppKeys.value = [...expandedAppKeys.value, groupKey];
}

function toggleGroup(groupKey: string) {
  if (isGroupExpanded(groupKey)) {
    expandedGroupKeys.value = expandedGroupKeys.value.filter((key) => key !== groupKey);
    return;
  }
  expandedGroupKeys.value = [...expandedGroupKeys.value, groupKey];
}

watch(
  () => props.groups.map((group) => group.groupKey).join('|'),
  () => {
    const existingGroupKeys = new Set(props.groups.map((group) => group.groupKey));
    const existingAppKeys = new Set(
      props.groups.flatMap((group) => group.apps.map((app) => app.groupKey)),
    );

    expandedGroupKeys.value = expandedGroupKeys.value.filter((groupKey) =>
      existingGroupKeys.has(groupKey),
    );
    expandedAppKeys.value = expandedAppKeys.value.filter((appKey) =>
      existingAppKeys.has(appKey),
    );
  },
  { immediate: true },
);
</script>

<template>
  <div class="app-sidebar">
    <div class="sidebar-header">
      <div class="title">应用监测</div>
      <div class="header-actions">
        <el-checkbox
          :model-value="props.showHistory"
          size="small"
          @change="(value) => emit('update:showHistory', Boolean(value))"
        >
          显示历史
        </el-checkbox>
        <button
          v-if="props.metricsConfigured"
          class="icon-button"
          title="新增分组"
          type="button"
          @click="emit('create-group')"
        >
          <svg
            fill="none"
            stroke="currentColor"
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
          >
            <path d="M12 5v14" />
            <path d="M5 12h14" />
          </svg>
        </button>
        <button
          class="icon-button primary"
          title="刷新"
          type="button"
          @click="emit('refresh')"
        >
          <svg
            fill="none"
            stroke="currentColor"
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
          >
            <path d="M23 4v6h-6" />
            <path d="M1 20v-6h6" />
            <path d="M3.51 9a9 9 0 0 1 14.13-3.36L23 10" />
            <path d="M20.49 15a9 9 0 0 1-14.13 3.36L1 14" />
          </svg>
        </button>
      </div>
    </div>

    <div v-loading="props.loading" class="list-wrap">
      <el-empty v-if="!props.groups.length && !props.loading" description="暂无应用" />

      <section
        v-for="group in props.groups"
        :key="group.groupKey"
        class="monitor-group"
      >
        <div class="group-header">
          <button
            class="group-toggle"
            type="button"
            @click="toggleGroup(group.groupKey)"
          >
            <div class="group-title-wrap">
              <span class="group-arrow" :class="{ expanded: isGroupExpanded(group.groupKey) }">
                <svg
                  fill="none"
                  stroke="currentColor"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  viewBox="0 0 24 24"
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <path d="m9 18 6-6-6-6" />
                </svg>
              </span>
              <span class="group-name">{{ group.groupName }}</span>
            </div>
            <span class="header-meta">
              <span
                v-if="props.metricsConfigured && !group.isDefault"
                class="icon-button subtle inline-action"
                title="编辑分组"
                @click.stop="emit('edit-group', group)"
              >
                <svg
                  fill="none"
                  stroke="currentColor"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  viewBox="0 0 24 24"
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <path d="M12 20h9" />
                  <path
                    d="M16.5 3.5a2.121 2.121 0 1 1 3 3L7 19l-4 1 1-4 12.5-12.5Z"
                  />
                </svg>
              </span>
              <span class="group-count">在线 {{ group.onlineCount }}</span>
            </span>
          </button>
        </div>

        <div v-if="isGroupExpanded(group.groupKey)" class="group-apps">
          <div
            v-for="app in group.apps"
            :key="app.groupKey"
            class="app-group"
          >
            <div class="app-group-header">
              <button
                class="app-toggle"
                :class="{ alerting: app.hasActiveAppAlert }"
                type="button"
                @click="toggleApp(app.groupKey)"
              >
                <div class="group-title-wrap">
                  <span class="group-arrow" :class="{ expanded: isAppExpanded(app.groupKey) }">
                    <svg
                      fill="none"
                      stroke="currentColor"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      viewBox="0 0 24 24"
                      xmlns="http://www.w3.org/2000/svg"
                    >
                      <path d="m9 18 6-6-6-6" />
                    </svg>
                  </span>
                  <span class="app-name">
                    {{ app.appTarget?.targetName || getAppDisplayName(app.appName) }}
                  </span>
                </div>
                <span class="header-meta">
                  <span
                    v-if="props.metricsConfigured"
                    class="icon-button subtle inline-action"
                    title="编辑应用"
                    @click.stop="emit('edit-app', app)"
                  >
                    <svg
                      fill="none"
                      stroke="currentColor"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      viewBox="0 0 24 24"
                      xmlns="http://www.w3.org/2000/svg"
                    >
                      <path d="M12 20h9" />
                      <path
                        d="M16.5 3.5a2.121 2.121 0 1 1 3 3L7 19l-4 1 1-4 12.5-12.5Z"
                      />
                    </svg>
                  </span>
                  <span class="group-count app-count">在线 {{ app.onlineCount }}</span>
                </span>
              </button>
            </div>

            <div v-if="isAppExpanded(app.groupKey)" class="group-items">
              <el-card
                v-for="item in app.items"
                :key="item.itemKey"
                class="app-item"
                :class="{
                  selected: props.modelValue === item.itemKey,
                  'instance-alert': item.hasActiveInstanceAlert,
                }"
                shadow="hover"
                @click="selectApp(item.itemKey)"
              >
                <div class="server-row" :title="item.hostName || '--'">
                  <span class="server-icon" aria-hidden="true">
                    <svg
                      fill="none"
                      stroke="currentColor"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      stroke-width="2"
                      viewBox="0 0 24 24"
                      xmlns="http://www.w3.org/2000/svg"
                    >
                      <rect x="3" y="4" width="18" height="7" rx="2" />
                      <rect x="3" y="13" width="18" height="7" rx="2" />
                      <path d="M7 8h.01" />
                      <path d="M7 17h.01" />
                      <path d="M11 8h6" />
                      <path d="M11 17h6" />
                    </svg>
                  </span>
                  <span class="server-name">{{ truncateMiddle(item.hostName || '--') }}</span>
                </div>
                <div class="meta-row meta-row-heartbeat">
                  <span class="env-inline" :title="item.environmentName || '--'">
                    <span class="env-dot" aria-hidden="true"></span>
                    <span class="env-name">{{ item.environmentName || '--' }}</span>
                  </span>
                  <span class="heartbeat-spacer"></span>
                  <span class="heartbeat-value-wrap">
                    <span class="heartbeat-icon" aria-hidden="true">
                      <svg
                        fill="none"
                        stroke="currentColor"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        viewBox="0 0 24 24"
                        xmlns="http://www.w3.org/2000/svg"
                      >
                        <path d="M22 12h-4l-3 7-4-14-3 7H2" />
                      </svg>
                    </span>
                    <span class="heartbeat-time">{{ formatHeartbeat(item.heartbeatTimestamp) }}</span>
                  </span>
                </div>
                <div class="meta-row meta-row-instance">
                  <span class="job-text">{{ item.instance || '--' }}</span>
                </div>
              </el-card>
            </div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.app-sidebar {
  border-right: 1px solid var(--el-border-color);
  height: 100%;
  padding: 12px;
  width: 340px;
}

.sidebar-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}

.header-actions {
  align-items: center;
  display: flex;
  gap: 8px;
}

.title {
  font-size: 16px;
  font-weight: 600;
}

.list-wrap {
  height: calc(100% - 44px);
  overflow: auto;
}

.monitor-group {
  margin-bottom: 12px;
}

.group-header,
.app-group-header {
  align-items: center;
  display: flex;
  gap: 8px;
}

.group-toggle,
.app-toggle {
  align-items: center;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  color: inherit;
  cursor: pointer;
  display: flex;
  flex: 1;
  justify-content: space-between;
  min-width: 0;
  padding: 10px 12px;
  text-align: left;
}

.group-toggle.alerting,
.app-toggle.alerting {
  background: color-mix(in srgb, var(--el-color-danger-light-9) 82%, var(--el-bg-color));
  border-color: color-mix(in srgb, var(--el-color-danger) 40%, var(--el-border-color));
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--el-color-danger) 14%, transparent);
}

.app-toggle {
  background: var(--el-bg-color-page);
}

.header-meta {
  align-items: center;
  display: inline-flex;
  flex: 0 0 auto;
  gap: 8px;
  margin-left: 12px;
}

.inline-action {
  pointer-events: auto;
}

.group-apps {
  margin-top: 8px;
}

.app-group {
  margin-bottom: 8px;
  margin-left: 12px;
}

.group-title-wrap {
  align-items: center;
  display: flex;
  gap: 8px;
  min-width: 0;
}

.group-arrow {
  color: var(--el-text-color-secondary);
  display: inline-flex;
  flex: 0 0 auto;
  height: 14px;
  transition: transform 0.2s ease;
  width: 14px;
}

.group-arrow.expanded {
  transform: rotate(90deg);
}

.group-name,
.app-name {
  font-size: 14px;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.group-count {
  background: var(--el-color-primary-light-9);
  border-radius: 999px;
  color: var(--el-color-primary);
  flex: 0 0 auto;
  font-size: 12px;
  line-height: 1;
  padding: 4px 8px;
}

.app-count {
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
}

.group-items {
  margin-top: 8px;
}

.app-item {
  cursor: pointer;
  margin-bottom: 8px;
  margin-left: 12px;
}

.app-item.selected {
  border-color: var(--el-color-primary);
}

.app-item.instance-alert {
  border-color: color-mix(in srgb, var(--el-color-danger) 45%, var(--el-border-color));
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--el-color-danger) 18%, transparent);
}

.icon-button {
  align-items: center;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  color: var(--el-text-color-regular);
  cursor: pointer;
  display: inline-flex;
  flex: 0 0 auto;
  height: 32px;
  justify-content: center;
  padding: 0;
  width: 32px;
}

.icon-button svg {
  height: 14px;
  width: 14px;
}

.icon-button.primary {
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: white;
}

.icon-button.subtle {
  opacity: 0;
  transition: opacity 0.2s ease;
}

.group-toggle:hover .icon-button.subtle,
.app-toggle:hover .icon-button.subtle {
  opacity: 1;
}

.server-row {
  align-items: center;
  color: var(--el-text-color-regular);
  display: flex;
  gap: 6px;
  margin-bottom: 8px;
}

.server-icon {
  align-items: center;
  color: var(--el-text-color-secondary);
  display: inline-flex;
  flex: 0 0 auto;
  height: 12px;
  justify-content: center;
  width: 12px;
}

.server-name {
  flex: 1 1 auto;
  font-size: 12px;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.meta-row {
  align-items: center;
  display: flex;
  gap: 8px;
  min-width: 0;
}

.meta-row + .meta-row {
  margin-top: 6px;
}

.env-inline {
  align-items: center;
  color: color-mix(in srgb, var(--el-color-primary) 82%, var(--el-text-color-secondary));
  display: inline-flex;
  flex: 0 1 auto;
  gap: 5px;
  max-width: 45%;
  min-width: 0;
}

.env-name,
.job-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.env-dot {
  background: currentcolor;
  border-radius: 999px;
  display: block;
  flex: 0 0 auto;
  height: 6px;
  width: 6px;
}

.heartbeat-spacer {
  flex: 1 1 auto;
}

.heartbeat-value-wrap {
  align-items: center;
  color: var(--el-text-color-secondary);
  display: inline-flex;
  flex: 0 0 auto;
  font-size: 12px;
  gap: 4px;
}

.heartbeat-icon {
  display: inline-flex;
  height: 12px;
  width: 12px;
}

.job-text {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  width: 100%;
}
</style>
