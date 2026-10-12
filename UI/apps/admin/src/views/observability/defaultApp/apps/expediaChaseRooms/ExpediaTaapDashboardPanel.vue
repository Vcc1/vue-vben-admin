<script lang="ts" setup>
import type { AppViewModel } from '../../types';
import type { ExpediaOverviewFilter, ExpediaResolvedTimeRange } from './types';

import { computed, onMounted, onUnmounted, ref, watch } from 'vue';

import { ElMessage } from 'element-plus';

import {
  loadExpediaTaapAccountNames,
  loadExpediaTaapDashboardData,
} from './api';
import AccountTable from './components/AccountTable.vue';
import LatencyChartsPanel from './components/LatencyChartsPanel.vue';
import OverviewCards from './components/OverviewCards.vue';
import TrendChartPanel from './components/TrendChartPanel.vue';
import { buildQueryStepSeconds, formatDateTime } from './formatters';
import { EXPEDIA_TIME_RANGE_OPTIONS } from './metrics';

defineOptions({ name: 'ExpediaTaapDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
}>();

const loading = ref(false);
const accountLoading = ref(false);
const errorMessage = ref('');
const selectedRange = ref<'1h' | '6h' | '15m' | '24h' | 'custom'>('1h');
const customRange = ref<[] | [Date, Date]>([]);
const selectedAccounts = ref<string[]>([]);
const accountOptions = ref<string[]>([]);
const autoRefreshEnabled = ref(true);
const dashboardData = ref<Awaited<
  ReturnType<typeof loadExpediaTaapDashboardData>
> | null>(null);
const activeOverviewFilter = ref<ExpediaOverviewFilter>(null);
const lastLoadedAt = ref(0);

let refreshTimer: number | undefined;
let requestId = 0;

function buildDefaultCustomRange(): [Date, Date] {
  return [new Date(Date.now() - 60 * 60 * 1000), new Date()];
}

const currentScope = computed(() => {
  const exportedInstance = props.app?.instances[0] || props.app?.instance || '';
  const exportedJob = props.app?.jobs[0] || '';

  if (!exportedInstance || !exportedJob) {
    return null;
  }

  return {
    exportedInstance,
    exportedJob,
    otelScopeName: 'AppMetrics',
  };
});

const scopeKey = computed(() => {
  if (!currentScope.value) {
    return '';
  }

  return `${currentScope.value.exportedInstance}|${currentScope.value.exportedJob}`;
});

const filterKey = computed(() =>
  JSON.stringify({
    accounts: [...selectedAccounts.value].sort(),
    customRange: customRange.value.map((item) => item.getTime()),
    range: selectedRange.value,
  }),
);

const effectiveTimeRange = computed<ExpediaResolvedTimeRange>(() => {
  const nowSeconds = Math.floor(Date.now() / 1000);

  if (selectedRange.value === 'custom' && customRange.value.length === 2) {
    const start = Math.floor(customRange.value[0].getTime() / 1000);
    const end = Math.floor(customRange.value[1].getTime() / 1000);
    return {
      end,
      key: 'custom',
      start,
      step: buildQueryStepSeconds(start, end),
    };
  }

  const rangeSecondsMap = {
    '15m': 15 * 60,
    '1h': 60 * 60,
    '6h': 6 * 60 * 60,
    '24h': 24 * 60 * 60,
  } as const;
  const duration =
    rangeSecondsMap[selectedRange.value as '1h' | '6h' | '15m' | '24h'];
  const start = nowSeconds - duration;

  return {
    end: nowSeconds,
    key: selectedRange.value,
    start,
    step: buildQueryStepSeconds(start, nowSeconds),
  };
});

const selectedAccountSummary = computed(() => {
  if (accountOptions.value.length === 0) {
    return '暂无账号';
  }
  if (selectedAccounts.value.length === 0) {
    return `全部 ${accountOptions.value.length} 个账号`;
  }
  return `${selectedAccounts.value.length} / ${accountOptions.value.length} 个账号`;
});

const filteredPendingTrend = computed(() => {
  const series = dashboardData.value?.pendingTrend || [];
  if (activeOverviewFilter.value?.kind === 'pendingPriority') {
    return series.filter(
      (item) => item.key === activeOverviewFilter.value?.value,
    );
  }
  return series;
});

const filteredResultTrend = computed(() => {
  const series = dashboardData.value?.resultTrend || [];
  if (activeOverviewFilter.value?.kind === 'taskResult') {
    return series.filter(
      (item) => item.key === activeOverviewFilter.value?.value,
    );
  }
  return series;
});

const activeOverviewFilterText = computed(() => {
  const current = activeOverviewFilter.value;
  if (!current) {
    return '';
  }

  if (current.kind === 'pendingPriority') {
    return current.value === 'high' ? '高优先级待执行任务' : '普通待执行任务';
  }

  if (current.kind === 'taskResult') {
    return `任务结果 ${current.value}`;
  }

  return current.value === 'workerCount'
    ? '当前 worker 总数'
    : '当前处理中任务总数';
});

const showEmptyState = computed(
  () =>
    !loading.value &&
    !errorMessage.value &&
    dashboardData.value &&
    !dashboardData.value.hasAnyData,
);

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}

