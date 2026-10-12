import type { SessionApi } from '#/api/session';

import { ref } from 'vue';

import { useAccessStore } from '@vben/stores';

import { defineStore } from 'pinia';

import {
  sendLoginStepUpCodeApi,
  sendSessionStepUpCodeApi,
  verifyLoginStepUpApi,
  verifySessionStepUpApi,
} from '#/api/session';

export type StepUpMode = 'login' | 'session';

const CHANNEL_LABELS: Record<string, string> = {
  email_otp: '邮箱验证码',
  password: '密码',
  sms: '短信验证码',
  totp: '动态口令',
};

export function channelLabel(channel: string) {
  return CHANNEL_LABELS[channel] ?? channel;
}

function describe(error: any, fallback: string) {
  const data = error?.response?.data;
  return data?.detail || data?.title || error?.message || fallback;
}

/**
 * 二次验证（step-up）的全局状态。
 * 两种入口共用同一个弹窗：
 * - login：登录时设备不受信（HTTP 428），通过后拿到 token 完成登录；
 * - session：已登录但操作要求更高等级（401 stepup_required），通过后原请求自动重放。
 * 两者都返回 Promise，调用方 await 即可，不需要自己管理弹窗开关。
 */
export const useStepUpStore = defineStore('stepup', () => {
  const accessStore = useAccessStore();

  const open = ref(false);
  const mode = ref<StepUpMode>('session');
  const channels = ref<string[]>([]);
  const channel = ref('');
  const credential = ref('');
  const maskedTarget = ref('');
  const requiredLevel = ref(2);
  const reason = ref('');
  const errorMessage = ref('');
  const sending = ref(false);
  const verifying = ref(false);
  const countdown = ref(0);
  const codeSent = ref(false);

  const stepUpToken = ref('');
  let rememberMe = false;
  let timer: null | ReturnType<typeof setInterval> = null;
  let loginResolver: ((tokens: null | SessionApi.Tokens) => void) | null = null;
  let sessionResolver: ((passed: boolean) => void) | null = null;

  function startCountdown(seconds: number) {
    stopCountdown();
    countdown.value = Math.max(0, seconds);
    if (countdown.value === 0) return;
    timer = setInterval(() => {
      countdown.value -= 1;
      if (countdown.value <= 0) stopCountdown();
    }, 1000);
  }

  function stopCountdown() {
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
    countdown.value = 0;
  }

  function reset() {
    stopCountdown();
    credential.value = '';
    errorMessage.value = '';
    codeSent.value = false;
    open.value = false;
  }

  function settleLogin(tokens: null | SessionApi.Tokens) {
    loginResolver?.(tokens);
    loginResolver = null;
    sessionResolver = null;
    reset();
  }

  function settleSession(passed: boolean) {
    sessionResolver?.(passed);
    sessionResolver = null;
    loginResolver = null;
    reset();
  }

  /** 登录 428：返回 token 表示验证通过，null 表示用户放弃或验证失败。 */
  function requestLoginStepUp(
    challenge: SessionApi.StepUpChallenge,
    options: { rememberMe?: boolean } = {},
  ): Promise<null | SessionApi.Tokens> {
    mode.value = 'login';
    stepUpToken.value = challenge.stepUpToken;
    channels.value = [...challenge.channels];
    channel.value = challenge.channels[0] ?? 'password';
    maskedTarget.value = challenge.maskedTarget ?? '';
    reason.value = challenge.reason;
    requiredLevel.value = 3;
    rememberMe = options.rememberMe ?? false;
    errorMessage.value = '';
    credential.value = '';
    codeSent.value = false;
    open.value = true;

    return new Promise((resolve) => {
      loginResolver = resolve;
      sessionResolver = null;
    });
  }

  /** 会话内提权：返回是否通过，通过后调用方应重放原请求。 */
  function requestInSessionStepUp(
    required: null | SessionApi.StepUpRequired = null,
  ): Promise<boolean> {
    mode.value = 'session';
    stepUpToken.value = '';
    requiredLevel.value = required?.requiredLevel ?? 3;
    channels.value = required?.channels?.length
      ? [...required.channels]
      : requiredLevel.value >= 3
        ? ['email_otp']
        : ['email_otp', 'password'];
    channel.value = channels.value[0] ?? 'email_otp';
    maskedTarget.value = '';
    reason.value = 'stepup_required';
    errorMessage.value = '';
    credential.value = '';
    codeSent.value = false;
    open.value = true;

    return new Promise((resolve) => {
      sessionResolver = resolve;
      loginResolver = null;
    });
  }

  function switchChannel(value: string) {
    channel.value = value;
    credential.value = '';
    errorMessage.value = '';
    codeSent.value = false;
  }

  async function sendCode() {
    if (sending.value || countdown.value > 0) return;
    sending.value = true;
    errorMessage.value = '';
    try {
      if (mode.value === 'login') {
        await sendLoginStepUpCodeApi(stepUpToken.value, channel.value);
      } else {
        const challenge = await sendSessionStepUpCodeApi(channel.value);
        if (challenge?.maskedTarget) {
          maskedTarget.value = challenge.maskedTarget;
        }
      }
      codeSent.value = true;
      startCountdown(60);
    } catch (error: any) {
      errorMessage.value = describe(error, '验证码发送失败，请稍后重试。');
    } finally {
      sending.value = false;
    }
  }

  async function submit() {
    if (verifying.value) return;
    if (!credential.value) {
      errorMessage.value =
        channel.value === 'password' ? '请输入密码。' : '请输入验证码。';
      return;
    }

    verifying.value = true;
    errorMessage.value = '';
    try {
      if (mode.value === 'login') {
        const result = await verifyLoginStepUpApi({
          channel: channel.value,
          credential: credential.value,
          mode: 'token',
          rememberMe,
          stepUpToken: stepUpToken.value,
        });
        settleLogin(result?.tokens ?? null);
        return;
      }

      const result = await verifySessionStepUpApi({
        channel: channel.value,
        credential: credential.value,
      });
      // 提权成功后服务端会重发 token，必须覆盖本地凭据，否则下一个请求仍带着旧的等级快照。
      if (result?.tokens?.accessToken) {
        accessStore.setAccessToken(result.tokens.accessToken);
      }
      if (result?.tokens?.refreshToken) {
        accessStore.setRefreshToken(result.tokens.refreshToken);
      }
      settleSession(true);
    } catch (error: any) {
      errorMessage.value = describe(error, '验证失败，请重试。');
    } finally {
      verifying.value = false;
    }
  }

  function cancel() {
    if (mode.value === 'login') {
      settleLogin(null);
    } else {
      settleSession(false);
    }
  }

  return {
    cancel,
    channel,
    channels,
    codeSent,
    countdown,
    credential,
    errorMessage,
    mode,
    open,
    maskedTarget,
    reason,
    requiredLevel,
    requestInSessionStepUp,
    requestLoginStepUp,
    sendCode,
    sending,
    submit,
    switchChannel,
    verifying,
  };
});
