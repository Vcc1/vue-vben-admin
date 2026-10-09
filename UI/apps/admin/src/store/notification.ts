import type { NotificationDto } from '#/api/notification';

import { computed, ref } from 'vue';

import { defineStore } from 'pinia';
import {
  getMyNotificationsApi,
  getUnreadCountApi,
  markAllAsReadApi,
  markAsReadApi,
} from '#/api/notification';
import { useAccessStore } from '@vben/stores';
import { useAppConfig } from '@vben/hooks';
import { ElNotification } from 'element-plus';
import * as signalR from '@microsoft/signalr';
import { useRouter } from 'vue-router';

export const useNotificationStore = defineStore(
  'notification',
  () => {
    const router = useRouter();
    const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);
    const notifications = ref<NotificationDto[]>([]);
    const unreadCount = ref(0);
    const connection = ref<signalR.HubConnection | null>(null);
    const loaded = ref(false);

    const hasUnread = computed(() => unreadCount.value > 0);

    async function loadInitial() {
      try {
        const [page, count] = await Promise.all([
          getMyNotificationsApi({ skip: 0, take: 20 }),
          getUnreadCountApi(),
        ]);
        notifications.value = page.items;
        unreadCount.value = count;
        loaded.value = true;
      } catch {
        // 静默处理，不影响用户体验
      }
    }

    async function refreshUnreadCount() {
      try {
        unreadCount.value = await getUnreadCountApi();
      } catch {
        // 静默处理
      }
    }

    async function markRead(ids: string[]) {
      await markAsReadApi(ids);
      for (const n of notifications.value) {
        if (ids.includes(n.id)) {
          n.isRead = true;
        }
      }
      await refreshUnreadCount();
    }

    async function markAllRead() {
      await markAllAsReadApi();
      for (const n of notifications.value) {
        n.isRead = true;
      }
      unreadCount.value = 0;
    }

    function startSignalR() {
      if (connection.value) return;

      const accessStore = useAccessStore();
      const token = accessStore.accessToken;
      if (!token) return;

      // 开发环境走 vite proxy，生产环境拼完整 URL
      const hubUrl = apiURL.startsWith('http')
        ? `${apiURL.replace(/\/api\/?$/, '')}/hubs/notification`
        : '/hubs/notification';

      const conn = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => accessStore.accessToken || '',
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .build();

      conn.on('ReceiveNotification', (dto: NotificationDto) => {
        notifications.value.unshift(dto);
        unreadCount.value++;

        // 浏览器原生通知（页面在后台时）
        if (document.hidden && Notification.permission === 'granted') {
          const sysNotif = new Notification(dto.title, {
            body: dto.summary,
            icon: '/favicon.ico',
          });
          sysNotif.onclick = () => {
            window.focus();
            if (dto.linkUrl) {
              if (dto.linkUrl.startsWith('http')) {
                window.open(dto.linkUrl, '_blank');
              } else {
                router.push(dto.linkUrl);
              }
            }
          };
        }

        // Element Plus 弹出提示（页面在前台时）
        if (!document.hidden) {
          ElNotification({
            title: dto.title,
            message: dto.summary,
            type: 'info',
            duration: 4000,
          });
        }
      });

      conn.start().catch(() => {
        // 静默处理连接失败
      });

      connection.value = conn;
    }

    function stopSignalR() {
      connection.value?.stop();
      connection.value = null;
    }

    return {
      hasUnread,
      loaded,
      loadInitial,
      markAllRead,
      markRead,
      notifications,
      refreshUnreadCount,
      startSignalR,
      stopSignalR,
      unreadCount,
    };
  },
);
