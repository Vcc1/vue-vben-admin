<script setup lang="ts">
import { ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { Profile } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';

import ProfileApiKeys from './api-keys.vue';
import ProfileBase from './base-setting.vue';
import ProfilePasswordSetting from './password-setting.vue';
import ProfileSessionsDevices from './sessions-devices.vue';

const route = useRoute();
const userStore = useUserStore();

const TAB_VALUES = ['basic', 'password', 'sessions', 'apikeys'];

const tabsValue = ref<string>('basic');

// 用 watch 而不是一次性读取：已经在 /profile 上时再点用户菜单里的
// "我的设备与会话"，组件会被复用、setup 不重跑，只有 query 变化能切页签。
watch(
  () => route.query.tab,
  (tab) => {
    if (typeof tab === 'string' && TAB_VALUES.includes(tab)) {
      tabsValue.value = tab;
    }
  },
  { immediate: true },
);

const tabs = ref([
  {
    label: '基本信息',
    value: 'basic',
  },
  {
    label: '修改密码',
    value: 'password',
  },
  {
    label: '设备与会话',
    value: 'sessions',
  },
  {
    label: 'API Key',
    value: 'apikeys',
  },
]);
</script>
<template>
  <Profile
    v-model:model-value="tabsValue"
    title="个人中心"
    :user-info="userStore.userInfo"
    :tabs="tabs"
  >
    <template #content>
      <ProfileBase v-if="tabsValue === 'basic'" />
      <ProfilePasswordSetting v-if="tabsValue === 'password'" />
      <ProfileSessionsDevices v-if="tabsValue === 'sessions'" />
      <ProfileApiKeys v-if="tabsValue === 'apikeys'" />
    </template>
  </Profile>
</template>
