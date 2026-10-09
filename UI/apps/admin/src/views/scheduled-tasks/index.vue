<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { useAccess } from '@vben/access';

import {
  ElButton,
  ElCard,
  ElDrawer,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
  ElMessage,
  ElMessageBox,
  ElPagination,
  ElRadioButton,
  ElRadioGroup,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  type ScheduledTaskAudit,
  type ScheduledTaskDefinition,
  type ScheduledTaskInput,
  type ScheduledTaskLogEntry,
  type ScheduledTaskRun,
  cancelTaskRunApi,
  createScheduledTaskApi,
  deleteScheduledTaskApi,
  getTaskRunLogsApi,
  listScheduledTaskAuditsApi,
  listScheduledTasksApi,
  retryTaskRunApi,
  runScheduledTaskNowApi,
  setScheduledTaskEnabledApi,
} from '#/api/scheduled-tasks';
import { useVbenForm } from '#/adapter/form';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:scheduling:create']);
const canUpdate = hasAccessByCodes(['auth:admin:scheduling:update']);
const canDelete = hasAccessByCodes(['auth:admin:scheduling:delete']);
const canTrigger = hasAccessByCodes(['auth:admin:scheduling:trigger']);

const loading = ref(false);
const tasks = ref<ScheduledTaskDefinition[]>([]);

const taskTypeOptions = [
  { label: '进程', value: 'Process' },
  { label: 'SQL', value: 'Sql' },
  { label: '程序', value: 'Program' },
];

const misfireOptions = [
  { label: '触发一次', value: 'FireOnce' },
  { label: '忽略', value: 'Ignore' },
  { label: '全部触发', value: 'FireAll' },
];

const [Form, formApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  schema: [
    {
      component: 'Input',
      fieldName: 'name',
      label: '任务名称',
      rules: 'required',
    },
    {
      component: 'Input',
      fieldName: 'description',
      label: '描述',
    },
    {
      component: 'Select',
      componentProps: {
        options: taskTypeOptions,
      },
      fieldName: 'taskType',
      label: '任务类型',
      rules: 'required',
      defaultValue: 'Process',
    },
    {
      component: 'Input',
      fieldName: 'cronExpression',
      label: 'Cron 表达式',
      rules: 'required',
      componentProps: {
        placeholder: '如 0 */5 * * * *',
      },
    },
    {
      component: 'Input',
      fieldName: 'timeZoneId',
      label: '时区',
      rules: 'required',
      defaultValue: 'Asia/Shanghai',
    },
    {
      component: 'Select',
      componentProps: {
        options: misfireOptions,
      },
      fieldName: 'misfirePolicy',
      label: '错过触发策略',
      rules: 'required',
      defaultValue: 'FireOnce',
    },
    {
      component: 'InputNumber',
      fieldName: 'timeoutSeconds',
      label: '超时时间 (秒)',
      rules: 'required',
      defaultValue: 300,
      componentProps: {
        min: 1,
      },
    },
    {
      component: 'InputNumber',
      fieldName: 'retryCount',
      label: '重试次数',
      defaultValue: 0,
      componentProps: {
        min: 0,
      },
    },
    {
      component: 'InputNumber',
      fieldName: 'retryIntervalSeconds',
      label: '重试间隔 (秒)',
      defaultValue: 30,
      componentProps: {
        min: 1,
      },
    },
    {
      component: 'Switch',
      fieldName: 'retryOnTimeout',
      label: '超时时重试',
      defaultValue: false,
    },
    {
      component: 'Switch',
      fieldName: 'isEnabled',
      label: '启用',
      defaultValue: true,
    },
    {
      component: 'Input',
      fieldName: 'configuration',
      label: '配置 (JSON)',
      componentProps: {
        type: 'textarea',
        rows: 5,
        placeholder: '{"handlerKey":"..."}',
      },
      defaultValue: '{}',
    },
  ],
  showDefaultActions: false,
});

const [Drawer, drawerApi] = useVbenDrawer({
  onOpenChange(isOpen) {
    if (!isOpen) {
      editingTask.value = null;
    }
  },
});

const editingTask = ref<ScheduledTaskDefinition | null>(null);
const isCreate = ref(true);

