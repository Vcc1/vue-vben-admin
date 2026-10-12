<script lang="ts" setup>
import type { AppLatestMetricValue, AppViewModel } from '../../types';

import { computed } from 'vue';

import { formatHeartbeat, formatSeconds } from '../../appPanelUtils';

defineOptions({ name: 'AppMetricsDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
}>();

interface ParsedMetricRow {
  labels: Record<string, string>;
  labelsText: string;
  metricName: string;
  numericValue: null | number;
  valueText: string;
}

interface SummaryCard {
  label: string;
  value: string;
}

interface DurationSummaryRow {
  averageMs: string;
  count: string;
  name: string;
  totalMs: string;
  upperBound: string;
}

interface LabelValueRow {
  label: string;
  value: string;
}

interface RequestDurationRow {
  averageMs: string;
  count: string;
  requestType: string;
  route: string;
  totalMs: string;
  upperBound: string;
}

function parseLabels(labelsText: string) {
  const result: Record<string, string> = {};
  for (const part of labelsText.split(', ')) {
    const index = part.indexOf('=');
    if (index < 0) {
      continue;
    }
    const key = part.slice(0, index).trim();
    const value = part.slice(index + 1).trim();
    if (key) {
      result[key] = value;
    }
  }
  return result;
}

function parseMetricValue(item: AppLatestMetricValue): ParsedMetricRow {
  const numericValue = Number(item.valueText);
  return {
    labels: parseLabels(item.labelsText),
    labelsText: item.labelsText,
    metricName: item.metricName,
    numericValue: Number.isFinite(numericValue) ? numericValue : null,
    valueText: item.valueText,
  };
}

function formatInteger(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 0 }).format(value);
}

function formatNumber(value: null | number, digits = 2) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: digits,
    minimumFractionDigits: 0,
  }).format(value);
}

function formatPercent(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return `${(value * 100).toFixed(2)}%`;
}

function formatDurationUpperBound(value: null | number) {
  if (value === null) {
    return '--';
  }
  if (!Number.isFinite(value)) {
    return '无限';
  }
  return `<= ${formatNumber(value, 0)} ms`;
}

function buildHistogramUpperBound(
  buckets: ParsedMetricRow[],
  totalCount: null | number,
) {
  if (!buckets.length || totalCount === null || totalCount <= 0) {
    return null;
  }

  const sorted = [...buckets].sort((a, b) => {
    const left = a.labels.le === '+Inf' ? Number.POSITIVE_INFINITY : Number(a.labels.le);
    const right = b.labels.le === '+Inf' ? Number.POSITIVE_INFINITY : Number(b.labels.le);
    return left - right;
  });

  const matched = sorted.find((row) => (row.numericValue ?? -1) >= totalCount);
  if (!matched) {
    return null;
  }

  if (matched.labels.le === '+Inf') {
    return Number.POSITIVE_INFINITY;
  }
  const upper = Number(matched.labels.le);
  return Number.isFinite(upper) ? upper : null;
}

const metricRows = computed(() =>
  (props.app?.latestMetricValues || []).map(parseMetricValue),
);

const appName = computed(() => props.app?.appName || '');
const isRoomMatching = computed(() => appName.value === 'RoomMatching');
const isConnectmateApi = computed(() =>
  ['ConnectmateApi', 'MengTuConnectmateApi'].includes(appName.value),
);

function getMetricRows(metricName: string, labels: Record<string, string> = {}) {
  return metricRows.value.filter((row) => {
    if (row.metricName !== metricName) {
      return false;
    }
    return Object.entries(labels).every(([key, value]) => row.labels[key] === value);
  });
}

function getMetricNumber(metricName: string, labels: Record<string, string> = {}) {
  const row = getMetricRows(metricName, labels)[0];
  return row?.numericValue ?? null;
}

function sumMetric(metricName: string, labels: Record<string, string> = {}) {
  return getMetricRows(metricName, labels).reduce(
    (sum, row) => sum + (row.numericValue || 0),
    0,
  );
}

