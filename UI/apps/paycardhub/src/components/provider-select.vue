<script lang="ts" setup>
import { ElOption, ElSelect } from 'element-plus';

withDefaults(
  defineProps<{
    clearable?: boolean;
    modelValue?: string;
    width?: string;
  }>(),
  { clearable: true, modelValue: '', width: '160px' },
);

const emit = defineEmits<{
  'update:modelValue': [value: string];
  change: [value: string];
}>();

const options = [
  { label: 'Airwallex', value: 'airwallex' },
  { label: 'TranHub', value: 'tranhub' },
];

function handleUpdate(value: unknown) {
  const next = (value as string) ?? '';
  emit('update:modelValue', next);
  emit('change', next);
}
</script>

<template>
  <ElSelect
    :model-value="modelValue"
    :clearable="clearable"
    :style="{ width }"
    placeholder="全部"
    @update:model-value="handleUpdate"
  >
    <ElOption v-for="item in options" :key="item.value" :label="item.label" :value="item.value" />
  </ElSelect>
</template>
