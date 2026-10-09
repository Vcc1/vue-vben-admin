<script lang="ts" setup>
import { computed } from 'vue';

import {
  ElAlert,
  ElButton,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElRadioButton,
  ElRadioGroup,
} from 'element-plus';

import { channelLabel, useStepUpStore } from '#/store';

defineOptions({ name: 'StepUpDialog' });

const stepUpStore = useStepUpStore();

const isPassword = computed(() => stepUpStore.channel === 'password');
const isOtp = computed(() => !isPassword.value);

const title = computed(() =>
  stepUpStore.mode === 'login' ? '新设备登录验证' : '安全验证',
);

const tip = computed(() => {
  if (stepUpStore.mode === 'login') {
    return '检测到这台设备未登录过你的账号，为防止账号被盗用，请完成一次额外验证。';
  }
  return `当前操作需要更高的登录等级（L${stepUpStore.requiredLevel}），请完成额外验证后继续。`;
});

const sendText = computed(() =>
  stepUpStore.countdown > 0
    ? `${stepUpStore.countdown} 秒后可重发`
    : stepUpStore.codeSent
      ? '重新发送验证码'
      : '获取验证码',
);

const submitText = computed(() => (isPassword.value ? '验证并继续' : '提交验证码'));

function onSubmit() {
  stepUpStore.submit();
}
</script>

<template>
  <ElDialog
    v-model="stepUpStore.open"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    :show-close="false"
    :title="title"
    append-to-body
    width="420px"
  >
    <div class="stepup-tip">{{ tip }}</div>

    <ElForm label-position="top" @submit.prevent="onSubmit">
      <ElFormItem
        v-if="stepUpStore.channels.length > 1"
        label="验证方式"
      >
        <ElRadioGroup
          :model-value="stepUpStore.channel"
          @update:model-value="(value: any) => stepUpStore.switchChannel(String(value))"
        >
          <ElRadioButton
            v-for="item in stepUpStore.channels"
            :key="item"
            :value="item"
          >
            {{ channelLabel(item) }}
          </ElRadioButton>
        </ElRadioGroup>
      </ElFormItem>

      <ElFormItem v-if="isOtp && stepUpStore.maskedTarget" label="发送至">
        <span class="stepup-target">{{ stepUpStore.maskedTarget }}</span>
      </ElFormItem>

      <ElFormItem v-if="isPassword" label="登录密码">
        <ElInput
          v-model="stepUpStore.credential"
          autocomplete="current-password"
          placeholder="请输入登录密码"
          show-password
          type="password"
          @keyup.enter="onSubmit"
        />
      </ElFormItem>

      <ElFormItem v-else label="验证码">
        <div class="stepup-code-row">
          <ElInput
            v-model="stepUpStore.credential"
            autocomplete="one-time-code"
            maxlength="8"
            placeholder="请输入验证码"
            @keyup.enter="onSubmit"
          />
          <ElButton
            :disabled="stepUpStore.countdown > 0"
            :loading="stepUpStore.sending"
            @click="stepUpStore.sendCode()"
          >
            {{ sendText }}
          </ElButton>
        </div>
      </ElFormItem>
    </ElForm>

    <ElAlert
      v-if="stepUpStore.errorMessage"
      :closable="false"
      :title="stepUpStore.errorMessage"
      show-icon
      type="error"
    />

    <template #footer>
      <ElButton @click="stepUpStore.cancel()">取消</ElButton>
      <ElButton
        :loading="stepUpStore.verifying"
        :type="isOtp && !stepUpStore.codeSent ? 'default' : 'primary'"
        @click="onSubmit"
      >
        {{ submitText }}
      </ElButton>
    </template>
  </ElDialog>
</template>

<style scoped>
.stepup-tip {
  margin-bottom: 16px;
  font-size: 13px;
  line-height: 1.6;
  color: var(--el-text-color-secondary);
}

.stepup-target {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.stepup-code-row {
  display: flex;
  gap: 8px;
  width: 100%;
}

.stepup-code-row .el-input {
  flex: 1;
}
</style>
