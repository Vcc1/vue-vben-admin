<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElForm,
  ElFormItem,
  ElInput,
  ElMessage,
  ElOption,
  ElRadio,
  ElRadioGroup,
  ElSelect,
} from 'element-plus';

import {
  type NotificationTypeDto,
  getNotificationTypesApi,
  publishNotificationApi,
} from '#/api/notification';

const loading = ref(false);
const submitting = ref(false);
const types = ref<NotificationTypeDto[]>([]);

const form = ref({
  typeCode: '',
  title: '',
  summary: '',
  content: '',
  linkUrl: '',
  scope: 'all',
  targetIds: '',
});

const scopeOptions = [
  { label: '全体用户', value: 'all' },
  { label: '指定角色', value: 'roles' },
  { label: '指定用户', value: 'users' },
];

const enabledTypes = computed(() =>
  types.value.filter((t) => t.isEnabled),
);

async function loadTypes() {
  loading.value = true;
  try {
    types.value = await getNotificationTypesApi();
  } finally {
    loading.value = false;
  }
}

async function handleSubmit() {
  if (!form.value.typeCode) {
    ElMessage.warning('请选择通知类型');
    return;
  }
  if (!form.value.title.trim()) {
    ElMessage.warning('请输入标题');
    return;
  }
  if (!form.value.summary.trim()) {
    ElMessage.warning('请输入摘要');
    return;
  }

  const targetIds = form.value.scope === 'all'
    ? []
    : form.value.targetIds
        .split(/[\n,]/)
        .map((s) => s.trim())
        .filter(Boolean);

  if (form.value.scope !== 'all' && targetIds.length === 0) {
    ElMessage.warning('请输入目标 ID');
    return;
  }

  submitting.value = true;
  try {
    await publishNotificationApi({
      contentJson: JSON.stringify({ text: form.value.content }),
      linkUrl: form.value.linkUrl || null,
      scope: form.value.scope,
      summary: form.value.summary,
      targetIds,
      title: form.value.title,
      typeCode: form.value.typeCode,
    });
    ElMessage.success('通知发布成功');
    form.value = {
      typeCode: '',
      title: '',
      summary: '',
      content: '',
      linkUrl: '',
      scope: 'all',
      targetIds: '',
    };
  } finally {
    submitting.value = false;
  }
}

onMounted(loadTypes);
</script>

<template>
  <Page title="发布通知" description="手动发布通知消息给用户">
    <ElCard class="max-w-2xl">
      <template #header>
        <span>通知内容</span>
      </template>

      <ElForm
        v-loading="loading"
        :model="form"
        label-width="100px"
        label-position="right"
      >
        <ElFormItem label="通知类型" required>
          <ElSelect
            v-model="form.typeCode"
            placeholder="请选择通知类型"
            class="w-full"
          >
            <ElOption
              v-for="t in enabledTypes"
              :key="t.typeCode"
              :label="t.displayName"
              :value="t.typeCode"
            >
              <span>{{ t.displayName }}</span>
              <span class="ml-2 text-xs text-gray-400">{{ t.typeCode }}</span>
            </ElOption>
          </ElSelect>
        </ElFormItem>

        <ElFormItem label="标题" required>
          <ElInput
            v-model="form.title"
            placeholder="通知标题"
            maxlength="100"
            show-word-limit
          />
        </ElFormItem>

        <ElFormItem label="摘要" required>
          <ElInput
            v-model="form.summary"
            type="textarea"
            :rows="2"
            placeholder="通知摘要，显示在通知列表和弹窗中"
            maxlength="200"
            show-word-limit
          />
        </ElFormItem>

        <ElFormItem label="详细内容">
          <ElInput
            v-model="form.content"
            type="textarea"
            :rows="4"
            placeholder="通知的详细内容（可选）"
          />
        </ElFormItem>

        <ElFormItem label="跳转链接">
          <ElInput
            v-model="form.linkUrl"
            placeholder="用户点击通知后跳转的 URL（可选）"
          />
        </ElFormItem>

        <ElFormItem label="接收范围" required>
          <ElRadioGroup v-model="form.scope">
            <ElRadio
              v-for="opt in scopeOptions"
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </ElRadio>
          </ElRadioGroup>
        </ElFormItem>

        <ElFormItem
          v-if="form.scope === 'roles'"
          label="角色名称"
          required
        >
          <ElInput
            v-model="form.targetIds"
            type="textarea"
            :rows="2"
            placeholder="每行一个角色名，如 Admin、Operator"
          />
        </ElFormItem>

        <ElFormItem
          v-if="form.scope === 'users'"
          label="用户 ID"
          required
        >
          <ElInput
            v-model="form.targetIds"
            type="textarea"
            :rows="3"
            placeholder="每行一个用户 ID（GUID）"
          />
        </ElFormItem>

        <ElFormItem>
          <ElButton
            type="primary"
            :loading="submitting"
            @click="handleSubmit"
          >
            发布通知
          </ElButton>
        </ElFormItem>
      </ElForm>
    </ElCard>
  </Page>
</template>
