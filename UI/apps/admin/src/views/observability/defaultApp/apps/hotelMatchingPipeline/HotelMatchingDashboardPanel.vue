<script lang="ts" setup>
import type { AppLatestMetricValue, AppViewModel } from '../../types';

import { computed } from 'vue';

import { formatHeartbeat, formatSeconds } from '../../appPanelUtils';
import HotelMatchingThroughputChart from './HotelMatchingThroughputChart.vue';

defineOptions({ name: 'HotelMatchingDashboardPanel' });

const props = defineProps<{
  app: AppViewModel | null;
  refreshKey?: number;
}>();

interface ParsedMetric {
  metricName: string;
  value: null | number;
}

interface QueueRow {
  capacity: null | number;
  current: null | number;
  label: string;
  percent: number;
  status: 'exception' | 'success' | 'warning';
}

interface StageRow {
  active?: null | number;
  completed: null | number;
  duration: null | number;
  failed: null | number;
  name: string;
  pending: null | number;
  stage: 'id' | 'information' | 'matcher' | 'writer';
  succeeded: null | number;
}

function parseMetric(item: AppLatestMetricValue): ParsedMetric {
  const value = Number(item.valueText);
  return {
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

function formatQueueCapacity(value: null | number) {
  if (value !== null && value < 0) {
    return '无限';
  }
  return formatInteger(value);
}

function formatDuration(value: null | number) {
  if (value === null || Number.isNaN(value) || value < 0) {
    return '--';
  }
  if (value < 1000) {
    return `${formatInteger(value)} ms`;
  }
  return `${(value / 1000).toFixed(value < 10_000 ? 1 : 0)} s`;
}

const metricRows = computed(() =>
  (props.app?.latestMetricValues || [])
    .map((item) => parseMetric(item))
    .filter((item) => item.metricName.startsWith('hotel_matching_')),
);

const hasMetrics = computed(() => metricRows.value.length > 0);

function getMetric(metricName: string) {
  return (
    metricRows.value.find((item) => item.metricName === metricName)?.value ??
    null
  );
}

const pipelineStateValue = computed(() =>
  getMetric('hotel_matching_pipeline_state'),
);

const pipelineState = computed(() => {
  switch (pipelineStateValue.value) {
    case 1: {
      return { label: '运行中', type: 'primary' as const };
    }
    case 4: {
      return { label: '本轮完成', type: 'success' as const };
    }
    case 9: {
      return { label: '异常', type: 'danger' as const };
    }
    default: {
      return { label: '等待指标', type: 'info' as const };
    }
  }
});

function queueModel(
  metricKey: 'id' | 'result' | 'work',
  label: string,
): QueueRow {
  const current = getMetric(`hotel_matching_${metricKey}_queue_current`);
  const capacity = getMetric(`hotel_matching_${metricKey}_queue_capacity`);
  const percent =
    current !== null && capacity && capacity > 0
      ? Math.min(100, Math.max(0, (current / capacity) * 100))
      : 0;
  let status: QueueRow['status'] = 'success';
  if (percent >= 95) {
    status = 'exception';
  } else if (percent >= 80) {
    status = 'warning';
  }
  return {
    capacity,
    current,
    label,
    percent,
    status,
  };
}

const queueRows = computed<QueueRow[]>(() => [
  queueModel('id', '待加载酒店 ID'),
  queueModel('work', '待匹配事实模型'),
  queueModel('result', '待写入匹配结果'),
]);

const idsRecalled = computed(() => getMetric('hotel_matching_ids_recalled'));
const informationCompleted = computed(() =>
  getMetric('hotel_matching_information_completed'),
);
const informationSucceeded = computed(() =>
  getMetric('hotel_matching_information_succeeded'),
);
const informationFailed = computed(() => {
  const failed = getMetric('hotel_matching_information_failed');
  const missing = getMetric('hotel_matching_information_missing');
  return failed === null && missing === null
    ? null
    : (failed || 0) + (missing || 0);
});
const matcherCompleted = computed(() =>
  getMetric('hotel_matching_matcher_completed'),
);
const matcherFailed = computed(() =>
  getMetric('hotel_matching_matcher_failed'),
);
const writerCompleted = computed(() =>
  getMetric('hotel_matching_writer_completed'),
);
const writerSucceeded = computed(() =>
  getMetric('hotel_matching_writer_succeeded'),
);
const writerFailed = computed(() => getMetric('hotel_matching_writer_failed'));
const informationRetrying = computed(() =>
  getMetric('hotel_matching_information_retrying'),
);
const informationRetryCount = computed(() =>
  getMetric('hotel_matching_information_retry_count'),
);
const informationRetrySupHotelId = computed(() =>
  getMetric('hotel_matching_information_retry_sup_hotel_id'),
);
const informationLastFailedSupHotelId = computed(() =>
  getMetric('hotel_matching_information_last_failed_sup_hotel_id'),
);
const informationRetryDelayMs = computed(() =>
  getMetric('hotel_matching_information_last_retry_delay_ms'),
);
const informationActive = computed(() =>
  getMetric('hotel_matching_information_active'),
);
const informationSlowCount = computed(() =>
  getMetric('hotel_matching_information_slow_count'),
);
const informationSlowestSupHotelId = computed(() =>
  getMetric('hotel_matching_information_slowest_sup_hotel_id'),
);
const informationSlowestElapsedMs = computed(() =>
  getMetric('hotel_matching_information_slowest_elapsed_ms'),
);
const informationProblemSupHotelId = computed(() => {
  if ((informationLastFailedSupHotelId.value || 0) > 0) {
    return informationLastFailedSupHotelId.value;
  }
  if ((informationRetrySupHotelId.value || 0) > 0) {
    return informationRetrySupHotelId.value;
  }
  if ((informationSlowestSupHotelId.value || 0) > 0) {
    return informationSlowestSupHotelId.value;
  }
  return null;
});
const idQueueCurrent = computed(() =>
  getMetric('hotel_matching_id_queue_current'),
);
const idQueueCapacity = computed(() =>
  getMetric('hotel_matching_id_queue_capacity'),
);
const workQueueCurrent = computed(() =>
  getMetric('hotel_matching_work_queue_current'),
);
const workQueueCapacity = computed(() =>
  getMetric('hotel_matching_work_queue_capacity'),
);
const resultQueueCurrent = computed(() =>
  getMetric('hotel_matching_result_queue_current'),
);
const resultQueueCapacity = computed(() =>
  getMetric('hotel_matching_result_queue_capacity'),
);

function queuePressure(current: null | number, capacity: null | number) {
  return current !== null && capacity && capacity > 0
    ? Math.min(100, Math.max(0, (current / capacity) * 100))
    : 0;
}

const idQueuePressure = computed(() =>
  queuePressure(idQueueCurrent.value, idQueueCapacity.value),
);
const workQueuePressure = computed(() =>
  queuePressure(workQueueCurrent.value, workQueueCapacity.value),
);
const resultQueuePressure = computed(() =>
  queuePressure(resultQueueCurrent.value, resultQueueCapacity.value),
);

const diagnosis = computed(() => {
  if (!hasMetrics.value) {
    return {
      detail: '尚未收到酒店匹配指标。',
      label: '等待指标',
      type: 'info' as const,
    };
  }
  if ((informationRetrying.value || 0) > 0) {
    return {
      detail: `信息加载 worker 正在无限重试，最近 SupHotelId=${formatInteger(informationRetrySupHotelId.value)}。`,
      label: '信息加载重试中',
      type: 'danger' as const,
    };
  }
  if ((informationSlowCount.value || 0) > 0) {
    return {
      detail: `信息加载有 ${formatInteger(informationSlowCount.value)} 个 worker 长时间未返回，最慢 SupHotelId=${formatInteger(informationSlowestSupHotelId.value)}，已执行 ${formatDuration(informationSlowestElapsedMs.value)}。请看控制台 Stage 慢日志定位 SQL。`,
      label: '信息加载卡住',
      type: 'danger' as const,
    };
  }
  if ((informationActive.value || 0) > 0 && idQueuePressure.value >= 95) {
    return {
      detail: `信息加载仍有 ${formatInteger(informationActive.value)} 个 worker 执行中，ID 队列已满，后续流水线正在被信息加载回压。`,
      label: '信息加载回压',
      type: 'warning' as const,
    };
  }
  if (resultQueuePressure.value >= 95) {
    return {
      detail: '结果队列接近满，优先看 SaveMatchEvaluationsAsync 写库等待或锁等待。',
      label: '写入可能背压',
      type: 'warning' as const,
    };
  }
  if (workQueuePressure.value >= 95) {
    return {
      detail: '匹配事实队列接近满，优先看 matcher 是否处理慢，或后面的 result 队列是否阻塞。',
      label: '匹配阶段积压',
      type: 'warning' as const,
    };
  }
  if (idQueuePressure.value >= 95) {
    return {
      detail: 'ID 队列接近满，通常是后续加载、匹配或写入阶段变慢导致回压。',
      label: 'ID 队列积压',
      type: 'warning' as const,
    };
  }
  if (pipelineStateValue.value === 1) {
    return {
      detail: '没有明显重试或队列打满，当前看起来是在正常推进。',
      label: '正常运行',
      type: 'success' as const,
    };
  }
  if (pipelineStateValue.value === 9) {
    return {
      detail: '本轮匹配已异常结束，请看控制台错误日志。',
      label: '本轮异常',
      type: 'danger' as const,
    };
  }
  return {
    detail: '当前没有明显卡点。',
    label: '暂无卡点',
    type: 'info' as const,
  };
});

const diagnosisAlertType = computed(() => {
  if (diagnosis.value.type === 'danger') {
    return 'error' as const;
  }
  if (diagnosis.value.type === 'warning') {
    return 'warning' as const;
  }
  if (diagnosis.value.type === 'success') {
    return 'success' as const;
  }
  return 'info' as const;
});

const stageRows = computed<StageRow[]>(() => [
  {
    completed: idsRecalled.value,
    duration: null,
    failed: getMetric('hotel_matching_id_failed'),
    name: 'ID 召回',
    pending: idQueueCurrent.value,
    stage: 'id',
    succeeded: idsRecalled.value,
  },
  {
    completed: informationCompleted.value,
    duration: getMetric('hotel_matching_information_last_duration_ms'),
    failed: informationFailed.value,
    name: '信息加载',
    pending: workQueueCurrent.value,
    stage: 'information',
    succeeded: informationSucceeded.value,
    active: informationActive.value,
  },
  {
    completed: matcherCompleted.value,
    duration: getMetric('hotel_matching_matcher_last_duration_ms'),
    failed: matcherFailed.value,
    name: '事实匹配',
    pending: resultQueueCurrent.value,
    stage: 'matcher',
    succeeded:
      matcherCompleted.value === null
        ? null
        : matcherCompleted.value - (matcherFailed.value || 0),
  },
  {
    completed: writerCompleted.value,
    duration: getMetric('hotel_matching_write_last_batch_duration_ms'),
    failed: writerFailed.value,
    name: '批量写入',
    pending: 0,
    stage: 'writer',
    succeeded: writerSucceeded.value,
  },
]);

const matchedHotels = computed(() =>
  getMetric('hotel_matching_matched_hotels'),
);
const singleCandidates = computed(() =>
  getMetric('hotel_matching_single_candidate_hotels'),
);
const ambiguousHotels = computed(() =>
  getMetric('hotel_matching_ambiguous_hotels'),
);
const noMatchHotels = computed(() =>
  getMetric('hotel_matching_no_match_hotels'),
);
const candidateCount = computed(() =>
  getMetric('hotel_matching_candidate_count'),
);

async function copySupHotelId(value: null | number) {
  if (!value) {
    return;
  }
  await navigator.clipboard?.writeText(String(Math.trunc(value)));
}
</script>

<template>
  <div class="hotel-matching-dashboard">
    <el-alert
      v-if="!hasMetrics"
      :closable="false"
      class="mb-3"
      title="尚未收到酒店匹配指标"
      type="warning"
    />

    <el-alert
      v-else
      :closable="false"
      :title="diagnosis.label"
      :type="diagnosisAlertType"
      class="mb-3"
      show-icon
    >
      {{ diagnosis.detail }}
    </el-alert>

    <div class="overview-grid">
      <section class="overview-item">
        <div class="metric-label">流水线状态</div>
        <div class="state-row">
          <strong class="metric-value">{{ pipelineState.label }}</strong>
          <el-tag :type="pipelineState.type" effect="plain" size="small">
            {{ pipelineStateValue ?? '--' }}
          </el-tag>
        </div>
      </section>
      <section class="overview-item">
        <div class="metric-label">信息加载重试</div>
        <strong
          class="metric-value"
          :class="{ danger: (informationRetrying || 0) > 0 }"
        >
          {{ formatInteger(informationRetrying) }}
        </strong>
      </section>
      <section class="overview-item">
        <div class="metric-label">异常 SupHotelId</div>
        <div class="copy-row">
          <strong
            class="metric-value"
            :class="{ danger: (informationProblemSupHotelId || 0) > 0 }"
          >
            {{ formatInteger(informationProblemSupHotelId) }}
          </strong>
          <el-button
            v-if="informationProblemSupHotelId"
            link
            size="small"
            type="primary"
            @click="copySupHotelId(informationProblemSupHotelId)"
          >
            复制
          </el-button>
        </div>
      </section>
      <section class="overview-item">
        <div class="metric-label">当前轮次</div>
        <strong class="metric-value">
          {{ formatInteger(getMetric('hotel_matching_round_number')) }}
        </strong>
      </section>
      <section class="overview-item">
        <div class="metric-label">本轮召回酒店</div>
        <strong class="metric-value">{{ formatInteger(idsRecalled) }}</strong>
      </section>
      <section class="overview-item">
        <div class="metric-label">匹配酒店 / 候选关系</div>
        <strong class="metric-value">
          {{ formatInteger(matchedHotels) }}
          <span class="metric-separator">/</span>
          {{ formatInteger(candidateCount) }}
        </strong>
      </section>
      <section class="overview-item">
        <div class="metric-label">多候选 / 无候选</div>
        <strong class="metric-value">
          <span :class="{ danger: (ambiguousHotels || 0) > 0 }">
            {{ formatInteger(ambiguousHotels) }}
          </span>
          <span class="metric-separator">/</span>
          {{ formatInteger(noMatchHotels) }}
        </strong>
      </section>
      <section class="overview-item">
        <div class="metric-label">最近心跳</div>
        <strong class="metric-value metric-value--compact">
          {{ formatHeartbeat(props.app?.heartbeatTimestamp ?? null) }}
        </strong>
      </section>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>
        <div class="section-heading">
          <span>匹配流水线</span>
          <span class="header-meta">
            本轮
            {{
              formatDuration(getMetric('hotel_matching_round_last_duration_ms'))
            }}
            · 运行 {{ formatSeconds(props.app?.uptimeSeconds ?? null) }}
          </span>
        </div>
      </template>
      <div class="pipeline-flow">
        <section
          v-for="(stage, index) in stageRows"
          :key="stage.name"
          class="pipeline-step"
          :class="{
            'pipeline-step--blocked':
              stage.stage === 'information' &&
              ((informationRetrying || 0) > 0 ||
                (informationSlowCount || 0) > 0),
          }"
        >
          <div class="step-index">{{ index + 1 }}</div>
          <div class="step-title">{{ stage.name }}</div>
          <div class="step-value">{{ formatInteger(stage.succeeded) }}</div>
          <div class="step-meta">
            完成 {{ formatInteger(stage.completed) }} · 失败/缺失
            <span :class="{ danger: (stage.failed || 0) > 0 }">
              {{ formatInteger(stage.failed) }}
            </span>
          </div>
          <div v-if="stage.stage === 'information'" class="step-meta">
            执行中
            <span :class="{ warning: (informationActive || 0) > 0 }">
              {{ formatInteger(stage.active ?? null) }}
            </span>
            · 慢加载
            <span :class="{ danger: (informationSlowCount || 0) > 0 }">
              {{ formatInteger(informationSlowCount) }}
            </span>
          </div>
          <div
            v-if="
              stage.stage === 'information' && informationProblemSupHotelId
            "
            class="step-meta"
          >
            异常 SupHotelId
            <button
              class="copy-id-button"
              type="button"
              @click="copySupHotelId(informationProblemSupHotelId)"
            >
              {{ formatInteger(informationProblemSupHotelId) }}
            </button>
          </div>
          <div class="step-meta">
            积压 {{ formatInteger(stage.pending) }}
            <template v-if="stage.duration !== null">
              · {{ formatDuration(stage.duration) }}
            </template>
          </div>
        </section>
      </div>
      <div class="retry-strip">
        <div>
          <span>正在加载 worker</span>
          <strong :class="{ warning: (informationActive || 0) > 0 }">
            {{ formatInteger(informationActive) }}
          </strong>
        </div>
        <div>
          <span>慢加载 worker</span>
          <strong :class="{ danger: (informationSlowCount || 0) > 0 }">
            {{ formatInteger(informationSlowCount) }}
          </strong>
        </div>
        <div>
          <span>最慢 SupHotelId</span>
          <strong>{{ formatInteger(informationSlowestSupHotelId) }}</strong>
        </div>
        <div>
          <span>最慢耗时</span>
          <strong>{{ formatDuration(informationSlowestElapsedMs) }}</strong>
        </div>
        <div>
          <span>正在重试 worker</span>
          <strong :class="{ danger: (informationRetrying || 0) > 0 }">
            {{ formatInteger(informationRetrying) }}
          </strong>
        </div>
        <div>
          <span>累计重试次数</span>
          <strong>{{ formatInteger(informationRetryCount) }}</strong>
        </div>
        <div>
          <span>异常 SupHotelId</span>
          <strong :class="{ danger: (informationProblemSupHotelId || 0) > 0 }">
            {{ formatInteger(informationProblemSupHotelId) }}
          </strong>
        </div>
        <div>
          <span>重试间隔</span>
          <strong>{{ formatDuration(informationRetryDelayMs) }}</strong>
        </div>
      </div>
    </el-card>

    <div class="content-grid mt-3">
      <el-card shadow="never">
        <template #header>队列占用</template>
        <div class="queue-list">
          <div v-for="queue in queueRows" :key="queue.label" class="queue-row">
            <div class="section-heading">
              <span>{{ queue.label }}</span>
              <strong>
                {{ formatInteger(queue.current) }} /
                {{ formatQueueCapacity(queue.capacity) }}
              </strong>
            </div>
            <el-progress
              :percentage="queue.percent"
              :show-text="false"
              :status="queue.status"
              :stroke-width="9"
            />
          </div>
        </div>
      </el-card>

      <el-card shadow="never">
        <template #header>匹配结果分布</template>
        <div class="result-grid">
          <div>
            <span>唯一候选</span>
            <strong class="success">{{
              formatInteger(singleCandidates)
            }}</strong>
          </div>
          <div>
            <span>多个候选</span>
            <strong :class="{ danger: (ambiguousHotels || 0) > 0 }">
              {{ formatInteger(ambiguousHotels) }}
            </strong>
          </div>
          <div>
            <span>无候选</span>
            <strong>{{ formatInteger(noMatchHotels) }}</strong>
          </div>
          <div>
            <span>候选关系</span>
            <strong>{{ formatInteger(candidateCount) }}</strong>
          </div>
        </div>
      </el-card>
    </div>

    <el-card class="mt-3" shadow="never">
      <template #header>运行参数与最近写入</template>
      <div class="parameter-grid">
        <div>
          <span>召回半径</span>
          <strong>
            {{
              formatInteger(getMetric('hotel_matching_recall_radius_meters'))
            }}
            m
          </strong>
        </div>
        <div>
          <span>匹配距离</span>
          <strong>
            {{
              formatInteger(getMetric('hotel_matching_match_distance_meters'))
            }}
            m
          </strong>
        </div>
        <div>
          <span>最大候选数</span>
          <strong>
            {{ formatInteger(getMetric('hotel_matching_max_candidates')) }}
          </strong>
        </div>
        <div>
          <span>写入批量</span>
          <strong>
            {{ formatInteger(getMetric('hotel_matching_write_batch_size')) }}
          </strong>
        </div>
        <div>
          <span>最近写入条数</span>
          <strong>
            {{
              formatInteger(getMetric('hotel_matching_write_last_batch_size'))
            }}
          </strong>
        </div>
        <div>
          <span>最近写入耗时</span>
          <strong>
            {{
              formatDuration(
                getMetric('hotel_matching_write_last_batch_duration_ms'),
              )
            }}
          </strong>
        </div>
      </div>
    </el-card>

    <HotelMatchingThroughputChart
      class="mt-3"
      :app="props.app"
      :refresh-key="props.refreshKey"
    />

    <el-card class="mt-3" shadow="never">
      <template #header>本轮节点执行情况</template>
      <el-table :data="stageRows" size="small">
        <el-table-column label="节点" min-width="120" prop="name" />
        <el-table-column label="完成" min-width="110">
          <template #default="{ row }">
            {{ formatInteger(row.completed) }}
          </template>
        </el-table-column>
        <el-table-column label="成功" min-width="110">
          <template #default="{ row }">
            {{ formatInteger(row.succeeded) }}
          </template>
        </el-table-column>
        <el-table-column label="异常 / 缺失" min-width="120">
          <template #default="{ row }">
            <span :class="{ danger: (row.failed || 0) > 0 }">
              {{ formatInteger(row.failed) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="节点间积压" min-width="120">
          <template #default="{ row }">
            {{ formatInteger(row.pending) }}
          </template>
        </el-table-column>
        <el-table-column label="最近耗时" min-width="120">
          <template #default="{ row }">
            {{ formatDuration(row.duration) }}
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.hotel-matching-dashboard {
  min-width: 0;
}

.overview-grid {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  border: 1px solid var(--el-border-color-lighter);
}

.overview-item {
  min-width: 0;
  min-height: 88px;
  padding: 14px 16px;
  background: var(--el-bg-color);
  border-right: 1px solid var(--el-border-color-lighter);
}

.overview-item:last-child {
  border-right: 0;
}

.metric-label,
.step-meta,
.header-meta,
.parameter-grid span,
.result-grid span {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.metric-value {
  display: block;
  margin-top: 10px;
  overflow-wrap: anywhere;
  font-size: 22px;
  line-height: 1.2;
  color: var(--el-text-color-primary);
}

.metric-value--compact {
  font-size: 15px;
}

.metric-separator {
  margin: 0 5px;
  color: var(--el-border-color);
}

.state-row,
.copy-row,
.section-heading {
  display: flex;
  gap: 10px;
  align-items: center;
  justify-content: space-between;
}

.copy-row .metric-value {
  margin-top: 10px;
}

.pipeline-flow {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  border: 1px solid var(--el-border-color-lighter);
}

.pipeline-step {
  position: relative;
  min-width: 0;
  min-height: 132px;
  padding: 16px;
  border-right: 1px solid var(--el-border-color-lighter);
}

.pipeline-step:last-child {
  border-right: 0;
}

.pipeline-step--blocked {
  background: var(--el-color-danger-light-9);
  box-shadow: inset 0 3px 0 var(--el-color-danger);
}

.pipeline-step--blocked .step-index {
  color: var(--el-color-danger);
  background: var(--el-color-danger-light-8);
}

.step-index {
  display: grid;
  width: 24px;
  height: 24px;
  font-size: 12px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-radius: 4px;
  place-items: center;
}

.step-title {
  margin-top: 10px;
  font-size: 14px;
  font-weight: 700;
}

.step-value {
  margin: 8px 0 4px;
  font-size: 24px;
  font-weight: 700;
}

.step-meta {
  margin-top: 3px;
}

.copy-id-button {
  padding: 0;
  font: inherit;
  font-weight: 700;
  color: var(--el-color-primary);
  cursor: pointer;
  background: transparent;
  border: 0;
}

.copy-id-button:hover {
  color: var(--el-color-primary-light-3);
}

.retry-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1px;
  margin-top: 12px;
  overflow: hidden;
  background: var(--el-border-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
}

.retry-strip > div {
  min-width: 0;
  padding: 12px 14px;
  background: var(--el-bg-color);
}

.retry-strip span {
  display: block;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.retry-strip strong {
  display: block;
  margin-top: 6px;
  overflow-wrap: anywhere;
  font-size: 18px;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 0.8fr);
  gap: 12px;
}

.queue-list {
  display: grid;
  gap: 18px;
}

.queue-row strong {
  font-size: 13px;
}

.queue-row :deep(.el-progress) {
  margin-top: 8px;
}

.result-grid,
.parameter-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1px;
  overflow: hidden;
  background: var(--el-border-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
}

.result-grid > div,
.parameter-grid > div {
  min-width: 0;
  padding: 14px;
  background: var(--el-bg-color);
}

.result-grid strong,
.parameter-grid strong {
  display: block;
  margin-top: 7px;
  overflow-wrap: anywhere;
  font-size: 18px;
}

.parameter-grid {
  grid-template-columns: repeat(6, minmax(0, 1fr));
}

.danger {
  color: var(--el-color-danger);
}

.warning {
  color: var(--el-color-warning);
}

.success {
  color: var(--el-color-success);
}

.mt-3 {
  margin-top: 12px;
}

.mb-3 {
  margin-bottom: 12px;
}

@media (max-width: 1280px) {
  .overview-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .overview-item:nth-child(3) {
    border-right: 0;
  }

  .parameter-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 900px) {
  .pipeline-flow,
  .retry-strip,
  .content-grid {
    grid-template-columns: 1fr;
  }

  .pipeline-step {
    min-height: 112px;
    border-right: 0;
    border-bottom: 1px solid var(--el-border-color-lighter);
  }

  .pipeline-step:last-child {
    border-bottom: 0;
  }
}

@media (max-width: 640px) {
  .overview-grid,
  .parameter-grid,
  .result-grid {
    grid-template-columns: 1fr;
  }

  .overview-item {
    border-right: 0;
    border-bottom: 1px solid var(--el-border-color-lighter);
  }

  .overview-item:last-child {
    border-bottom: 0;
  }
}
</style>
