<script lang="ts" setup>
import type { AppLatestMetricValue, AppViewModel } from '../../types';

import { computed } from 'vue';

import { formatHeartbeat, formatSeconds } from '../../appPanelUtils';
import HotelCleaningThroughputChart from './HotelCleaningThroughputChart.vue';

defineOptions({ name: 'HotelMatchingPipelineDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
  refreshKey?: number;
}>();

interface ParsedMetric {
  labels: Record<string, string>;
  metricName: string;
  value: null | number;
}

interface SourceProgress {
  cleaned: null | number;
  detailDurationMs: null | number;
  failed: null | number;
  pending: null | number;
  percent: number;
  remaining: null | number;
  snapshotAlreadyCleaned: null | number;
  snapshotDurationMs: null | number;
  snapshotPending: null | number;
  snapshotSkippedFailed: null | number;
  snapshotSkippedTotal: null | number;
  snapshotState: null | number;
  source: 'OTA' | 'SUP';
  total: null | number;
}

interface StageRow {
  completed: null | number;
  failed: null | number;
  name: string;
  pending: null | number;
  stage: 'cleaner' | 'reader' | 'writer';
  succeeded: null | number;
}

function parseLabels(labelsText: string) {
  const labels: Record<string, string> = {};
  for (const part of labelsText.split(', ')) {
    const separator = part.indexOf('=');
    if (separator <= 0) {
      continue;
    }
    labels[part.slice(0, separator).trim()] = part.slice(separator + 1).trim();
  }
  return labels;
}

function parseMetric(item: AppLatestMetricValue): ParsedMetric {
  const value = Number(item.valueText);
  return {
    labels: parseLabels(item.labelsText),
    metricName: item.metricName,
    value: Number.isFinite(value) ? value : null,
  };
}

function formatInteger(value: null | number) {
  if (value === null || Number.isNaN(value)) {
    return '--';
  }
  return new Intl.NumberFormat('zh-CN', {
    maximumFractionDigits: 0,
  }).format(value);
}

function formatPercent(value: number) {
  return `${value.toFixed(1)}%`;
}

function formatDuration(value: null | number) {
  if (value === null || Number.isNaN(value) || value <= 0) {
    return '--';
  }
  if (value < 1000) {
    return `${formatInteger(value)} ms`;
  }
  return `${(value / 1000).toFixed(value < 10_000 ? 1 : 0)} s`;
}

function snapshotState(value: null | number) {
  switch (value) {
    case 0: {
      return { label: '未开始', type: 'info' as const };
    }
    case 1: {
      return { label: '读取中', type: 'primary' as const };
    }
    case 2: {
      return { label: '已完成', type: 'success' as const };
    }
    case 9: {
      return { label: '失败', type: 'danger' as const };
    }
    default: {
      return { label: '无指标', type: 'info' as const };
    }
  }
}

const metricRows = computed(() =>
  (props.app?.latestMetricValues || [])
    .map((item) => parseMetric(item))
    .filter((item) => item.metricName.startsWith('hotel_cleaning_')),
);

const hasMetrics = computed(() => metricRows.value.length > 0);

function getMetric(metricName: string, labels: Record<string, string> = {}) {
  const row = metricRows.value.find(
    (item) =>
      item.metricName === metricName &&
      Object.entries(labels).every(
        ([key, value]) => item.labels[key] === value,
      ),
  );
  return row?.value ?? null;
}

function sumMetric(metricName: string, labels: Record<string, string> = {}) {
  const rows = metricRows.value.filter(
    (item) =>
      item.metricName === metricName &&
      Object.entries(labels).every(
        ([key, value]) => item.labels[key] === value,
      ),
  );
  if (rows.length === 0) {
    return null;
  }
  return rows.reduce((total, row) => total + (row.value || 0), 0);
}

const pipelineStateValue = computed(() =>
  getMetric('hotel_cleaning_pipeline_state'),
);

const pipelineState = computed(() => {
  switch (pipelineStateValue.value) {
    case 1: {
      return { label: '运行中', type: 'primary' as const };
    }
    case 4: {
      return { label: '已完成', type: 'success' as const };
    }
    case 9: {
      return { label: '异常', type: 'danger' as const };
    }
    default: {
      return { label: '等待指标', type: 'info' as const };
    }
  }
});

const sourceRows = computed<SourceProgress[]>(() =>
  (['OTA', 'SUP'] as const).map((source) => {
    const total = getMetric('hotel_cleaning_source_total', { source });
    const cleaned = getMetric('hotel_cleaning_source_cleaned', { source });
    const remaining = getMetric('hotel_cleaning_source_remaining', { source });
    const failed = getMetric('hotel_cleaning_source_failed', { source });
    const pending = getMetric('hotel_cleaning_source_pending', { source });
    return {
      cleaned,
      detailDurationMs: getMetric(
        'hotel_cleaning_detail_last_query_duration_ms',
        { source },
      ),
      failed,
      pending,
      percent:
        total && cleaned !== null
          ? Math.min(100, Math.max(0, (cleaned / total) * 100))
          : 0,
      remaining,
      snapshotAlreadyCleaned: getMetric(
        'hotel_cleaning_id_snapshot_already_cleaned',
        { source },
      ),
      snapshotDurationMs: getMetric('hotel_cleaning_id_snapshot_duration_ms', {
        source,
      }),
      snapshotPending: getMetric('hotel_cleaning_id_snapshot_pending', {
        source,
      }),
      snapshotSkippedFailed: getMetric(
        'hotel_cleaning_id_snapshot_skipped_failed',
        { source },
      ),
      snapshotSkippedTotal: getMetric(
        'hotel_cleaning_id_snapshot_skipped_total',
        { source },
      ),
      snapshotState: getMetric('hotel_cleaning_id_snapshot_state', { source }),
      source,
      total,
    };
  }),
);

function queueModel(kind: 'clean' | 'id' | 'raw', label: string) {
  const metricPrefix = `hotel_cleaning_${kind}_queue`;
  const current = getMetric(
    kind === 'id' ? `${metricPrefix}_ids_current` : `${metricPrefix}_current`,
  );
  const capacity = getMetric(`${metricPrefix}_capacity`);
  const percent =
    current !== null && capacity
      ? Math.min(100, Math.max(0, (current / capacity) * 100))
      : 0;
  let status: 'exception' | 'success' | 'warning' = 'success';
  if (percent >= 95) {
    status = 'exception';
  } else if (percent >= 80) {
    status = 'warning';
  }
  return {
    capacity,
    capacityLabel: capacity && capacity > 0 ? formatInteger(capacity) : '不限',
    current,
    label,
    percent,
    status,
  } as const;
}

const queueRows = computed(() => [
  queueModel('id', '待查询酒店'),
  queueModel('raw', '待清洗原始酒店'),
  queueModel('clean', '待写入'),
]);

const idQueueBatches = computed(() =>
  getMetric('hotel_cleaning_id_queue_batches_current'),
);
const idQueueIds = computed(() =>
  getMetric('hotel_cleaning_id_queue_ids_current'),
);
const detailRequested = computed(
  () =>
    getMetric('hotel_cleaning_detail_requested') ??
    getMetric('hotel_cleaning_reader_node_completed'),
);
const detailEnqueued = computed(
  () =>
    getMetric('hotel_cleaning_detail_enqueued') ??
    getMetric('hotel_cleaning_reader_node_completed'),
);
const detailMissing = computed(() =>
  getMetric('hotel_cleaning_detail_missing'),
);
const refillWaitDuration = computed(() =>
  getMetric('hotel_cleaning_refill_last_wait_duration_ms'),
);

const refillState = computed(() => {
  switch (getMetric('hotel_cleaning_refill_state')) {
    case 0: {
      return { label: '可读取', type: 'success' as const };
    }
    case 1: {
      return { label: '等待 ID 队列降水位', type: 'warning' as const };
    }
    case 2: {
      return { label: 'ID 读取冷却中', type: 'warning' as const };
    }
    default: {
      return { label: '无指标', type: 'info' as const };
    }
  }
});

const stageRows = computed<StageRow[]>(() => {
  const cleanerCompleted = getMetric('hotel_cleaning_cleaner_node_completed');
  const writerCompleted = getMetric('hotel_cleaning_writer_node_completed');
  return [
    {
      completed: detailRequested.value,
      failed: detailMissing.value,
      name: '详情读取',
      pending: getMetric('hotel_cleaning_raw_queue_current'),
      stage: 'reader',
      succeeded: detailEnqueued.value,
    },
    {
      completed: cleanerCompleted,
      failed: getMetric('hotel_cleaning_cleaner_failed'),
      name: '清洗',
      pending: getMetric('hotel_cleaning_clean_queue_current'),
      stage: 'cleaner',
      succeeded: getMetric('hotel_cleaning_cleaner_succeeded'),
    },
    {
      completed: writerCompleted,
      failed: getMetric('hotel_cleaning_writer_failed'),
      name: '写入',
      pending: 0,
      stage: 'writer',
      succeeded: getMetric('hotel_cleaning_writer_succeeded'),
    },
  ];
});

const totalHotels = computed(() =>
  sourceRows.value.reduce((total, row) => total + (row.total || 0), 0),
);
const totalCleaned = computed(() =>
  sourceRows.value.reduce((total, row) => total + (row.cleaned || 0), 0),
);
const totalPending = computed(() =>
  sourceRows.value.reduce((total, row) => total + (row.pending || 0), 0),
);
const totalFailed = computed(() =>
  sourceRows.value.reduce((total, row) => total + (row.failed || 0), 0),
);
const totalSnapshotPending = computed(() =>
  sourceRows.value.reduce(
    (total, row) => total + (row.snapshotPending || 0),
    0,
  ),
);
const totalSnapshotSkipped = computed(() =>
  sourceRows.value.reduce(
    (total, row) => total + (row.snapshotSkippedTotal || 0),
    0,
  ),
);

const nodeNames = {
  cleaner: '清洗',
  reader: '详情读取',
  writer: '写入',
} as const;

const nodeCounterRows = computed(() =>
  (['reader', 'cleaner', 'writer'] as const).flatMap((node) =>
    (['OTA', 'SUP'] as const).map((source) => {
      const completed = sumMetric('hotel_cleaning_node_completed_total', {
        node,
        source,
      });
      return {
        completed: formatInteger(completed),
        failed: formatInteger(
          node === 'reader'
            ? sumMetric('hotel_cleaning_detail_missing_total', { source })
            : sumMetric('hotel_cleaning_node_failed_total', { node, source }),
        ),
        node: nodeNames[node],
        source,
        succeeded: formatInteger(
          node === 'reader'
            ? (sumMetric('hotel_cleaning_detail_enqueued_total', { source }) ??
                completed)
            : sumMetric('hotel_cleaning_node_succeeded_total', {
                node,
                source,
              }),
        ),
      };
    }),
  ),
);
</script>

<template>
  <div class="hotel-cleaning-dashboard">
    <el-alert
      v-if="!hasMetrics"
      :closable="false"
      class="mb-3"
      title="尚未收到酒店清洗指标，请确认 HotelMatchingPipeline 已启动并成功导出 OpenTelemetry 指标。"
      type="warning"
    />

    <div class="overview-grid">
      <div class="overview-item">
        <div class="metric-label">流水线状态</div>
        <div class="state-row">
          <span class="metric-value">{{ pipelineState.label }}</span>
          <el-tag :type="pipelineState.type" effect="plain" size="small">
            {{ pipelineStateValue ?? '--' }}
          </el-tag>
        </div>
      </div>
      <div class="overview-item">
        <div class="metric-label">酒店总数</div>
        <div class="metric-value">{{ formatInteger(totalHotels) }}</div>
      </div>
      <div class="overview-item">
        <div class="metric-label">当前版本已清洗</div>
        <div class="metric-value metric-value--success">
          {{ formatInteger(totalCleaned) }}
        </div>
      </div>
      <div class="overview-item">
        <div class="metric-label">待处理 / 失败</div>
        <div class="metric-value">
          {{ formatInteger(totalPending) }}
          <span class="metric-separator">/</span>
          <span :class="{ 'metric-value--danger': totalFailed > 0 }">
            {{ formatInteger(totalFailed) }}
          </span>
        </div>
      </div>
      <div class="overview-item">
        <div class="metric-label">最近心跳</div>
        <div class="metric-value metric-value--compact">
          {{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}
        </div>
      </div>
      <div class="overview-item">
        <div class="metric-label">运行时长</div>
        <div class="metric-value metric-value--compact">
          {{ formatSeconds(props.app?.uptimeSeconds ?? null) }}
        </div>
      </div>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>
        <div class="section-heading">
          <span>清洗流水线</span>
          <el-tag :type="refillState.type" effect="plain" size="small">
            {{ refillState.label }}
          </el-tag>
        </div>
      </template>
      <div class="pipeline-flow">
        <section class="pipeline-step pipeline-step--snapshot">
          <div class="step-index">1</div>
          <div class="step-title">ID 快照</div>
          <div class="step-value">
            {{ formatInteger(totalSnapshotSkipped) }} /
            {{ formatInteger(totalSnapshotPending) }}
          </div>
          <div class="metric-label">启动已跳过 / 待重跑</div>
        </section>
        <section class="pipeline-step pipeline-step--queue">
          <div class="step-index">2</div>
          <div class="step-title">待查询队列</div>
          <div class="step-value">{{ formatInteger(idQueueIds) }} / 不限</div>
          <div class="metric-label">
            约 {{ formatInteger(idQueueBatches) }} 批待读取
          </div>
        </section>
        <section class="pipeline-step pipeline-step--detail">
          <div class="step-index">3</div>
          <div class="step-title">详情读取</div>
          <div class="step-value">
            {{ formatInteger(detailEnqueued) }} /
            {{ formatInteger(detailRequested) }}
          </div>
          <div class="metric-label">
            已入队 / 已请求，缺失 {{ formatInteger(detailMissing) }}
          </div>
        </section>
        <section class="pipeline-step pipeline-step--clean">
          <div class="step-index">4</div>
          <div class="step-title">清洗</div>
          <div class="step-value">
            {{ formatInteger(getMetric('hotel_cleaning_cleaner_succeeded')) }}
          </div>
          <div class="metric-label">
            原始队列
            {{ formatInteger(getMetric('hotel_cleaning_raw_queue_current')) }}
          </div>
        </section>
        <section class="pipeline-step pipeline-step--write">
          <div class="step-index">5</div>
          <div class="step-title">批量写入</div>
          <div class="step-value">
            {{ formatInteger(getMetric('hotel_cleaning_writer_succeeded')) }}
          </div>
          <div class="metric-label">
            <div>
              清洗队列
              {{
                formatInteger(getMetric('hotel_cleaning_clean_queue_current'))
              }}
            </div>
            <div>
              最近批次
              {{
                formatInteger(getMetric('hotel_cleaning_write_last_batch_size'))
              }}
              ·
              {{
                formatDuration(
                  getMetric('hotel_cleaning_write_last_batch_duration_ms'),
                )
              }}
            </div>
          </div>
        </section>
      </div>
      <div class="refill-strip">
        <span>ID 补货阈值</span>
        <strong>{{
          formatInteger(getMetric('hotel_cleaning_refill_threshold'))
        }}</strong>
        <span>ID 最近等待</span>
        <strong>{{ formatDuration(refillWaitDuration) }}</strong>
      </div>
    </el-card>

    <el-card class="mt-3" shadow="never">
      <template #header>OTA / SUP 清洗进度</template>
      <div class="source-grid">
        <section
          v-for="row in sourceRows"
          :key="row.source"
          class="source-progress"
        >
          <div class="section-heading">
            <div>
              <div class="source-title-row">
                <div class="source-name">{{ row.source }}</div>
                <el-tag
                  :type="snapshotState(row.snapshotState).type"
                  effect="plain"
                  size="small"
                >
                  ID 快照{{ snapshotState(row.snapshotState).label }}
                </el-tag>
              </div>
              <div class="metric-label">
                {{ formatInteger(row.cleaned) }} /
                {{ formatInteger(row.total) }}
              </div>
            </div>
            <strong>{{ formatPercent(row.percent) }}</strong>
          </div>
          <el-progress
            :percentage="row.percent"
            :show-text="false"
            :stroke-width="10"
          />
          <div class="source-stats">
            <div>
              <span>剩余</span>
              <strong>{{ formatInteger(row.remaining) }}</strong>
            </div>
            <div>
              <span>待处理</span>
              <strong>{{ formatInteger(row.pending) }}</strong>
            </div>
            <div>
              <span>失败</span>
              <strong :class="{ danger: (row.failed || 0) > 0 }">
                {{ formatInteger(row.failed) }}
              </strong>
            </div>
            <div>
              <span>启动已是当前版本</span>
              <strong>{{ formatInteger(row.snapshotAlreadyCleaned) }}</strong>
            </div>
            <div>
              <span>启动失败跳过</span>
              <strong>{{ formatInteger(row.snapshotSkippedFailed) }}</strong>
            </div>
            <div>
              <span>启动跳过合计</span>
              <strong>{{ formatInteger(row.snapshotSkippedTotal) }}</strong>
            </div>
            <div>
              <span>快照待处理 ID</span>
              <strong>{{ formatInteger(row.snapshotPending) }}</strong>
            </div>
            <div>
              <span>ID 快照耗时</span>
              <strong>{{ formatDuration(row.snapshotDurationMs) }}</strong>
            </div>
            <div>
              <span>最近详情查询</span>
              <strong>{{ formatDuration(row.detailDurationMs) }}</strong>
            </div>
          </div>
        </section>
      </div>
    </el-card>

    <HotelCleaningThroughputChart
      class="mt-3"
      :app="props.app"
      :refresh-key="props.refreshKey"
    />

    <div class="content-grid mt-3">
      <el-card shadow="never">
        <template #header>队列占用</template>
        <div class="queue-list">
          <div v-for="queue in queueRows" :key="queue.label" class="queue-row">
            <div class="section-heading">
              <span>{{ queue.label }}</span>
              <strong>
                {{ formatInteger(queue.current) }} /
                {{ queue.capacityLabel }}
              </strong>
            </div>
            <el-progress
              :percentage="queue.percent"
              :status="queue.status"
              :stroke-width="12"
            />
          </div>
        </div>
      </el-card>

      <el-card shadow="never">
        <template #header>本轮节点执行情况</template>
        <el-table :data="stageRows" size="small" stripe>
          <el-table-column label="节点" min-width="90" prop="name" />
          <el-table-column align="right" label="完成" min-width="110">
            <template #default="scope">
              {{ formatInteger(scope.row.completed) }}
            </template>
          </el-table-column>
          <el-table-column align="right" label="成功" min-width="110">
            <template #default="scope">
              {{ formatInteger(scope.row.succeeded) }}
            </template>
          </el-table-column>
          <el-table-column align="right" label="异常 / 缺失" min-width="120">
            <template #default="scope">
              <span :class="{ danger: (scope.row.failed || 0) > 0 }">
                {{ formatInteger(scope.row.failed) }}
              </span>
            </template>
          </el-table-column>
          <el-table-column align="right" label="节点间积压" min-width="120">
            <template #default="scope">
              {{ formatInteger(scope.row.pending) }}
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>进程累计计数（进程重启归零）</template>
      <el-table :data="nodeCounterRows" size="small" stripe>
        <el-table-column label="数据源" min-width="100" prop="source" />
        <el-table-column label="节点" min-width="100" prop="node" />
        <el-table-column
          align="right"
          label="完成"
          min-width="120"
          prop="completed"
        />
        <el-table-column
          align="right"
          label="成功"
          min-width="120"
          prop="succeeded"
        />
        <el-table-column
          align="right"
          label="失败"
          min-width="120"
          prop="failed"
        />
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.hotel-cleaning-dashboard {
  width: 100%;
}

.overview-grid {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 12px;
}

.overview-item,
.source-progress {
  padding: 14px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.metric-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-value {
  margin-top: 6px;
  overflow-wrap: anywhere;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: 0;
}

.metric-value--compact {
  font-size: 15px;
}

.metric-value--success,
.success {
  color: var(--el-color-success);
}

.metric-value--danger,
.danger {
  color: var(--el-color-danger);
}

.metric-separator {
  padding: 0 4px;
  color: var(--el-text-color-placeholder);
}

.state-row,
.section-heading {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.state-row .metric-value {
  min-width: 0;
}

.source-grid,
.content-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.pipeline-flow {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 10px;
}

.pipeline-step {
  position: relative;
  min-height: 126px;
  padding: 14px;
  overflow: hidden;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-top: 3px solid var(--el-color-success);
  border-radius: 6px;
}

.pipeline-step:not(:last-child)::after {
  position: absolute;
  top: 47px;
  right: -8px;
  z-index: 1;
  width: 16px;
  height: 24px;
  font-size: 22px;
  line-height: 21px;
  color: var(--el-text-color-placeholder);
  text-align: center;
  content: '›';
  background: var(--el-bg-color);
}

.pipeline-step--snapshot,
.pipeline-step--queue {
  border-top-color: var(--el-color-warning);
}

.pipeline-step--detail {
  border-top-color: var(--el-color-primary);
}

.step-index {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  margin-bottom: 9px;
  font-size: 12px;
  font-weight: 700;
  color: var(--el-text-color-secondary);
  border: 1px solid var(--el-border-color);
  border-radius: 50%;
}

.step-title {
  font-size: 13px;
  font-weight: 700;
}

.step-value {
  margin: 7px 0 3px;
  overflow-wrap: anywhere;
  font-size: 19px;
  font-weight: 700;
  letter-spacing: 0;
}

.refill-strip {
  display: flex;
  gap: 10px;
  align-items: center;
  justify-content: flex-end;
  padding-top: 12px;
  margin-top: 12px;
  font-size: 12px;
  border-top: 1px solid var(--el-border-color-lighter);
}

.refill-strip span {
  color: var(--el-text-color-secondary);
}

.refill-strip strong + span {
  margin-left: 12px;
}

.source-name {
  margin-bottom: 3px;
  font-size: 16px;
  font-weight: 700;
}

.source-title-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.source-progress :deep(.el-progress) {
  margin-top: 12px;
}

.source-stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-top: 14px;
}

.source-stats > div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.source-stats span {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.queue-list {
  display: flex;
  flex-direction: column;
  gap: 22px;
}

.queue-row :deep(.el-progress) {
  margin-top: 10px;
}

.mb-3 {
  margin-bottom: 12px;
}

.mt-3 {
  margin-top: 12px;
}

@media (max-width: 1440px) {
  .overview-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .pipeline-flow {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .pipeline-step::after {
    display: none;
  }
}

@media (max-width: 900px) {
  .content-grid,
  .source-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .overview-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .pipeline-flow {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .overview-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .pipeline-flow,
  .source-stats {
    grid-template-columns: minmax(0, 1fr);
  }

  .refill-strip {
    flex-wrap: wrap;
    justify-content: flex-start;
  }
}
</style>
