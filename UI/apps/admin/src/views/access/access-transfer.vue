<script lang="ts" setup>
import { computed, nextTick, ref } from 'vue';

import { SvgDownloadIcon } from '@vben/icons';
import {
  ElButton, ElCheckbox, ElDialog, ElMessage, ElMessageBox,
  ElTable, ElTableColumn, ElTag,
} from 'element-plus';

import {
  type AccessTransferChange, type AccessTransferDocument,
  type AccessTransferPreview, type TransferKind,
  applyAccessApi, compareAccessApi, exportAccessApi,
} from '#/api/admin-access';

import { reportAccessError } from './error';

const props = defineProps<{ kind: TransferKind }>();
const emit = defineEmits<{ applied: [] }>();
const input = ref<HTMLInputElement>();
const document = ref<AccessTransferDocument | null>(null);
const preview = ref<AccessTransferPreview | null>(null);
const fileName = ref('');
const selectedKeys = ref<string[]>([]);
const open = ref(false);
const loading = ref(false);
const tab = ref<'add' | 'all' | 'delete' | 'update'>('all');
const label = computed(() => props.kind === 'roles' ? '角色' : '权限');
const changes = computed(() => preview.value?.changes || []);
const visible = computed(() => tab.value === 'all'
  ? changes.value : changes.value.filter((item) => item.action === tab.value));
const counts = computed(() => ({
  add: changes.value.filter((item) => item.action === 'add').length,
  update: changes.value.filter((item) => item.action === 'update').length,
  delete: changes.value.filter((item) => item.action === 'delete').length,
}));

const selectableChanges = computed(() => changes.value.filter((item) => !item.blockReason));
const isAllSelected = computed(
  () => selectableChanges.value.length > 0 && selectedKeys.value.length === selectableChanges.value.length,
);

function toggleSelectAll() {
  if (isAllSelected.value) {
    selectedKeys.value = [];
  } else {
    selectedKeys.value = selectableChanges.value.map((item) => item.key);
  }
}

async function download() {
  loading.value = true;
  try {
    const data = await exportAccessApi(props.kind);
    const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], {
      type: 'application/json;charset=utf-8',
    }));
    const anchor = window.document.createElement('a');
    anchor.href = url;
    anchor.download = `admin-${props.kind}.json`;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (error) {
    reportAccessError(error);
  } finally {
    loading.value = false;
  }
}

async function chooseFile(event: Event) {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  target.value = '';
  if (!file) return;
  if (!file.name.toLowerCase().endsWith('.json') || file.size > 1_000_000) {
    ElMessage.warning('请选择不超过 1 MB 的 JSON 文件');
    return;
  }
  loading.value = true;
  try {
    const parsed = JSON.parse((await file.text()).replace(/^\uFEFF/, '')) as AccessTransferDocument;
    if (parsed?.kind !== props.kind) {
      ElMessage.warning(`文件类型不匹配，请上传${label.value}文件`);
      return;
    }
    const result = await compareAccessApi(parsed);
    document.value = parsed;
    preview.value = result;
    fileName.value = file.name;
    selectedKeys.value = [];
    tab.value = 'all';
    open.value = true;
    await nextTick();
  } catch (error) {
    if (error instanceof SyntaxError) ElMessage.error('JSON 格式不正确');
    else reportAccessError(error);
  } finally {
    loading.value = false;
  }
}

function toggle(key: string, checked: boolean) {
  selectedKeys.value = checked
    ? [...selectedKeys.value, key]
    : selectedKeys.value.filter((value) => value !== key);
}

function dataText(item: AccessTransferChange, before: boolean) {
  const value = props.kind === 'roles'
    ? (before ? item.beforeRole : item.afterRole)
    : (before ? item.beforePermission : item.afterPermission);
  if (!value) return '—';
  if ('permissions' in value) return value.permissions.join('\n') || '无授权';
  return `名称：${value.name}\n说明：${value.description ?? '—'}\n状态：${value.isEnabled ? '启用' : '停用'}`;
}

