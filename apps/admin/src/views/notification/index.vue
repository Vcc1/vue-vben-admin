<script lang="ts" setup>
import { computed, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElEmpty,
  ElPagination,
  ElTabPane,
  ElTabs,
  ElTag,
} from 'element-plus';

import {
  type NotificationDto,
  type NotificationTypeDto,
  getMyNotificationsApi,
  getNotificationTypesApi,
  markAllAsReadApi,
  markAsReadApi,
} from '#/api/notification';
import { useNotificationStore } from '#/store';

const notificationStore = useNotificationStore();

const loading = ref(false);
const notifications = ref<NotificationDto[]>([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = 20;
const activeType = ref('');
const types = ref<NotificationTypeDto[]>([]);

const typeOptions = computed(() => [
  { label: '全部', value: '' },
  ...types.value.map((t) => ({ label: t.displayName, value: t.typeCode })),
]);

async function loadTypes() {
  try {
    types.value = await getNotificationTypesApi();
  } catch {
    // 静默处理
  }
}

async function loadNotifications() {
  loading.value = true;
  try {
    const skip = (currentPage.value - 1) * pageSize;
    const page = await getMyNotificationsApi({
      skip,
      take: pageSize,
      typeCode: activeType.value || undefined,
    });
    notifications.value = page.items;
    totalCount.value = page.totalCount;
  } finally {
    loading.value = false;
  }
}

async function handleMarkRead(item: NotificationDto) {
  await markAsReadApi([item.id]);
  item.isRead = true;
  notificationStore.refreshUnreadCount();
}

async function handleMarkAllRead() {
  await markAllAsReadApi();
  for (const n of notifications.value) {
    n.isRead = true;
  }
  notificationStore.markAllRead();
}

function handleTypeChange(tab: string | number) {
  activeType.value = String(tab);
  currentPage.value = 1;
}

function handlePageChange(page: number) {
  currentPage.value = page;
}

function handleClick(item: NotificationDto) {
  if (!item.isRead) {
    handleMarkRead(item);
  }
  if (item.linkUrl) {
    if (item.linkUrl.startsWith('http')) {
      window.open(item.linkUrl, '_blank');
    }
  }
}

function formatTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return '刚刚';
  if (diffMin < 60) return `${diffMin}分钟前`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour}小时前`;
  const diffDay = Math.floor(diffHour / 24);
  if (diffDay < 30) return `${diffDay}天前`;
  return date.toLocaleDateString();
}

const unreadCount = computed(
  () => notifications.value.filter((n) => !n.isRead).length,
);

watch([activeType, currentPage], loadNotifications);

onMounted(() => {
  loadTypes();
  loadNotifications();
});
</script>

<template>
  <Page title="消息中心" description="查看和管理您的通知消息">
    <ElCard>
      <template #header>
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <span>通知列表</span>
            <ElTag v-if="unreadCount > 0" type="danger" size="small">
              {{ unreadCount }} 条未读
            </ElTag>
          </div>
          <ElButton :disabled="unreadCount === 0" @click="handleMarkAllRead">
            全部已读
          </ElButton>
        </div>
      </template>

      <ElTabs
        :model-value="activeType"
        class="mb-4"
        @tab-change="handleTypeChange"
      >
        <ElTabPane
          v-for="opt in typeOptions"
          :key="opt.value"
          :label="opt.label"
          :name="opt.value"
        />
      </ElTabs>

      <div v-loading="loading" class="space-y-3">
        <div
          v-for="item in notifications"
          :key="item.id"
          class="flex cursor-pointer items-start gap-3 rounded-lg border p-4 transition-colors hover:bg-gray-50"
          :class="{ 'bg-blue-50/50': !item.isRead }"
          @click="handleClick(item)"
        >
          <div class="mt-1.5 h-2 w-2 shrink-0 rounded-full">
            <div
              v-if="!item.isRead"
              class="h-2 w-2 rounded-full bg-blue-500"
            />
          </div>
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <span class="font-medium">{{ item.title }}</span>
              <ElTag size="small" type="info">
                {{ item.typeDisplayName }}
              </ElTag>
            </div>
            <p class="mt-1 text-sm text-gray-500">{{ item.summary }}</p>
            <span class="mt-1 text-xs text-gray-400">
              {{ formatTime(item.createdAtUtc) }}
            </span>
          </div>
          <ElButton
            v-if="!item.isRead"
            size="small"
            text
            @click.stop="handleMarkRead(item)"
          >
            标为已读
          </ElButton>
        </div>

        <ElEmpty v-if="!loading && notifications.length === 0" description="暂无通知" />
      </div>

      <div v-if="totalCount > pageSize" class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="currentPage"
          :page-size="pageSize"
          :total="totalCount"
          layout="total, prev, pager, next"
          @current-change="handlePageChange"
        />
      </div>
    </ElCard>
  </Page>
</template>
