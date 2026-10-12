<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import {
  ElButton,
  ElCard,
  ElDatePicker,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  type ApiKeyItem,
  type CreatedApiKey,
  createMyApiKeyApi,
  listMyApiKeysApi,
  revokeMyApiKeyApi,
} from '#/api/api-keys';

const loading = ref(false);
const apiKeys = ref<ApiKeyItem[]>([]);

const dialogVisible = ref(false);
const creating = ref(false);
const newKeyName = ref('');
const newKeyExpires = ref<string | null>(null);

const createdKeyDialogVisible = ref(false);
const createdKey = ref<CreatedApiKey | null>(null);

async function loadApiKeys() {
  loading.value = true;
  try {
    apiKeys.value = await listMyApiKeysApi();
  } finally {
    loading.value = false;
  }
}

function openCreateDialog() {
  newKeyName.value = '';
  newKeyExpires.value = null;
  dialogVisible.value = true;
}

async function handleCreate() {
  if (!newKeyName.value.trim()) {
    ElMessage.warning('请输入名称');
    return;
  }
  creating.value = true;
  try {
    const result = await createMyApiKeyApi({
      expiresAtUtc: newKeyExpires.value
        ? new Date(newKeyExpires.value).toISOString()
        : null,
      name: newKeyName.value.trim(),
    });
    dialogVisible.value = false;
    createdKey.value = result;
    createdKeyDialogVisible.value = true;
    await loadApiKeys();
  } finally {
    creating.value = false;
  }
}

async function handleRevoke(key: ApiKeyItem) {
  await ElMessageBox.confirm(
    `确定要撤销 API Key "${key.name}" 吗？撤销后无法恢复。`,
    '确认',
    { type: 'warning' },
  );
  await revokeMyApiKeyApi(key.id);
  ElMessage.success('API Key 已撤销');
  await loadApiKeys();
}

function formatTime(dt: string | null) {
  if (!dt) return '-';
  return new Date(dt).toLocaleString('zh-CN', { hour12: false });
}

function copySecret() {
  if (!createdKey.value) return;
  navigator.clipboard.writeText(createdKey.value.secret);
  ElMessage.success('已复制到剪贴板');
}

onMounted(loadApiKeys);
</script>

<template>
  <ElCard>
    <template #header>
      <div class="flex items-center justify-between">
        <span>API Key 列表</span>
        <ElButton type="primary" @click="openCreateDialog">新建 API Key</ElButton>
      </div>
    </template>

    <ElTable :data="apiKeys" v-loading="loading" stripe>
      <ElTableColumn prop="name" label="名称" min-width="150" />
      <ElTableColumn prop="prefix" label="前缀" width="120">
        <template #default="{ row }">
          <code>{{ row.prefix }}</code>
        </template>
      </ElTableColumn>
      <ElTableColumn label="有效期" width="170">
        <template #default="{ row }">
          <ElTag v-if="row.revokedAtUtc" type="danger" size="small">已撤销</ElTag>
          <span v-else>{{ formatTime(row.expiresAtUtc) }}</span>
        </template>
      </ElTableColumn>
      <ElTableColumn label="最近使用" width="170">
        <template #default="{ row }">
          {{ formatTime(row.lastUsedAtUtc) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="创建时间" width="170">
        <template #default="{ row }">
          {{ formatTime(row.createdAtUtc) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="操作" width="100" fixed="right">
        <template #default="{ row }">
          <ElButton
            v-if="!row.revokedAtUtc"
            size="small"
            type="danger"
            @click="handleRevoke(row as ApiKeyItem)"
          >
            撤销
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>
  </ElCard>

  <ElDialog v-model="dialogVisible" title="新建 API Key" width="450px">
    <ElForm label-width="100px">
      <ElFormItem label="名称" required>
        <ElInput
          v-model="newKeyName"
          placeholder="如：生产环境、CI/CD"
          maxlength="100"
        />
      </ElFormItem>
      <ElFormItem label="过期时间">
        <ElDatePicker
          v-model="newKeyExpires"
          type="datetime"
          placeholder="留空表示永不过期"
          format="YYYY-MM-DD HH:mm"
          value-format="YYYY-MM-DDTHH:mm:ss"
          class="w-full"
        />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="dialogVisible = false">取消</ElButton>
      <ElButton type="primary" :loading="creating" @click="handleCreate">
        创建
      </ElButton>
    </template>
  </ElDialog>

  <ElDialog
    v-model="createdKeyDialogVisible"
    title="API Key 已创建"
    width="500px"
    :close-on-click-modal="false"
  >
    <div v-if="createdKey">
      <ElAlert
        title="请复制并妥善保存密钥，关闭后将无法再次查看"
        type="warning"
        :closable="false"
        show-icon
        class="mb-4"
      />
      <ElForm label-width="80px">
        <ElFormItem label="名称">
          <span>{{ createdKey.name }}</span>
        </ElFormItem>
        <ElFormItem label="前缀">
          <code>{{ createdKey.prefix }}</code>
        </ElFormItem>
        <ElFormItem label="密钥">
          <ElInput
            :model-value="createdKey.secret"
            readonly
            type="textarea"
            :autosize="{ minRows: 3, maxRows: 6 }"
            class="font-mono"
          />
        </ElFormItem>
      </ElForm>
    </div>
    <template #footer>
      <ElButton type="primary" @click="copySecret">复制密钥</ElButton>
      <ElButton @click="createdKeyDialogVisible = false">关闭</ElButton>
    </template>
  </ElDialog>
</template>