async function loadTasks() {
  loading.value = true;
  try {
    tasks.value = await listScheduledTasksApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  isCreate.value = true;
  drawerApi.open();
  formApi.resetForm();
  formApi.setValues({
    cronExpression: '0 0 * * *',
    isEnabled: true,
    misfirePolicy: 'FireOnce',
    retryCount: 0,
    retryIntervalSeconds: 30,
    retryOnTimeout: false,
    taskType: 'Process',
    timeZoneId: 'Asia/Shanghai',
    timeoutSeconds: 300,
    configuration: '{}',
  });
}

function openEdit(task: ScheduledTaskDefinition) {
  isCreate.value = false;
  editingTask.value = task;
  drawerApi.open();
  formApi.setValues({
    configuration: JSON.stringify(task.configuration, null, 2),
    cronExpression: task.cronExpression,
    description: task.description || '',
    isEnabled: task.isEnabled,
    misfirePolicy: task.misfirePolicy,
    name: task.name,
    retryCount: task.retryCount,
    retryIntervalSeconds: task.retryIntervalSeconds,
    retryOnTimeout: task.retryOnTimeout,
    taskType: task.taskType,
    timeZoneId: task.timeZoneId,
    timeoutSeconds: task.timeoutSeconds,
  });
}

async function handleSubmit() {
  const values = await formApi.getValues();
  let config: any;
  try {
    config = JSON.parse(values.configuration || '{}');
  } catch {
    ElMessage.error('配置 JSON 格式无效');
    return;
  }

  const input: ScheduledTaskInput = {
    configuration: config,
    cronExpression: values.cronExpression,
    description: values.description || null,
    isEnabled: values.isEnabled,
    misfirePolicy: values.misfirePolicy,
    name: values.name,
    retryCount: values.retryCount,
    retryIntervalSeconds: values.retryIntervalSeconds,
    retryOnTimeout: values.retryOnTimeout,
    taskType: values.taskType,
    timeZoneId: values.timeZoneId,
    timeoutSeconds: values.timeoutSeconds,
  };

  if (isCreate.value) {
    await createScheduledTaskApi(input);
    ElMessage.success('任务创建成功');
  } else {
    const { updateScheduledTaskApi } = await import('#/api/scheduled-tasks');
    await updateScheduledTaskApi(
      editingTask.value!.id,
      input,
      editingTask.value!.rowVersion,
    );
    ElMessage.success('任务更新成功');
  }
  drawerApi.close();
  await loadTasks();
}

async function handleToggleEnabled(task: ScheduledTaskDefinition) {
  await setScheduledTaskEnabledApi(
    task.id,
    !task.isEnabled,
    task.rowVersion,
  );
  ElMessage.success(task.isEnabled ? '任务已禁用' : '任务已启用');
  await loadTasks();
}

async function handleRunNow(task: ScheduledTaskDefinition) {
  await ElMessageBox.confirm(
    `确定要立即运行 "${task.name}" 吗？`,
    '确认',
    { type: 'info' },
  );
  await runScheduledTaskNowApi(task.id);
  ElMessage.success('任务已加入执行队列');
}

async function handleDelete(task: ScheduledTaskDefinition) {
  await ElMessageBox.confirm(
    `确定要删除任务 "${task.name}" 吗？`,
    '确认',
    { type: 'warning' },
  );
  await deleteScheduledTaskApi(task.id, task.rowVersion);
  ElMessage.success('任务删除成功');
  await loadTasks();
}

function openRuns(task: ScheduledTaskDefinition) {
  runsDrawerApi.open();
  runsCurrentPage.value = 1;
  currentTaskId.value = task.id;
  loadRuns(task.id);
  currentTaskName.value = task.name;
}

const currentTaskName = ref('');
const currentTaskId = ref('');
const taskRuns = ref<ScheduledTaskRun[]>([]);
const allTaskRuns = ref<ScheduledTaskRun[]>([]);
const runsLoading = ref(false);
const runsTotal = ref(0);
const runsCurrentPage = ref(1);
const runsPageSize = 20;

const [RunsDrawer, runsDrawerApi] = useVbenDrawer();

function recomputeRunsPage() {
  const start = (runsCurrentPage.value - 1) * runsPageSize;
  taskRuns.value = allTaskRuns.value.slice(start, start + runsPageSize);
}

async function loadRuns(taskId: string) {
  runsLoading.value = true;
  try {
    const { listScheduledTaskRunsApi } = await import('#/api/scheduled-tasks');
    allTaskRuns.value = await listScheduledTaskRunsApi(taskId, runsPageSize * 10);
    runsTotal.value = allTaskRuns.value.length;
    recomputeRunsPage();
  } finally {
    runsLoading.value = false;
  }
}

function handleRunsPageChange(page: number) {
  runsCurrentPage.value = page;
  recomputeRunsPage();
}

async function handleCancelRun(run: ScheduledTaskRun) {
  await ElMessageBox.confirm('确定要取消此运行吗？', '确认', { type: 'warning' });
  await cancelTaskRunApi(run.id);
  ElMessage.success('已发送取消请求');
  if (currentTaskId.value) {
    await loadRuns(currentTaskId.value);
  }
}

async function handleRetryRun(run: ScheduledTaskRun) {
  await ElMessageBox.confirm('确定要重试此运行吗？', '确认', { type: 'info' });
  const result = await retryTaskRunApi(run.id);
  ElMessage.success(`已加入队列：${result.runId}`);
  if (currentTaskId.value) {
    await loadRuns(currentTaskId.value);
  }
}

const logDrawerVisible = ref(false);
const logRunId = ref('');
const logLines = ref<ScheduledTaskLogEntry[]>([]);
const logLoading = ref(false);
const logSequence = ref(0);
let logTimer: ReturnType<typeof setInterval> | null = null;

const logLevelFilter = ref('All');
const logLevelOptions = ['All', 'Warning', 'Error'];
const filteredLogLines = computed(() => {
  if (logLevelFilter.value === 'All') return logLines.value;
  const threshold = logLevelFilter.value;
  const order: Record<string, number> = { Debug: 0, Information: 1, Warning: 2, Error: 3, Critical: 4 };
  const min = order[threshold] ?? 0;
  return logLines.value.filter((l) => (order[l.level] ?? 0) >= min);
});

function openLogs(run: ScheduledTaskRun) {
  logRunId.value = run.id;
  logLines.value = [];
  logSequence.value = 0;
  logDrawerVisible.value = true;
  loadLogs();
  logTimer = setInterval(loadLogs, 2000);
}

async function loadLogs() {
  if (!logRunId.value) return;
  logLoading.value = true;
  try {
    const page = await getTaskRunLogsApi(logRunId.value, logSequence.value, 500);
    logLines.value = [...logLines.value, ...page.items];
    logSequence.value = page.nextSequence;
    if (page.isComplete && logTimer) {
      clearInterval(logTimer);
      logTimer = null;
    }
  } finally {
    logLoading.value = false;
  }
}

function closeLogDrawer() {
  if (logTimer) {
    clearInterval(logTimer);
    logTimer = null;
  }
  logDrawerVisible.value = false;
  logRunId.value = '';
  logLines.value = [];
  logSequence.value = 0;
}

function formatLogTime(dt: string) {
  return new Date(dt).toLocaleString('zh-CN', { hour12: false });
}

function logLevelClass(level: string) {
  const map: Record<string, string> = {
    Error: 'text-red-500',
    Warning: 'text-yellow-600',
    Information: 'text-blue-500',
    Debug: 'text-gray-500',
    Trace: 'text-gray-400',
  };
  return map[level] || '';
}

function openAudits(task: ScheduledTaskDefinition) {
  auditDrawerApi.open();
  auditsCurrentPage.value = 1;
  currentAuditTaskId.value = task.id;
  loadAudits(task.id);
  auditTaskName.value = task.name;
}

const auditTaskName = ref('');
const currentAuditTaskId = ref('');
const taskAudits = ref<ScheduledTaskAudit[]>([]);
const allTaskAudits = ref<ScheduledTaskAudit[]>([]);
const auditsLoading = ref(false);
const auditsTotal = ref(0);
const auditsCurrentPage = ref(1);
const auditsPageSize = 20;

const [AuditDrawer, auditDrawerApi] = useVbenDrawer();

function recomputeAuditsPage() {
  const start = (auditsCurrentPage.value - 1) * auditsPageSize;
  taskAudits.value = allTaskAudits.value.slice(start, start + auditsPageSize);
}

async function loadAudits(taskId: string) {
  auditsLoading.value = true;
  try {
    const result = await listScheduledTaskAuditsApi(taskId, 200);
    allTaskAudits.value = result;
    auditsTotal.value = result.length;
    recomputeAuditsPage();
  } finally {
    auditsLoading.value = false;
  }
}

function handleAuditsPageChange(page: number) {
  auditsCurrentPage.value = page;
  recomputeAuditsPage();
}

type TagType = 'primary' | 'success' | 'warning' | 'info' | 'danger' | undefined;

function statusTagType(status: string): TagType {
  const map: Record<string, TagType> = {
    Cancelled: 'info',
    Failed: 'danger',
    Interrupted: 'warning',
    Running: 'primary',
    Succeeded: 'success',
    TimedOut: 'danger',
  };
  return map[status];
}

const actionLabels: Record<string, string> = {
  Cancel: '取消',
  Create: '创建',
  Delete: '删除',
  Disable: '禁用',
  Enable: '启用',
  ManualRetry: '手动重试',
  RunNow: '立即运行',
  Update: '更新',
};

function actionLabel(action: string) {
  return actionLabels[action] || action;
}

function actionTagType(action: string): TagType {
  const map: Record<string, TagType> = {
    Cancel: 'warning',
    Create: 'success',
    Delete: 'danger',
    Disable: 'info',
    Enable: 'success',
    RunNow: 'primary',
    Update: 'primary',
  };
  return map[action];
}

function formatTime(dt: string | null) {
  if (!dt) return '-';
  return new Date(dt).toLocaleString();
}

onMounted(loadTasks);
</script>

<template>
  <Page title="定时任务" description="管理 Quartz.NET 定时任务">
    <ElCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span>任务列表</span>
          <ElButton v-if="canCreate" type="primary" @click="openCreate">新建任务</ElButton>
        </div>
      </template>

      <ElTable :data="tasks" v-loading="loading" stripe>
        <ElTableColumn prop="name" label="任务名称" min-width="180" />
        <ElTableColumn prop="taskType" label="类型" width="100">
          <template #default="{ row }">
            <ElTag size="small">{{ row.taskType }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="cronExpression" label="Cron 表达式" width="150" />
        <ElTableColumn label="启用" width="90" align="center">
          <template #default="{ row }">
            <ElSwitch
              v-if="canUpdate"
              :model-value="row.isEnabled"
              size="small"
              @change="handleToggleEnabled(row as ScheduledTaskDefinition)"
            />
            <ElTag v-else :type="row.isEnabled ? 'success' : 'info'" size="small">
              {{ row.isEnabled ? '启用' : '停用' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="timeoutSeconds" label="超时 (秒)" width="100" />
        <ElTableColumn label="重试" width="120">
          <template #default="{ row }">
            {{ row.retryCount > 0 ? `${row.retryCount}次 / ${row.retryIntervalSeconds}秒` : '-' }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="更新时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.updatedAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="220" fixed="right">
          <template #default="{ row }">
            <ElButton v-if="canUpdate" size="small" @click="openEdit(row as ScheduledTaskDefinition)">编辑</ElButton>
            <ElButton v-if="canTrigger" size="small" type="success" @click="handleRunNow(row as ScheduledTaskDefinition)">
              立即运行
            </ElButton>
            <ElDropdown trigger="click">
              <ElButton size="small">
                更多 ▾
              </ElButton>
              <template #dropdown>
                <ElDropdownMenu>
                  <ElDropdownItem @click="openRuns(row as ScheduledTaskDefinition)">
                    运行记录
                  </ElDropdownItem>
                  <ElDropdownItem @click="openAudits(row as ScheduledTaskDefinition)">
                    审计日志
                  </ElDropdownItem>
                  <ElDropdownItem
                    v-if="canDelete"
                    divided
                    @click="handleDelete(row as ScheduledTaskDefinition)"
                  >
                    <span class="text-red-500">删除</span>
                  </ElDropdownItem>
                </ElDropdownMenu>
              </template>
            </ElDropdown>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <Drawer :title="isCreate ? '新建任务' : '编辑任务'" class="w-150">
      <Form />
      <template #footer>
        <ElButton @click="drawerApi.close()">取消</ElButton>
        <ElButton type="primary" @click="handleSubmit">
          {{ isCreate ? '创建' : '更新' }}
        </ElButton>
      </template>
    </Drawer>

    <RunsDrawer :title="`运行记录：${currentTaskName}`" class="w-250">
      <ElTable :data="taskRuns" v-loading="runsLoading" stripe size="small">
        <ElTableColumn prop="status" label="状态" width="110">
          <template #default="{ row }">
            <ElTag :type="statusTagType(row.status)" size="small">
              {{ row.status }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="triggerType" label="触发类型" width="100" />
        <ElTableColumn prop="attemptNumber" label="尝试次数" width="90" align="center" />
        <ElTableColumn label="开始时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.startedAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="结束时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.finishedAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn prop="resultSummary" label="结果摘要" min-width="200" show-overflow-tooltip />
        <ElTableColumn prop="errorSummary" label="错误信息" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.errorSummary" class="text-red-500">{{ row.errorSummary }}</span>
            <span v-else>-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <ElButton size="small" @click="openLogs(row as ScheduledTaskRun)">日志</ElButton>
            <ElButton
              v-if="['Queued', 'Running'].includes((row as ScheduledTaskRun).status)"
              size="small"
              type="warning"
              @click="handleCancelRun(row as ScheduledTaskRun)"
            >
              取消
            </ElButton>
            <ElButton
              v-if="!['Queued', 'Running'].includes((row as ScheduledTaskRun).status)"
              size="small"
              type="primary"
              @click="handleRetryRun(row as ScheduledTaskRun)"
            >
              重试
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
      <div class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="runsCurrentPage"
          :page-size="runsPageSize"
          :total="runsTotal"
          layout="total, prev, pager, next"
          @current-change="handleRunsPageChange"
        />
      </div>
    </RunsDrawer>

    <ElDrawer
      v-model="logDrawerVisible"
      :title="`执行日志 ${logRunId}`"
      size="60%"
      @close="closeLogDrawer"
    >
      <div class="mb-3 flex items-center justify-between">
        <ElRadioGroup v-model="logLevelFilter" size="small">
          <ElRadioButton v-for="opt in logLevelOptions" :key="opt" :value="opt">{{ opt === 'All' ? '全部' : opt }}</ElRadioButton>
        </ElRadioGroup>
        <span class="text-gray-400 text-xs">{{ filteredLogLines.length }} / {{ logLines.length }} 条</span>
      </div>
      <div v-loading="logLoading" class="log-container">
        <div v-if="filteredLogLines.length === 0" class="text-gray-400">暂无日志</div>
        <div v-for="line in filteredLogLines" :key="line.sequence" :class="['log-line', `log-${line.level.toLowerCase()}`]">
          <span class="log-time">{{ formatLogTime(line.timestampUtc) }}</span>
          <span :class="['log-level', logLevelClass(line.level)]">[{{ line.level }}]</span>
          <span class="log-category">{{ line.category }}</span>
          <pre class="log-message">{{ line.message }}</pre>
          <pre v-if="line.exception" class="log-exception">{{ line.exception }}</pre>
        </div>
      </div>
    </ElDrawer>

    <AuditDrawer :title="`审计日志：${auditTaskName}`" class="w-280">
      <ElTable :data="taskAudits" v-loading="auditsLoading" stripe size="small">
        <ElTableColumn prop="action" label="操作" width="100">
          <template #default="{ row }">
            <ElTag :type="actionTagType(row.action)" size="small">
              {{ actionLabel(row.action) }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="actorName" label="操作人" width="120" />
        <ElTableColumn prop="actorType" label="角色" width="100">
          <template #default="{ row }">
            <ElTag size="small" type="info">{{ row.actorType }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.occurredAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn prop="ipAddress" label="IP 地址" width="130" />
        <ElTableColumn label="变更详情" min-width="250">
          <template #default="{ row }">
            <div v-if="row.before || row.after" class="text-xs">
              <div v-if="row.before" class="mb-1">
                <span class="font-bold text-red-500">变更前：</span>
                <pre class="m-0 whitespace-pre-wrap break-all">{{ JSON.stringify(row.before, null, 2) }}</pre>
              </div>
              <div v-if="row.after">
                <span class="font-bold text-green-600">变更后：</span>
                <pre class="m-0 whitespace-pre-wrap break-all">{{ JSON.stringify(row.after, null, 2) }}</pre>
              </div>
            </div>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
      </ElTable>
      <div class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="auditsCurrentPage"
          :page-size="auditsPageSize"
          :total="auditsTotal"
          layout="total, prev, pager, next"
          @current-change="handleAuditsPageChange"
        />
      </div>
    </AuditDrawer>
  </Page>
</template>

<style scoped>
.log-container {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 12px;
  line-height: 1.6;
  max-height: calc(100vh - 240px);
  overflow-y: auto;
}

.log-line {
  padding: 6px 8px;
  border-left: 3px solid transparent;
  border-bottom: 1px solid #f0f0f0;
}

.log-line.log-information { border-left-color: #409eff; }
.log-line.log-warning { border-left-color: #e6a23c; background: #fdf6ec; }
.log-line.log-error,
.log-line.log-critical { border-left-color: #f56c6c; background: #fef0f0; }

.log-time {
  color: #909399;
  margin-right: 8px;
}

.log-level {
  font-weight: 600;
  margin-right: 8px;
}

.log-category {
  color: #909399;
  font-size: 11px;
}

.log-message {
  display: block;
  margin: 4px 0 0;
  padding: 6px 8px;
  background: #f8f9fa;
  border-radius: 3px;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 12px;
}

.log-exception {
  display: block;
  margin: 4px 0 0;
  padding: 6px 8px;
  background: #fff5f5;
  border-radius: 3px;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 12px;
  color: #c53030;
}
</style>
