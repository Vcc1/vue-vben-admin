<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccessStore } from '@vben/stores';

import { useAuthStore } from '#/store';

const router = useRouter();
const authStore = useAuthStore();
const accessStore = useAccessStore();

const errorMessage = ref<string>('');
const isRetrying = ref(false);

onMounted(async () => {
  const params = new URLSearchParams(window.location.search);
  const error = params.get('error');

  if (error) {
    errorMessage.value = `认证失败: ${error}`;
    return;
  }

  const token = params.get('token');
  const refreshToken = params.get('refreshToken');
  const returnUrl = params.get('returnUrl');

  if (!token) {
    errorMessage.value = '缺少访问令牌';
    return;
  }

  accessStore.setAccessToken(token);
  if (refreshToken) {
    accessStore.setRefreshToken(refreshToken);
  }

  window.history.replaceState({}, '', '/auth/callback');

  try {
    const userInfo = await authStore.fetchUserInfo();
    const target = returnUrl || userInfo.homePath || '/';
    await router.replace(target);
  } catch (err) {
    // 获取用户信息失败，显示错误而非自动重试
    accessStore.setAccessToken(null);
    accessStore.setRefreshToken(null);
    errorMessage.value = '获取用户信息失败，请重试';
  }
});

async function retryLogin() {
  if (isRetrying.value) return;
  isRetrying.value = true;
  errorMessage.value = '';
  await authStore.authLogin();
}
</script>

<template>
  <div class="flex h-full flex-col items-center justify-center gap-4">
    <template v-if="errorMessage">
      <p class="text-lg font-medium text-red-500">{{ errorMessage }}</p>
      <button
        class="rounded bg-blue-500 px-4 py-2 text-white hover:bg-blue-600"
        :disabled="isRetrying"
        @click="retryLogin"
      >
        {{ isRetrying ? '跳转中...' : '重新登录' }}
      </button>
    </template>
    <template v-else>
      <p class="text-lg font-medium">正在登录...</p>
    </template>
  </div>
</template>
