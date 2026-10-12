import type { RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  authenticateResponseInterceptor,
  defaultResponseInterceptor,
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';
import { useAccessStore } from '@vben/stores';

import { ElMessage } from 'element-plus';

import { useAuthStore, useStepUpStore } from '#/store';

import { refreshTokenApi } from './core';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

/** 这两个端点自身的 401 不能再触发刷新，否则会和自己形成死锁。 */
const AUTH_ENDPOINTS = ['/admin/auth/login', '/admin/auth/refresh'];

async function doReAuthenticate() {
  console.warn('Access token or refresh token is invalid or expired. ');
  const accessStore = useAccessStore();
  const authStore = useAuthStore();
  accessStore.setAccessToken(null);
  accessStore.setRefreshToken(null);
  if (
    preferences.app.loginExpiredMode === 'modal' &&
    accessStore.isAccessChecked
  ) {
    accessStore.setLoginExpired(true);
  } else {
    await authStore.logout();
  }
}

async function performRefreshToken(): Promise<string> {
  const accessStore = useAccessStore();
  const currentRefreshToken = accessStore.refreshToken;
  if (!currentRefreshToken) {
    await doReAuthenticate();
    return '';
  }
  const resp = await refreshTokenApi(currentRefreshToken);
  accessStore.setAccessToken(resp.accessToken);
  if (resp.refreshToken) {
    accessStore.setRefreshToken(resp.refreshToken);
  }
  return resp.accessToken;
}

let refreshInFlight: null | Promise<string> = null;

/**
 * refresh token 是一次性的，重复提交会被后端判定为重放并吊销整条令牌链。
 * requestClient 与 baseRequestClient 共用同一个在途 Promise，
 * 保证同一时刻全站只发一次刷新请求，其余 401 都等同一个结果。
 */
function doRefreshToken(): Promise<string> {
  refreshInFlight ??= performRefreshToken().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

function formatToken(token: null | string) {
  return token ? `Bearer ${token}` : null;
}

function isAuthEndpoint(url?: string) {
  return AUTH_ENDPOINTS.some((path) => url?.startsWith(path));
}

/**
 * 请求发出时用的 token 是否已被别处换掉。
 * access store 持久化在 localStorage，多页签共享：另一个页签刷新成功后，
 * 本页签手里的旧 token 必然 401，但这时正确的动作是带新 token 重放，不是登出。
 */
function tokenReplacedElsewhere(config: any) {
  const accessStore = useAccessStore();
  const current = accessStore.accessToken;
  return !!current && String(config?.headers?.Authorization ?? '') !== `Bearer ${current}`;
}

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL,
  });

  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();
      config.headers.Authorization = formatToken(accessStore.accessToken);
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

  client.addResponseInterceptor(
    authenticateResponseInterceptor({
      client,
      doReAuthenticate,
      doRefreshToken,
      enableRefreshToken: preferences.app.enableRefreshToken,
      formatToken,
    }),
  );

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

// 登录接口跨端口（前端 5101 → Admin.Host 5100）返回 Set-Cookie，
// OIDC 授权端点依赖该 cookie，不带 credentials 浏览器会直接丢弃。
export const baseRequestClient = new RequestClient({
  baseURL: apiURL,
  withCredentials: true,
});
baseRequestClient.addRequestInterceptor({
  fulfilled: async (config) => {
    const accessStore = useAccessStore();
    const token = accessStore.accessToken;
    config.headers.Authorization = token ? `Bearer ${token}` : null;
    return config;
  },
});
baseRequestClient.addResponseInterceptor({
  fulfilled: (response) => response.data,
  rejected: async (error) => {
    const response = error?.response;
    const config = error?.config;
    if (response?.status !== 401 || !config) {
      throw error;
    }

    // 敏感操作要求更高的登录等级时，后端返回 401 + stepup_required。
    // 必须排在刷新 token 之前处理：刷新出来的新 token 只是等级快照换了张皮，
    // 重放照样 401，白白消耗一次一次性的 refresh token。
    if (response.data?.error === 'stepup_required' && !config.__isStepUpRetry) {
      const passed = await useStepUpStore().requestInSessionStepUp(
        response.data,
      );
      if (passed) {
        config.__isStepUpRetry = true;
        return baseRequestClient.request(config.url, { ...config });
      }
      // 用户主动取消二次验证：原样抛出，不能顺势把人登出。
      throw error;
    }

    // 登录/刷新端点自身的 401 交给发起方处理：在这里登出会让别的页签正常的刷新把整站踢掉。
    if (isAuthEndpoint(config.url)) {
      throw error;
    }

    if (config.__isRetryRequest) {
      await doReAuthenticate();
      throw error;
    }

    // 别的页签刚刷新过：直接带新 token 重放，省掉一次刷新。
    if (tokenReplacedElsewhere(config)) {
      config.__isRetryRequest = true;
      return baseRequestClient.request(config.url, { ...config });
    }

    if (!preferences.app.enableRefreshToken) {
      await doReAuthenticate();
      throw error;
    }

    config.__isRetryRequest = true;
    try {
      const newToken = await doRefreshToken();
      if (!newToken) {
        throw error;
      }
      return baseRequestClient.request(config.url, { ...config });
    } catch (refreshError) {
      // 刷新失败也可能是并发竞态：另一个页签已经拿到新 token 了。
      if (tokenReplacedElsewhere(config)) {
        return baseRequestClient.request(config.url, { ...config });
      }
      await doReAuthenticate();
      throw refreshError;
    }
  },
});