function readDuration(name: string) {
  const countMetric = `room_matching_${name}_duration_ms_milliseconds_count`;
  const sumMetricName = `room_matching_${name}_duration_ms_milliseconds_sum`;
  const bucketMetric = `room_matching_${name}_duration_ms_milliseconds_bucket`;
  const countRows = getMetricRows(countMetric);

  return countRows
    .map((row) => {
      const labelName = row.labels.source ? 'source' : row.labels.step ? 'step' : '';
      const labelValue = (labelName ? row.labels[labelName] : name) || name;
      const labels: Record<string, string> = labelName ? { [labelName]: labelValue } : {};
      const count = row.numericValue;
      const sum = getMetricNumber(sumMetricName, labels);
      const upperBound = buildHistogramUpperBound(getMetricRows(bucketMetric, labels), count);

      return {
        averageMs: count && sum !== null ? formatNumber(sum / count, 2) : '--',
        count: formatInteger(count),
        name: labelValue || name,
        totalMs: formatNumber(sum, 2),
        upperBound: formatDurationUpperBound(upperBound),
      };
    })
    .sort((a, b) => a.name.localeCompare(b.name));
}

const roomMatchingSummaryCards = computed<SummaryCard[]>(() => {
  if (!isRoomMatching.value) {
    return [];
  }

  const current = getMetricNumber('room_matching_current_hotels_matched');
  const total = getMetricNumber('room_matching_current_hotels_total');

  return [
    { label: '酒店总量', value: formatInteger(total) },
    { label: '已读取酒店', value: formatInteger(getMetricNumber('room_matching_current_hotels_read')) },
    { label: '已匹配酒店', value: formatInteger(current) },
    { label: '已写入酒店', value: formatInteger(getMetricNumber('room_matching_current_hotels_written')) },
    { label: '已生成房型', value: formatInteger(getMetricNumber('room_matching_current_rooms_generated')) },
    { label: '已入库房型', value: formatInteger(getMetricNumber('room_matching_current_rooms_persisted')) },
    { label: '当前进度', value: formatPercent(getMetricNumber('work_loop_progress_ratio', { loop: 'roommatching_hotels' })) },
    { label: '错误数', value: formatInteger(sumMetric('room_matching_error_total')) },
    { label: '匹配 Worker', value: formatInteger(getMetricNumber('room_matching_active_match_workers')) },
    { label: '读取队列', value: formatInteger(getMetricNumber('room_matching_read_queue_depth')) },
    { label: '写入队列', value: formatInteger(getMetricNumber('room_matching_write_queue_depth')) },
    { label: '当前轮次', value: formatInteger(getMetricNumber('room_matching_current_round_id')) },
  ];
});

const roomMatchingProgress = computed(() => {
  if (!isRoomMatching.value) {
    return null;
  }

  const ratio =
    getMetricNumber('work_loop_progress_ratio', { loop: 'roommatching_hotels' }) ??
    (() => {
      const current = getMetricNumber('room_matching_current_hotels_matched');
      const total = getMetricNumber('room_matching_current_hotels_total');
      return current !== null && total ? current / total : 0;
    })();

  return {
    current: formatInteger(getMetricNumber('room_matching_current_hotels_matched')),
    total: formatInteger(getMetricNumber('room_matching_current_hotels_total')),
    value: ratio,
  };
});

const roomMatchingLastCards = computed<SummaryCard[]>(() => {
  if (!isRoomMatching.value) {
    return [];
  }

  return [
    { label: '最近批次耗时', value: `${formatNumber(getMetricNumber('room_matching_last_batch_duration_ms'), 0)} ms` },
    { label: '最近匹配轮耗时', value: `${formatNumber(getMetricNumber('room_matching_last_round_duration_ms'), 0)} ms` },
    { label: '最近写入耗时', value: `${formatNumber(getMetricNumber('room_matching_last_write_duration_ms'), 0)} ms` },
    { label: '最近错误时间', value: formatHeartbeat(getMetricNumber('room_matching_last_error_timestamp')) },
  ];
});

const roomMatchingMatchResults = computed<LabelValueRow[]>(() =>
  getMetricRows('room_matching_match_result_total')
    .map((row) => ({
      label: row.labels.status || '--',
      value: formatInteger(row.numericValue),
    }))
    .sort((a, b) => a.label.localeCompare(b.label)),
);

const roomMatchingRedisPush = computed<LabelValueRow[]>(() =>
  getMetricRows('room_matching_redis_push_total')
    .map((row) => ({
      label: [row.labels.target, row.labels.outcome].filter(Boolean).join(' / ') || '--',
      value: formatInteger(row.numericValue),
    }))
    .sort((a, b) => a.label.localeCompare(b.label)),
);

