import type { UserInfo } from '@vben/types';

import { ref } from 'vue';

import { resetAllStores, useAccessStore, useUserStore } from '@vben/stores';

import { ElNotification } from 'element-plus';
import { defineStore } from 'pinia';

import { getAccessCodesApi, getUserInfoApi } from '#/api';
import { $t } from '#/locales';

const apiURL = import.meta.env.VITE_GLOB_API_URL as string;

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();

  const loginLoading = ref(false);

  function authLogin() {
    window.location.href = `${apiURL}/auth/login`;
  }

  function logout() {
    resetAllStores();
    window.location.href = `${apiURL}/auth/login`;
  }

  async function fetchUserInfo(): Promise<UserInfo> {
    const userInfo = await getUserInfoApi();
    userStore.setUserInfo(userInfo);

    const accessCodes = await getAccessCodesApi();
    accessStore.setAccessCodes(accessCodes);

    if (accessStore.loginExpired) {
      accessStore.setLoginExpired(false);
    }

    if (userInfo?.realName) {
      ElNotification({
        message: `${$t('authentication.loginSuccessDesc')}:${userInfo?.realName}`,
        title: $t('authentication.loginSuccess'),
        type: 'success',
      });
    }

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