async function loadAccountOptions(silent = false) {
  if (!currentScope.value) {
    accountOptions.value = [];
    return;
  }

  accountLoading.value = true;
  try {
    const options = await loadExpediaTaapAccountNames(currentScope.value);
    accountOptions.value = options;
    selectedAccounts.value = selectedAccounts.value.filter((item) =>
      options.includes(item),
    );
  } catch (error) {
    const message = getErrorMessage(error);
    if (!silent && !dashboardData.value) {
      errorMessage.value = message;
    }
    if (silent) {
      ElMessage.warning(`Expedia TAAP 账号列表刷新失败: ${message}`);
    }
  } finally {
    accountLoading.value = false;
  }
}

async function loadDashboard(silent = false) {
  if (!currentScope.value) {
    dashboardData.value = null;
    return;
  }

  const currentRequestId = ++requestId;
  if (!silent) {
    loading.value = true;
  }

  try {
    const result = await loadExpediaTaapDashboardData({
      accountNames: accountOptions.value,
      accounts: selectedAccounts.value,
      targetScope: currentScope.value,
      timeRange: effectiveTimeRange.value,
    });

    if (currentRequestId !== requestId) {
      return;
    }

    dashboardData.value = result;
    errorMessage.value = '';
    lastLoadedAt.value = Date.now();
  } catch (error) {
    if (currentRequestId !== requestId) {
      return;
    }

    const message = getErrorMessage(error);
    errorMessage.value = message;
    if (silent) {
      ElMessage.error(`Expedia TAAP 监控刷新失败: ${message}`);
    }
  } finally {
    if (!silent && currentRequestId === requestId) {
      loading.value = false;
    }
  }
}

async function refreshAll() {
  await loadAccountOptions(true);
  await loadDashboard();
}

function toggleOverviewFilter(filter: ExpediaOverviewFilter) {
  if (!filter) {
    return;
  }

  if (
    activeOverviewFilter.value &&
    activeOverviewFilter.value.kind === filter.kind &&
    activeOverviewFilter.value.value === filter.value
  ) {
    activeOverviewFilter.value = null;
    return;
  }

  activeOverviewFilter.value = filter;
}

watch(
  () => selectedRange.value,
  (value) => {
    if (value === 'custom' && customRange.value.length !== 2) {
      customRange.value = buildDefaultCustomRange();
    }
  },
  { immediate: true },
);

watch(
  () => scopeKey.value,
  async (value) => {
    activeOverviewFilter.value = null;
    selectedAccounts.value = [];
    dashboardData.value = null;
    errorMessage.value = '';

    if (!value) {
      return;
    }

    await loadAccountOptions();
    await loadDashboard();
  },
  { immediate: true },
);

watch(
  () => filterKey.value,
  async () => {
    if (!scopeKey.value) {
      return;
    }

    await loadDashboard();
  },
);

onMounted(() => {
  refreshTimer = window.setInterval(async () => {
    if (!autoRefreshEnabled.value || !scopeKey.value) {
      return;
    }

    await loadDashboard(true);
  }, 30_000);
});

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer);
  }
});
</script>

