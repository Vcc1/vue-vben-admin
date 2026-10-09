<script setup lang="ts">
import { onMounted, ref } from 'vue';

import { Profile } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';

import { ElDescriptions, ElDescriptionsItem, ElTag } from 'element-plus';

import { type OidcUserInfo, getUserInfoApi } from '#/api';

const userStore = useUserStore();
const tabsValue = ref('basic');
const tabs = [{ label: '基本信息', value: 'basic' }];

const info = ref<OidcUserInfo | null>(null);

onMounted(async () => {
  info.value = await getUserInfoApi();
});
</script>
<template>
  <Profile
    v-model:model-value="tabsValue"
    title="个人中心"
    :user-info="userStore.userInfo"
    :tabs="tabs"
  >
    <template #content>
      <ElDescriptions v-if="info" :column="1" border>
        <ElDescriptionsItem label="用户名">
          {{ info.username || '-' }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="显示名称">
          {{ info.realName || '-' }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="邮箱">
          {{ info.email || '-' }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="角色">
          <template v-if="info.roles?.length">
            <ElTag v-for="role in info.roles" :key="role" class="mr-2" size="small">
              {{ role }}
            </ElTag>
          </template>
          <span v-else>-</span>
        </ElDescriptionsItem>
        <ElDescriptionsItem label="权限">
          <template v-if="info.permissions?.length">
            <ElTag
              v-for="code in info.permissions"
              :key="code"
              class="mr-2 mb-1"
              size="small"
              type="info"
            >
              {{ code }}
            </ElTag>
          </template>
          <span v-else>-</span>
        </ElDescriptionsItem>
      </ElDescriptions>
      <div class="mt-4 text-xs text-gray-400">
        以上信息来自 OIDC 统一身份认证，仅支持查看；如需修改请前往 Admin.Host 管理后台。
      </div>
    </template>
  </Profile>
</template>