const roomMatchingErrors = computed<LabelValueRow[]>(() =>
  getMetricRows('room_matching_error_total')
    .map((row) => ({
      label: row.labels.stage || '--',
      value: formatInteger(row.numericValue),
    }))
    .sort((a, b) => a.label.localeCompare(b.label)),
);

const roomMatchingDurations = computed<DurationSummaryRow[]>(() => [
  ...readDuration('read').map((row) => ({ ...row, name: `读取: ${row.name}` })),
  ...readDuration('match').map((row) => ({ ...row, name: `匹配: ${row.name}` })),
  ...readDuration('write').map((row) => ({ ...row, name: `写入: ${row.name}` })),
]);

const connectmateSummaryCards = computed<SummaryCard[]>(() => {
  if (!isConnectmateApi.value) {
    return [];
  }

  const rows = getMetricRows('connectmate_client_configuration_request_duration_milliseconds_count');
  const totalRequests = rows.reduce((sum, row) => sum + (row.numericValue || 0), 0);
  const totalDuration = getMetricRows('connectmate_client_configuration_request_duration_milliseconds_sum')
    .reduce((sum, row) => sum + (row.numericValue || 0), 0);

  return [
    { label: '配置请求数', value: formatInteger(totalRequests) },
    { label: '总耗时', value: totalDuration === 0 ? '--' : `${formatNumber(totalDuration, 2)} ms` },
    {
      label: '平均耗时',
      value: totalRequests > 0 ? `${formatNumber(totalDuration / totalRequests, 2)} ms` : '--',
    },
    { label: '当前并发', value: formatInteger(props.app?.inflight ?? null) },
  ];
});

const connectmateRequestDurations = computed<RequestDurationRow[]>(() => {
  if (!isConnectmateApi.value) {
    return [];
  }

  const countRows = getMetricRows('connectmate_client_configuration_request_duration_milliseconds_count');
  return countRows
    .map((row) => {
      const route = row.labels.route || '--';
      const requestType = row.labels.request_type || '--';
      const count = row.numericValue;
      const sum = getMetricNumber('connectmate_client_configuration_request_duration_milliseconds_sum', {
        request_type: requestType,
        route,
      });
      const upperBound = buildHistogramUpperBound(
        getMetricRows('connectmate_client_configuration_request_duration_milliseconds_bucket', {
          request_type: requestType,
          route,
        }),
        count,
      );

      return {
        averageMs: count && sum !== null ? formatNumber(sum / count, 2) : '--',
        count: formatInteger(count),
        requestType,
        route,
        totalMs: formatNumber(sum, 2),
        upperBound: formatDurationUpperBound(upperBound),
      };
    })
    .sort((a, b) => a.route.localeCompare(b.route) || a.requestType.localeCompare(b.requestType));
});

const businessMetricCount = computed(
  () =>
    metricRows.value.filter(
      (row) =>
        row.metricName.startsWith('room_matching_') ||
        row.metricName.startsWith('work_loop_') ||
        row.metricName.startsWith('app_'),
    ).length,
);
</script>