<template>
  <div class="expedia-dashboard">
    <el-empty
      v-if="!currentScope"
      description="当前应用缺少 exported_instance 或 exported_job 标签，无法加载 Expedia TAAP 监控面板。"
    />

    <template v-else>
      <el-card class="toolbar-card" shadow="never">
        <div class="toolbar-shell">
          <div class="toolbar-main">
            <div class="filter-group">
              <div class="filter-label">时间范围</div>
              <el-radio-group v-model="selectedRange" size="small">
                <el-radio-button
                  v-for="item in EXPEDIA_TIME_RANGE_OPTIONS"
                  :key="item.value"
                  :label="item.value"
                >
                  {{ item.label }}
                </el-radio-button>
              </el-radio-group>
            </div>

            <div
              v-if="selectedRange === 'custom'"
              class="filter-group filter-grow"
            >
              <div class="filter-label">自定义时间</div>
              <el-date-picker
                v-model="customRange"
                :clearable="false"
                end-placeholder="结束时间"
                format="YYYY-MM-DD HH:mm:ss"
                start-placeholder="开始时间"
                type="datetimerange"
                unlink-panels
              />
            </div>

            <div class="filter-group filter-grow">
              <div class="filter-label">account_name</div>
              <el-select
                v-model="selectedAccounts"
                :loading="accountLoading"
                clearable
                collapse-tags
                collapse-tags-tooltip
                filterable
                multiple
                placeholder="全部账号"
              >
                <el-option
                  v-for="item in accountOptions"
                  :key="item"
                  :label="item"
                  :value="item"
                />
              </el-select>
            </div>

            <div class="filter-group">
              <div class="filter-label">自动刷新</div>
              <div class="refresh-box">
                <el-switch v-model="autoRefreshEnabled" />
                <span>30 秒</span>
              </div>
            </div>
          </div>

          <div class="toolbar-side">
            <div class="toolbar-meta">
              <span>筛选账号：{{ selectedAccountSummary }}</span>
              <span>更新时间：{{ formatDateTime(lastLoadedAt) }}</span>
            </div>
            <el-button plain size="small" type="primary" @click="refreshAll">
              立即刷新
            </el-button>
          </div>
        </div>
      </el-card>

      <el-alert
        v-if="activeOverviewFilter"
        class="mt-3"
        :closable="true"
        :title="`总览联动筛选：${activeOverviewFilterText}`"
        show-icon
        type="info"
        @close="activeOverviewFilter = null"
      />

      <el-alert
        v-if="errorMessage"
        class="mt-3"
        :closable="false"
        show-icon
        type="error"
      >
        <template #title>Prometheus 请求失败：{{ errorMessage }}</template>
        <template #default>
          <el-button link type="primary" @click="refreshAll">重试</el-button>
        </template>
      </el-alert>

      <el-empty
        v-if="showEmptyState"
        class="empty-state"
        description="当前时间范围内没有 Expedia TAAP 指标数据。"
      />

      <div v-else v-loading="loading" class="dashboard-body">
        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">区域 A · 总览卡片</div>
            <div class="section-desc">
              当前值和最近 1 小时任务结果，点击卡片可联动下方图表或表格排序。
            </div>
          </div>
          <OverviewCards
            :active-filter="activeOverviewFilter"
            :cards="dashboardData?.overviewCards || []"
            @toggle-filter="toggleOverviewFilter"
          />
        </section>

        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">区域 B · 趋势图</div>
            <div class="section-desc">
              数据库待执行任务、任务结果和会话事件趋势，默认按当前应用和所选
              account_name 聚合。
            </div>
          </div>
          <div class="chart-grid chart-grid-3">
            <el-card shadow="never">
              <TrendChartPanel
                :loading="loading"
                :series="filteredPendingTrend"
                title="数据库待执行任务趋势"
              />
            </el-card>
            <el-card shadow="never">
              <TrendChartPanel
                :loading="loading"
                :series="filteredResultTrend"
                stack
                title="任务结果趋势"
              />
            </el-card>
            <el-card shadow="never">
              <TrendChartPanel
                :loading="loading"
                :series="dashboardData?.sessionTrend || []"
                stack
                title="会话事件趋势"
              />
            </el-card>
          </div>
        </section>

        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">区域 C · 耗时图</div>
            <div class="section-desc">
              基于 histogram bucket、sum、count 计算 P50、P95
              和平均耗时，不直接向业务展示 le。
            </div>
          </div>
          <LatencyChartsPanel
            :charts="dashboardData?.latencyCharts || []"
            :loading="loading"
          />
        </section>

        <section class="dashboard-section">
          <div class="section-header">
            <div class="section-title">区域 D · 账号维度表格</div>
            <div class="section-desc">
              默认按 error 降序、其次按处理中任务数降序；支持点击表头重新排序。
            </div>
          </div>
          <el-card shadow="never">
            <AccountTable
              :focus-filter="activeOverviewFilter"
              :loading="loading"
              :rows="dashboardData?.accountRows || []"
            />
          </el-card>
        </section>
      </div>
    </template>
  </div>
</template>

<style scoped>
.expedia-dashboard {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.toolbar-card,
.dashboard-section > .el-card {
  border-radius: 12px;
}

.toolbar-shell {
  display: flex;
  gap: 16px;
  justify-content: space-between;
}

.toolbar-main {
  display: flex;
  flex: 1;
  flex-wrap: wrap;
  gap: 12px;
}

.toolbar-side {
  display: flex;
  flex-direction: column;
  gap: 10px;
  align-items: flex-end;
  justify-content: space-between;
  min-width: 220px;
}

.toolbar-meta {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  text-align: right;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.filter-grow {
  flex: 1;
  min-width: 260px;
}

.filter-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.refresh-box {
  display: flex;
  gap: 10px;
  align-items: center;
  height: 32px;
}

.dashboard-body {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.dashboard-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.section-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.section-desc {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.chart-grid {
  display: grid;
  gap: 12px;
}

.chart-grid-3 {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.empty-state {
  background: var(--el-bg-color-overlay);
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
}

@media (max-width: 1440px) {
  .chart-grid-3 {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}

@media (max-width: 992px) {
  .toolbar-shell {
    flex-direction: column;
  }

  .toolbar-side {
    align-items: stretch;
    min-width: 0;
  }

  .toolbar-meta {
    text-align: left;
  }
}
</style>
