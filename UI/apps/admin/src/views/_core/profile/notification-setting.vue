<script setup lang="ts">
import { onMounted, ref } from 'vue';

import { ProfileNotificationSetting } from '@vben/common-ui';

import { ElMessage } from 'element-plus';

import {
  type UserNotificationPreferenceDto,
  getMyPreferencesApi,
  updateMyPreferencesApi,
} from '#/api/notification';

interface PreferenceItem {
  description: string;
  fieldName: string;
  label: string;
  value: boolean;
}

const formSchema = ref<PreferenceItem[]>([]);
const preferences = ref<UserNotificationPreferenceDto[]>([]);
const loading = ref(true);

async function loadPreferences() {
  loading.value = true;
  try {
    preferences.value = await getMyPreferencesApi();
    formSchema.value = preferences.value.map((p) => ({
      value: p.isEnabled && p.enabledChannels.length > 0,
      fieldName: p.typeCode,
      label: p.typeDisplayName,
      description: p.isEnabled
        ? '站内信'
        : '该通知类型已禁用',
    }));
  } finally {
    loading.value = false;
  }
}

async function handleChange(payload: Record<string, any>) {
  const fieldName = payload.fieldName as string;
  const value = payload.value as boolean;
  const pref = preferences.value.find((p) => p.typeCode === fieldName);
  if (!pref) return;

  const enabledChannels = value ? ['in_app'] : [];

  try {
    await updateMyPreferencesApi([
      { typeCode: fieldName, enabledChannels },
    ]);
    pref.enabledChannels = enabledChannels;
    ElMessage.success(value ? '已开启通知' : '已关闭通知');
  } catch {
    ElMessage.error('更新失败');
    await loadPreferences();
  }
}

onMounted(loadPreferences);
</script>
<template>
  <ProfileNotificationSetting
    :form-schema="formSchema"
    @change="handleChange"
  />
</template>
