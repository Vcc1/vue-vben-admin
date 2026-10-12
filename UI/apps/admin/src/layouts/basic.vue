<script lang="ts" setup>
import type { NotificationItem } from '@vben/layouts';

import { computed, onMounted, onUnmounted, watch } from 'vue';
import { useRouter } from 'vue-router';

import { AuthenticationLoginExpiredModal } from '@vben/common-ui';
import { useWatermark } from '@vben/hooks';
import {
  BasicLayout,
  LockScreen,
  Notification,
  UserDropdown,
} from '@vben/layouts';
import { preferences, usePreferences } from '@vben/preferences';
import { useAccessStore, useUserStore } from '@vben/stores';

import StepUpDialog from '#/components/step-up-dialog.vue';
import { $t } from '#/locales';
import { useAuthStore, useNotificationStore } from '#/store';
import LoginForm from '#/views/_core/authentication/login.vue';

const router = useRouter();
const userStore = useUserStore();
const authStore = useAuthStore();
const accessStore = useAccessStore();
const notificationStore = useNotificationStore();
const { destroyWatermark, updateWatermark } = useWatermark();
const { isDark } = usePreferences();

const notifications = computed<NotificationItem[]>(() =>
  notificationStore.notifications.map((n) => ({
    id: n.id,
    avatar: '',
    date: formatRelativeTime(n.createdAtUtc),
    isRead: n.isRead,
    message: n.summary,
    title: n.title,
    link: n.linkUrl || undefined,
  })),
);

const showDot = computed(() => notificationStore.hasUnread);

function formatRelativeTime(dateStr: string): string {
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

onMounted(() => {
  notificationStore.loadInitial();
  notificationStore.startSignalR();
  // 请求浏览器通知权限
  if (
    typeof Notification !== 'undefined' &&
    Notification.permission === 'default'
  ) {
    Notification.requestPermission();
  }
});

onUnmounted(() => {
  notificationStore.stopSignalR();
});

const menus = computed(() => [
  {
    handler: () => {
      window.open('http://paycardhubadmin.v.m-tu.com', '_blank');
    },
    icon: 'lucide:credit-card',
    text: 'VCC后台',
  },
  {
    divider: true,
    handler: () => {
      router.push({ name: 'Profile', query: { tab: 'sessions' } });
    },
    icon: 'lucide:smartphone',
    text: $t('page.session.myDevices'),
  },
  {
    handler: () => {
      router.push({ name: 'Profile' });
    },
    icon: 'lucide:user',
    text: $t('page.auth.profile'),
  },
]);

const avatar = computed(() => {
  if (userStore.userInfo?.avatar) return userStore.userInfo.avatar;
  const name = userStore.userInfo?.realName || userStore.userInfo?.username || '';
  const match = name.match(/\p{L}/u);
  const char = match ? match[0] : '?';
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="#3b82f6"/><text x="50" y="50" font-size="50" text-anchor="middle" dominant-baseline="central" fill="white" font-family="system-ui, sans-serif">${char}</text></svg>`;
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
});

const userEmail = computed(() => userStore.userInfo?.email || '');

async function handleLogout() {
  await authStore.logout();
}

function handleNoticeClear() {
  notificationStore.markAllRead();
}

function markRead(id: number | string) {
  notificationStore.markRead([String(id)]);
}

function remove(id: number | string) {
  // 前端移除（不影响后端状态）
  const idx = notificationStore.notifications.findIndex(
    (n) => n.id === String(id),
  );
  if (idx >= 0) {
    notificationStore.notifications.splice(idx, 1);
  }
}

function handleMakeAll() {
  notificationStore.markAllRead();
}

const viewAll = () => {
  router.push({ name: 'NotificationCenter' });
};

const handleClick = (item: NotificationItem) => {
  if (item.link) {
    navigateTo(item.link, item.query, item.state);
  }
};

function navigateTo(
  link: string,
  query?: Record<string, any>,
  state?: Record<string, any>,
) {
  if (link.startsWith('http://') || link.startsWith('https://')) {
    window.open(link, '_blank');
  } else {
    router.push({
      path: link,
      query: query || {},
      state,
    });
  }
}

watch(
  () => ({
    enable: preferences.app.watermark,
    content: preferences.app.watermarkContent,
    isDark: isDark.value,
  }),
  async ({ enable, content, isDark: isDarkValue }) => {
    if (enable) {
      const watermarkColor = isDarkValue
        ? 'rgba(255, 255, 255, 0.12)'
        : 'rgba(0, 0, 0, 0.12)';

      await updateWatermark({
        advancedStyle: {
          colorStops: [
            {
              color: watermarkColor,
              offset: 0,
            },
            {
              color: watermarkColor,
              offset: 1,
            },
          ],
          type: 'linear',
        },
        content:
          content ||
          `${userStore.userInfo?.username} - ${userStore.userInfo?.realName}`,
      });
    } else {
      destroyWatermark();
    }
  },
  {
    immediate: true,
  },
);
</script>

<template>
  <BasicLayout
    :avatar
    :text="userStore.userInfo?.realName"
    @clear-preferences-and-logout="handleLogout"
    @logout="handleLogout"
  >
    <template #user-dropdown>
      <UserDropdown
        :avatar
        :menus
        :text="userStore.userInfo?.realName"
        :description="userEmail"
        @clear-preferences-and-logout="handleLogout"
        @logout="handleLogout"
      />
    </template>
    <template #notification>
      <Notification
        :dot="showDot"
        :notifications="notifications"
        @clear="handleNoticeClear"
        @read="(item) => item.id && markRead(item.id)"
        @remove="(item) => item.id && remove(item.id)"
        @make-all="handleMakeAll"
        @on-click="handleClick"
        @view-all="viewAll"
      />
    </template>
    <template #extra>
      <AuthenticationLoginExpiredModal
        v-model:open="accessStore.loginExpired"
        :avatar
      >
        <LoginForm />
      </AuthenticationLoginExpiredModal>
      <StepUpDialog />
    </template>
    <template #lock-screen>
      <LockScreen :avatar @to-login="handleLogout" />
    </template>
  </BasicLayout>
</template>