<template>
  <div class="app-metrics-dashboard-panel">
    <div class="metrics-grid">
      <el-card shadow="never">
        <div class="metric-label">最近心跳</div>
        <div class="metric-value">{{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">运行时长</div>
        <div class="metric-value">{{ formatSeconds(props.app?.uptimeSeconds ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">当前 Inflight</div>
        <div class="metric-value">{{ formatInteger(props.app?.inflight ?? null) }}</div>
      </el-card>
      <el-card shadow="never">
        <div class="metric-label">业务指标数</div>
        <div class="metric-value">{{ formatInteger(businessMetricCount) }}</div>
      </el-card>
    </div>

    <template v-if="isRoomMatching">
      <el-card class="mt-3" shadow="never">
        <template #header>房型匹配概览</template>
        <div class="summary-grid">
          <div v-for="item in roomMatchingSummaryCards" :key="item.label" class="summary-item">
            <div class="summary-label">{{ item.label }}</div>
            <div class="summary-value">{{ item.value }}</div>
          </div>
        </div>
      </el-card>

      <el-card v-if="roomMatchingProgress" class="mt-3" shadow="never">
        <template #header>处理进度</template>
        <div class="progress-meta">
          <span>已匹配: {{ roomMatchingProgress.current }}</span>
          <span>总量: {{ roomMatchingProgress.total }}</span>
          <span>比例: {{ formatPercent(roomMatchingProgress.value) }}</span>
        </div>
        <el-progress
          :percentage="Number((Math.min(1, roomMatchingProgress.value) * 100).toFixed(2))"
          :stroke-width="16"
        />
      </el-card>

      <el-card class="mt-3" shadow="never">
        <template #header>最近状态</template>
        <div class="summary-grid compact-grid">
          <div v-for="item in roomMatchingLastCards" :key="item.label" class="summary-item">
            <div class="summary-label">{{ item.label }}</div>
            <div class="summary-value">{{ item.value }}</div>
          </div>
        </div>
      </el-card>

      <el-card class="mt-3" shadow="never">
        <template #header>匹配结果</template>
        <el-empty v-if="!roomMatchingMatchResults.length" description="暂无匹配结果指标" />
        <el-table v-else :data="roomMatchingMatchResults" stripe>
          <el-table-column label="状态" min-width="220" prop="label" />
          <el-table-column align="right" label="累计值" min-width="140" prop="value" />
        </el-table>
      </el-card>

      <el-card class="mt-3" shadow="never">
        <template #header>耗时分布</template>
        <el-empty v-if="!roomMatchingDurations.length" description="暂无耗时分布指标" />
        <el-table v-else :data="roomMatchingDurations" stripe>
          <el-table-column label="阶段" min-width="240" prop="name" />
          <el-table-column align="right" label="次数" min-width="120" prop="count" />
          <el-table-column align="right" label="总耗时(ms)" min-width="140" prop="totalMs" />
          <el-table-column align="right" label="平均(ms)" min-width="120" prop="averageMs" />
          <el-table-column label="落点上界" min-width="140" prop="upperBound" />
        </el-table>
      </el-card>

      <el-card class="mt-3" shadow="never">
        <template #header>Redis 推送</template>
        <el-empty v-if="!roomMatchingRedisPush.length" description="暂无 Redis 推送指标" />
        <el-table v-else :data="roomMatchingRedisPush" stripe>
          <el-table-column label="目标 / 结果" min-width="220" prop="label" />
          <el-table-column align="right" label="累计值" min-width="140" prop="value" />
        </el-table>
      </el-card>

      <el-card class="mt-3" shadow="never">
        <template #header>错误统计</template>
        <el-empty v-if="!roomMatchingErrors.length" description="暂无错误指标" />
        <el-table v-else :data="roomMatchingErrors" stripe>
          <el-table-column label="阶段" min-width="220" prop="label" />
          <el-table-column align="right" label="累计值" min-width="140" prop="value" />
        </el-table>
      </el-card>
    </template>

    <template v-else-if="isConnectmateApi">
      <el-card class="mt-3" shadow="never">
        <template #header>ConnectmateApi 概览</template>
        <div class="summary-grid compact-grid">
          <div v-for="item in connectmateSummaryCards" :key="item.label" class="summary-item">
            <div class="summary-label">{{ item.label }}</div>
            <div class="summary-value">{{ item.value }}</div>
          </div>
        </div>
      </el-card>

      <el-card class="mt-3" shadow="never">
        <template #header>配置接口请求耗时</template>
        <el-empty v-if="!connectmateRequestDurations.length" description="暂无 ConnectmateApi 请求耗时指标" />
        <el-table v-else :data="connectmateRequestDurations" stripe>
          <el-table-column label="路由" min-width="260" prop="route" />
          <el-table-column label="请求类型" min-width="140" prop="requestType" />
          <el-table-column align="right" label="次数" min-width="120" prop="count" />
          <el-table-column align="right" label="总耗时(ms)" min-width="140" prop="totalMs" />
          <el-table-column align="right" label="平均(ms)" min-width="120" prop="averageMs" />
          <el-table-column label="落点上界" min-width="140" prop="upperBound" />
        </el-table>
      </el-card>
    </template>

    <el-empty v-else description="当前应用暂无专用 AppMetrics 面板" />
  </div>
</template>

<style scoped>
.app-metrics-dashboard-panel {
  width: 100%;
}

.metrics-grid,
.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.compact-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.metric-label,
.summary-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-value,
.summary-value {
  margin-top: 6px;
  font-size: 18px;
  font-weight: 700;
  word-break: break-all;
}

.summary-item {
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.progress-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 12px;
  color: var(--el-text-color-secondary);
}

@media (max-width: 1280px) {
  .metrics-grid,
  .summary-grid,
  .compact-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .metrics-grid,
  .summary-grid,
  .compact-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
