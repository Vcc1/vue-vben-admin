<script lang="ts" setup>
import type { VbenFormSchema } from '@vben/common-ui';

import type { Recordable } from '@vben/types';

import { computed, onMounted, ref } from 'vue';

import { AuthenticationLogin, z } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { ElMessage } from 'element-plus';

import {
  checkCaptchaRequiredApi,
  getImageCaptchaApi,
} from '#/api';
import { useAuthStore } from '#/store';

import CaptchaField from './captcha-field.vue';

defineOptions({ name: 'Login' });

const authStore = useAuthStore();

const loginFormRef = ref<InstanceType<typeof AuthenticationLogin>>();
const captchaRequired = ref(false);
const captchaImage = ref('');
const captchaId = ref('');
const captchaLoading = ref(false);
const captchaAnswer = ref('');
const captchaErrorMsg = ref('');

const formSchema = computed((): VbenFormSchema[] => {
  return [
    {
      component: 'VbenInput',
      componentProps: {
        placeholder: $t('authentication.usernameTip'),
      },
      fieldName: 'username',
      label: $t('authentication.username'),
      rules: z.string().min(1, { message: $t('authentication.usernameTip') }),
    },
    {
      component: 'VbenInputPassword',
      componentProps: {
        placeholder: $t('authentication.password'),
      },
      fieldName: 'password',
      label: $t('authentication.password'),
      rules: z.string().min(1, { message: $t('authentication.passwordTip') }),
    },
    {
      component: CaptchaField,
      componentProps: {
        imageBase64: captchaImage.value,
        loading: captchaLoading.value,
        errorMsg: captchaErrorMsg.value,
        'onUpdate:answer': (v: string) => { captchaAnswer.value = v; },
        onRefresh: () => refreshCaptcha(),
        onSubmit: () => {
          loginFormRef.value?.getFormApi()?.submit();
        },
      },
      fieldName: 'captchaAnswer',
      hide: !captchaRequired.value,
      hideLabel: true,
      label: ' ',
    },
  ];
});

async function checkCaptcha() {
  try {
    let userName: string | undefined;
    try {
      userName = await loginFormRef.value?.getFormApi()?.getValues()?.username;
    } catch {
      // form not ready yet
    }
    const resp = await checkCaptchaRequiredApi(userName);
    captchaRequired.value = resp.required;
    if (!resp.required) {
      captchaImage.value = '';
      captchaId.value = '';
      captchaAnswer.value = '';
      captchaErrorMsg.value = '';
    } else {
      await refreshCaptcha();
    }
  } catch {
    // ignore
  }
}

async function refreshCaptcha() {
  captchaLoading.value = true;
  captchaErrorMsg.value = '';
  try {
    const resp = await getImageCaptchaApi();
    captchaImage.value = resp.imageBase64;
    captchaId.value = resp.captchaId;
  } catch {
    // ignore
  } finally {
    captchaLoading.value = false;
  }
}

async function handleSubmit(params: Recordable<any>) {
  if (captchaRequired.value && !captchaAnswer.value.trim()) {
    captchaErrorMsg.value = '请输入验证码';
    return;
  }

  const result = await authStore.authLogin({
    ...params,
    captchaId: captchaRequired.value ? captchaId.value : undefined,
    captchaAnswer: captchaRequired.value ? captchaAnswer.value : undefined,
  });
  if (!result.success) {
    if (captchaRequired.value) {
      captchaAnswer.value = '';
      captchaErrorMsg.value = '验证码错误，请重新输入';
      await refreshCaptcha();
    } else {
      await checkCaptcha();
    }
  }
}

onMounted(() => {
  checkCaptcha();
});

function handleForgetPassword() {
  ElMessage.warning('请联系管理员重置密码');
}
</script>

<template>
  <AuthenticationLogin
    ref="loginFormRef"
    :form-schema="formSchema"
    :loading="authStore.loginLoading"
    :show-code-login="false"
    :show-qrcode-login="false"
    :show-register="false"
    :show-third-party-login="false"
    @forget-password="handleForgetPassword"
    @submit="handleSubmit"
  />
</template>
