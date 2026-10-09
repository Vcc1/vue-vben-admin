<script lang="ts" setup>
import { computed } from 'vue';

import { Page } from '@vben/common-ui';
import { preferences } from '@vben/preferences';
import { useUserStore } from '@vben/stores';

import { ElCard, ElDescriptions, ElDescriptionsItem, ElTag } from 'element-plus';

const userStore = useUserStore();

const currentTime = computed(() => {
  const hour = new Date().getHours();
  if (hour < 6) return '凌晨';
  if (hour < 9) return '早上';
  if (hour < 12) return '上午';
  if (hour < 14) return '中午';
  if (hour < 18) return '下午';
  if (hour < 22) return '晚上';
  return '深夜';
});

const currentDate = computed(() => {
  const date = new Date();
  const weekDays = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'];
  return `${date.getFullYear()}年${date.getMonth() + 1}月${date.getDate()}日 ${weekDays[date.getDay()]}`;
});
</script>

<template>
  <Page>
    <div class="space-y-6 p-6">
      <ElCard shadow="hover">
        <div class="flex items-center gap-6">
          <img
            :src="userStore.userInfo?.avatar || preferences.app.defaultAvatar"
            :alt="userStore.userInfo?.realName"
            class="h-20 w-20 rounded-full"
          />
          <div class="flex-1">
            <h1 class="mb-2 text-2xl font-semibold">
              {{ currentTime }}好，{{ userStore.userInfo?.realName || '用户' }}！
            </h1>
            <p class="text-gray-500">
              欢迎使用 PayCardHub 虚拟卡管理系统，{{ currentDate }}
            </p>
            <p class="mt-1 text-sm text-gray-400">
              高效管理虚拟卡支付，助力酒店业务运营
            </p>
          </div>
        </div>
      </ElCard>

      <ElCard shadow="hover">
        <template #header>
          <div class="flex items-center gap-2">
            <span class="text-lg font-medium">系统信息</span>
          </div>
        </template>
        <ElDescriptions :column="2" border>
          <ElDescriptionsItem label="系统名称">
            PayCardHub
          </ElDescriptionsItem>
          <ElDescriptionsItem label="当前用户">
            <ElTag type="primary">{{ userStore.userInfo?.username }}</ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="用户角色">
            <ElTag type="success">{{ userStore.userInfo?.realName }}</ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="系统状态">
            <ElTag type="success">运行中</ElTag>
          </ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard shadow="hover">
        <template #header>
          <div class="flex items-center gap-2">
            <span class="text-lg font-medium">快速入口</span>
          </div>
        </template>
        <div class="grid grid-cols-2 gap-4 md:grid-cols-3">
          <router-link
            to="/cards"
            class="flex items-center gap-3 rounded-lg border border-gray-200 p-4 transition-colors hover:border-blue-500 hover:bg-blue-50 dark:border-gray-700 dark:hover:bg-blue-900/30"
          >
            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-100 dark:bg-blue-900/50">
              <span class="text-2xl">💳</span>
            </div>
            <div>
              <div class="font-medium text-gray-900 dark:text-gray-100">虚拟卡管理</div>
              <div class="text-xs text-gray-500 dark:text-gray-400">查看和管理虚拟卡</div>
            </div>
          </router-link>
          <router-link
            to="/cardholders"
            class="flex items-center gap-3 rounded-lg border border-gray-200 p-4 transition-colors hover:border-blue-500 hover:bg-blue-50 dark:border-gray-700 dark:hover:bg-blue-900/30"
          >
            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-green-100 dark:bg-green-900/50">
              <span class="text-2xl">👤</span>
            </div>
            <div>
              <div class="font-medium text-gray-900 dark:text-gray-100">持卡人管理</div>
              <div class="text-xs text-gray-500 dark:text-gray-400">管理持卡人信息</div>
            </div>
          </router-link>
          <router-link
            to="/provider-accounts"
            class="flex items-center gap-3 rounded-lg border border-gray-200 p-4 transition-colors hover:border-blue-500 hover:bg-blue-50 dark:border-gray-700 dark:hover:bg-blue-900/30"
          >
            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-purple-100 dark:bg-purple-900/50">
              <span class="text-2xl">🏦</span>
            </div>
            <div>
              <div class="font-medium text-gray-900 dark:text-gray-100">发卡账户</div>
              <div class="text-xs text-gray-500 dark:text-gray-400">配置供应商账户</div>
            </div>
          </router-link>
          <router-link
            to="/transactions"
            class="flex items-center gap-3 rounded-lg border border-gray-200 p-4 transition-colors hover:border-blue-500 hover:bg-blue-50 dark:border-gray-700 dark:hover:bg-blue-900/30"
          >
            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-orange-100 dark:bg-orange-900/50">
              <span class="text-2xl">💰</span>
            </div>
            <div>
              <div class="font-medium text-gray-900 dark:text-gray-100">交易记录</div>
              <div class="text-xs text-gray-500 dark:text-gray-400">查看交易明细</div>
            </div>
          </router-link>
          <router-link
            to="/api-clients"
            class="flex items-center gap-3 rounded-lg border border-gray-200 p-4 transition-colors hover:border-blue-500 hover:bg-blue-50 dark:border-gray-700 dark:hover:bg-blue-900/30"
          >
            <div class="flex h-10 w-10 items-center justify-center rounded-lg bg-cyan-100 dark:bg-cyan-900/50">
              <span class="text-2xl">🔑</span>
            </div>
            <div>
              <div class="font-medium text-gray-900 dark:text-gray-100">API 客户端</div>
              <div class="text-xs text-gray-500 dark:text-gray-400">管理 API 访问权限</div>
            </div>
          </router-link>
        </div>
      </ElCard>
    </div>
  </Page>
</template>
