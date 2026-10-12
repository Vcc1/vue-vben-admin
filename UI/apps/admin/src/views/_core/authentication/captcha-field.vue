<script lang="ts" setup>
import { ref, watch } from 'vue';

const props = defineProps<{
  imageBase64?: string;
  loading?: boolean;
  errorMsg?: string;
  onSubmit?: () => void;
}>();

const emit = defineEmits<{
  refresh: [];
  'update:answer': [value: string];
}>();

const localAnswer = ref('');

watch(localAnswer, (v) => emit('update:answer', v));

watch(
  () => props.errorMsg,
  () => {
    localAnswer.value = '';
  },
);

function handleRefresh() {
  localAnswer.value = '';
  emit('refresh');
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter') {
    e.preventDefault();
    props.onSubmit?.();
  }
}
</script>

<template>
  <div class="flex w-full flex-col gap-2">
    <div class="flex items-center gap-2">
      <div
        class="relative h-10 w-[130px] shrink-0 cursor-pointer overflow-hidden rounded-md border border-gray-200 dark:border-gray-700"
        :class="{ 'opacity-50': loading }"
        title="点击刷新验证码"
        @click="handleRefresh"
      >
        <img
          v-if="imageBase64"
          :src="`data:image/png;base64,${imageBase64}`"
          alt="验证码"
          class="h-full w-full object-cover"
        />
        <div
          v-else
          class="flex h-full items-center justify-center text-xs text-gray-400"
        >
          点击获取
        </div>
      </div>
      <input
        v-model="localAnswer"
        type="text"
        placeholder="请输入验证码"
        maxlength="5"
        class="border-input bg-background placeholder:text-muted-foreground/50 focus-visible:outline-none h-10 w-full min-w-0 flex-1 rounded-md border px-3 py-2 text-sm"
        style="--ring: var(--primary)"
        @keydown="handleKeydown"
      />
    </div>
    <div v-if="errorMsg" class="text-xs text-destructive">
      {{ errorMsg }}
    </div>
  </div>
</template>
<style scoped>
input:focus-visible {
  box-shadow: inset 0 0 0 1px hsl(var(--ring));
}
</style>
