import type { RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';
import {
  defaultResponseInterceptor,
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';

import { ElMessage } from 'element-plus';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL,
  });

  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();
      if (accessStore.accessToken) {
        config.headers.Authorization = `Bearer ${accessStore.accessToken}`;
      }
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  client.addResponseInterceptor(
    defaultResponseInterceptor({
      codeField: 'code',
      dataField: 'data',
      successCode: 0,
    }),
  );

  let isRefreshing = false;

  client.addResponseInterceptor({
    rejected: async (error) => {
      if (error?.response?.status === 401) {
        const accessStore = useAccessStore();
        const refreshToken = accessStore.refreshToken;

        if (refreshToken && !isRefreshing) {
          isRefreshing = true;
          try {
            const response = await fetch(`${baseURL}/auth/refresh`, {
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
              isRefreshing = false;
              window.location.reload();
              return Promise.reject(error);
            }
          } catch {
            // refresh failed
          }
          isRefreshing = false;
        }

        accessStore.setAccessToken(null);
        accessStore.setRefreshToken(null);

        const apiURL = import.meta.env.VITE_GLOB_API_URL as string;
        window.location.href = `${apiURL}/auth/login`;
      }
      return Promise.reject(error);
    },
  });

  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const responseData = error?.response?.data ?? {};
      const errorMessage = responseData?.error ?? responseData?.message ?? '';
      ElMessage.error(errorMessage || msg);
    }),
  );

  return client;
}

export const requestClient = createRequestClient(apiURL, {
  responseReturn: 'body',
});

export const baseRequestClient = new RequestClient({
  baseURL: apiURL,
});
baseRequestClient.addResponseInterceptor({
  fulfilled: (response) => response.data,
});