async function apply() {
  if (!document.value || !preview.value || selectedKeys.value.length === 0) return;
  const selected = changes.value.filter((item) => selectedKeys.value.includes(item.key));
  const deletions = selected.filter((item) => item.action === 'delete').length;
  try {
    await ElMessageBox.confirm(
      `将应用 ${selected.length} 项${label.value}变更，其中删除 ${deletions} 项。此操作会写入目标环境，确定继续？`,
      '确认导入', { type: 'warning', confirmButtonText: '确认修改' },
    );
  } catch { return; }
  loading.value = true;
  try {
    const result = await applyAccessApi(document.value, preview.value.baseline, selectedKeys.value);
    open.value = false;
    ElMessage.success(`已应用 ${result.appliedCount} 项变更`);
    emit('applied');
  } catch (error) {
    reportAccessError(error);
    // A stale preview must not remain actionable after a failed apply.
    preview.value = null;
    open.value = false;
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <ElButton :loading="loading" @click="download"><SvgDownloadIcon class="mr-1 size-4" />下载{{ label }}</ElButton>
  <ElButton :loading="loading" @click="input?.click()">上传{{ label }}</ElButton>
  <input ref="input" type="file" accept=".json,application/json" class="hidden" @change="chooseFile" />

  <ElDialog v-model="open" :title="`${label}导入对比 · ${fileName}`" width="min(960px, 96vw)" destroy-on-close>
    <div class="mb-3 flex flex-wrap items-center gap-2">
      <ElButton size="small" :type="tab === 'all' ? 'primary' : 'default'" @click="tab = 'all'">全部 {{ changes.length }}</ElButton>
      <ElButton size="small" :type="tab === 'add' ? 'primary' : 'default'" @click="tab = 'add'">新增 {{ counts.add }}</ElButton>
      <ElButton size="small" :type="tab === 'update' ? 'primary' : 'default'" @click="tab = 'update'">修改 {{ counts.update }}</ElButton>
      <ElButton size="small" :type="tab === 'delete' ? 'primary' : 'default'" @click="tab = 'delete'">删除 {{ counts.delete }}</ElButton>
    </div>
    <ElTable :data="visible" row-key="key" max-height="55vh" border class="w-full">
      <ElTableColumn label="应用" width="62" fixed="left">
        <template #default="{ row }">
          <ElCheckbox :model-value="selectedKeys.includes(row.key)" :disabled="!!row.blockReason"
            @change="(value) => toggle(row.key, Boolean(value))" />
        </template>
      </ElTableColumn>
      <ElTableColumn label="类型" width="75">
        <template #default="{ row }"><ElTag :type="row.action === 'delete' ? 'danger' : row.action === 'add' ? 'success' : 'warning'" size="small">{{ row.action === 'add' ? '新增' : row.action === 'delete' ? '删除' : '修改' }}</ElTag></template>
      </ElTableColumn>
      <ElTableColumn prop="key" :label="kind === 'roles' ? '角色' : '权限码'" min-width="160" show-overflow-tooltip />
      <ElTableColumn label="目标环境（修改前）" min-width="210">
        <template #default="{ row }"><div class="whitespace-pre-wrap break-all text-xs leading-5">{{ dataText(row as AccessTransferChange, true) }}</div></template>
      </ElTableColumn>
      <ElTableColumn label="文件内容（修改后）" min-width="210">
        <template #default="{ row }"><div class="whitespace-pre-wrap break-all text-xs leading-5">{{ dataText(row as AccessTransferChange, false) }}</div></template>
      </ElTableColumn>
      <ElTableColumn label="提示" min-width="180">
        <template #default="{ row }"><span :class="row.blockReason ? 'text-red-600' : ''">{{ row.blockReason || row.impact || '—' }}</span></template>
      </ElTableColumn>
    </ElTable>
    <template #footer>
      <div class="flex items-center justify-between">
        <span class="text-sm">
          已勾选 {{ selectedKeys.length }} / {{ selectableChanges.length }} 项
          <ElButton link type="primary" class="ml-2" @click="toggleSelectAll">
            {{ isAllSelected ? '取消全选' : '全选' }}
          </ElButton>
        </span>
        <div>
          <ElButton @click="open = false">取消</ElButton>
          <ElButton type="primary" :loading="loading" :disabled="!preview || !selectedKeys.length" @click="apply">确认修改</ElButton>
        </div>
      </div>
    </template>
  </ElDialog>
</template>
