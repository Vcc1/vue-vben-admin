import { RequestClient } from '@vben/request';
import { useAccessStore } from '@vben/stores';

import { ElMessage } from 'element-plus';

import { handleUnauthorized } from './core';

const apiURL = import.meta.env.VITE_GLOB_API_URL as string;

const adminClient = new RequestClient({
  baseURL: `${apiURL}/admin-api`,
});

adminClient.addRequestInterceptor({
  fulfilled: (config) => {
    const accessStore = useAccessStore();
    if (accessStore.accessToken) {
      config.headers.Authorization = `Bearer ${accessStore.accessToken}`;
    }
    return config;
  },
});

adminClient.addResponseInterceptor({
  fulfilled: (response) => {
    return response.data;
  },
  rejected: async (error) => {
    const status = error?.response?.status;

    if (status === 401) {
      const accessStore = useAccessStore();
      const refreshToken = accessStore.refreshToken;

      if (refreshToken) {
        try {
          const response = await fetch(`${apiURL}/auth/refresh`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken }),
          });

          if (response.ok) {
            const data = await response.json();
            accessStore.setAccessToken(data.accessToken);
            if (data.refreshToken) {
              accessStore.setRefreshToken(data.refreshToken);
            }
            window.location.reload();
            return Promise.reject(error);
          }
        } catch {
          // refresh failed
        }
      }

      await handleUnauthorized();
      return Promise.reject(error);
    }

    const data = error?.response?.data;
    const msg =
      status === 403
        ? (data?.message ?? '当前账号没有访问该功能的权限。')
        : (data?.message || data?.ErrerMessage || error?.message || '请求失败');
    ElMessage.error(msg);
    return Promise.reject(error);
  },
});

export { adminClient };
