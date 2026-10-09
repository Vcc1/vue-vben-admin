<script lang="ts" setup>
import { computed } from 'vue';

import { ElTag, ElTooltip } from 'element-plus';

export interface EnumLabelEntry {
  label: string;
  description?: string;
  type?: 'danger' | 'info' | 'primary' | 'success' | 'warning';
}

const props = defineProps<{
  map: Record<string, EnumLabelEntry>;
  value?: null | string;
}>();

const raw = computed(() => (props.value ?? '').toString());

const entry = computed(() => {
  const key = raw.value;
  if (!key) return undefined;
  const direct = props.map[key];
  if (direct) return direct;
  const upper = props.map[key.toUpperCase()];
  return upper;
});

const label = computed(() => entry.value?.label || raw.value || '-');
const tagType = computed(() => entry.value?.type ?? 'info');
const tooltip = computed(() => {
  if (!entry.value) return raw.value;
  return entry.value.description ? `${raw.value}：${entry.value.description}` : raw.value;
});
const hasTooltip = computed(() => Boolean(raw.value));
</script>

<template>
  <ElTooltip v-if="hasTooltip" :content="tooltip" placement="top">
    <ElTag :type="tagType" size="small">{{ label }}</ElTag>
  </ElTooltip>
  <span v-else>-</span>
</template>
