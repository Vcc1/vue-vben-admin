<script setup lang="ts">
import { computed } from 'vue';

import { useUserStore } from '@vben/stores';

interface InfoField {
  label: string;
  value: string;
}

function maskMiddle(text: string, keepStart: number, keepEnd: number): string {
  if (!text) return '-';
  const len = text.length;
  if (len <= keepStart + keepEnd) return text;
  const head = text.substring(0, keepStart);
  const tail = text.substring(len - keepEnd);
  const maskLen = len - keepStart - keepEnd;
  return head + '*'.repeat(maskLen) + tail;
}

function maskEmail(email: string): string {
  if (!email) return '-';
  const at = email.indexOf('@');
  if (at < 0) return maskMiddle(email, 3, 0);
  const local = email.substring(0, at);
  const domain = email.substring(at);
  const visible = local.length <= 3 ? local : local.substring(0, 3);
  return visible + '***' + domain;
}

function maskPhone(phone: string): string {
  if (!phone) return '-';
  return maskMiddle(phone, 3, 3);
}

const userStore = useUserStore();

const fields = computed<InfoField[]>(() => {
  const info = userStore.userInfo as Record<string, any>;
  return [
    { label: '昵称', value: info?.realName || '-' },
    { label: '姓名', value: info?.name || '-' },
    { label: '用户名', value: maskMiddle(info?.username || '-', 2, 2) },
    { label: '邮箱', value: maskEmail(info?.email || '') },
    { label: '手机号', value: maskPhone(info?.phoneNumber || '') },
    { label: '角色', value: (info?.roles as string[])?.join('、') || '-' },
  ];
});
</script>

<template>
  <div class="flex flex-col gap-5 py-4">
    <div v-for="field in fields" :key="field.label" class="flex items-baseline gap-4">
      <span class="w-18 shrink-0 text-right text-sm text-foreground/60">{{ field.label }}</span>
      <span class="text-sm text-foreground/90 break-all">{{ field.value }}</span>
    </div>
  </div>
</template>
