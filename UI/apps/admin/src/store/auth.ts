import type { Recordable, UserInfo } from '@vben/types';

import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { preferences } from '@vben/preferences';
import { resetAllStores, useAccessStore, useUserStore } from '@vben/stores';

import { ElNotification } from 'element-plus';
import { defineStore } from 'pinia';

import { type AuthApi, getAccessCodesApi, getUserInfoApi, loginApi, logoutApi } from '#/api';
import { $t } from '#/locales';
import { useStepUpStore } from '#/store/stepup';

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const stepUpStore = useStepUpStore();
  const router = useRouter();

  const loginLoading = ref(false);

  /** 拿到 token 之后的公共流程：落地凭据、拉用户信息、跳转。 */
  async function completeLogin(
    accessToken: string,
    refreshToken?: string,
    onSuccess?: () => Promise<void> | void,
  ) {
    accessStore.setAccessToken(accessToken);
    if (refreshToken) {
      accessStore.setRefreshToken(refreshToken);
    }

    const returnUrl = new URLSearchParams(window.location.search).get(
      'returnUrl',
    );
    if (returnUrl) {
      window.location.href = returnUrl;
      return null;
    }

    try {
      const [fetchUserInfoResult, accessCodes] = await Promise.all([
        fetchUserInfo(),
        getAccessCodesApi(),
      ]);

      const userInfo = fetchUserInfoResult;
      userStore.setUserInfo(userInfo);
      accessStore.setAccessCodes(accessCodes);

      if (accessStore.loginExpired) {
        accessStore.setLoginExpired(false);
      } else if (onSuccess) {
        await onSuccess();
      } else {
        await router.push(userInfo.homePath || preferences.app.defaultHomePath);
      }

      if (userInfo?.realName) {
        ElNotification({
          message: `${$t('authentication.loginSuccessDesc')}:${userInfo?.realName}`,
          title: $t('authentication.loginSuccess'),
          type: 'success',
        });
      }
      return userInfo;
    } catch (error) {
      // 获取用户信息失败，清除 token 避免后续路由守卫误判
      accessStore.setAccessToken(null);
      accessStore.setRefreshToken(null);
      throw error;
    }
  }

  async function authLogin(
    params: Recordable<any>,
    onSuccess?: () => Promise<void> | void,
  ): Promise<{ success: boolean; userInfo: null | UserInfo }> {
    const loginParams = params as AuthApi.LoginParams;
    let userInfo: null | UserInfo = null;
    try {
      loginLoading.value = true;

      const { accessToken, refreshToken } = await loginApi(loginParams);
      if (accessToken) {
        userInfo = await completeLogin(accessToken, refreshToken, onSuccess);
      }
    } catch (error: any) {
      // 428 + stepUp：密码正确但设备不受信，需要二次验证后才能拿到 token。
      const stepUp = error?.response?.data?.stepUp;
      if (error?.response?.status === 428 && stepUp) {
        const tokens = await stepUpStore.requestLoginStepUp(stepUp, {
          rememberMe: !!loginParams.rememberMe,
        });
        if (tokens?.accessToken) {
          userInfo = await completeLogin(
            tokens.accessToken,
            tokens.refreshToken,
            onSuccess,
          );
        }
        return { success: true, userInfo };
      }

      // 428 无 stepUp：需要验证码，由登录页处理 UI，不弹全局通知。
      if (error?.response?.status === 428) {
        return { success: false, userInfo: null };
      }

      // 423：账户已锁定
      if (error?.response?.status === 423) {
        const message =
          error?.response?.data?.message || '账户已锁定，请稍后重试';
        ElNotification({
          message,
          title: '账户已锁定',
          type: 'error',
        });
        return { success: false, userInfo: null };
      }

      const message =
        error?.response?.data?.title ||
        error?.response?.data?.message ||
        error?.message ||
        '登录失败，请检查用户名和密码';
      ElNotification({
        message,
        title: '登录失败',
        type: 'error',
      });
      return { success: false, userInfo: null };
    } finally {
      loginLoading.value = false;
    }

    return { success: true, userInfo };
  }

  async function logout() {
    try {
      await logoutApi();
    } catch {
      // 后端吊销失败也继续清理本地状态
    }
    resetAllStores();
    window.location.href = '/login';
  }

  async function fetchUserInfo() {
    const userInfo = await getUserInfoApi();
    userStore.setUserInfo(userInfo);
    return userInfo;
  }

  function $reset() {
    loginLoading.value = false;
  }

  return {
    $reset,
    authLogin,
    fetchUserInfo,
    loginLoading,
    logout,
  };
});
