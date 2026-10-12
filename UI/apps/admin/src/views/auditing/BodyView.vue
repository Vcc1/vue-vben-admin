<script lang="ts" setup>
import { computed, ref } from 'vue';

import { ElEmpty, ElSegmented, ElTable, ElTableColumn, ElTag } from 'element-plus';

import {
  type BodyFormat,
  detectFormat,
  formatLabel,
  type KeyValue,
  parseKeyValue,
  prettyByFormat,
} from './format';

const props = defineProps<{
  raw: null | string | undefined;
  truncated?: boolean;
}>();

type ViewMode = 'friendly' | 'raw';

const mode = ref<ViewMode>('friendly');

const format = computed<BodyFormat>(() => detectFormat(props.raw));
const isEmpty = computed(
  () => props.raw === null || props.raw === undefined || props.raw.trim() === '',
);
const prettyText = computed(() => prettyByFormat(props.raw, format.value));
const keyValues = computed<KeyValue[]>(() =>
  format.value === 'form' && props.raw ? parseKeyValue(props.raw) : [],
);
</script>

<template>
  <div class="body-view">
    <div v-if="isEmpty" class="text-muted">
      <ElEmpty :image-size="40" description="无内容" />
    </div>
    <template v-else>
      <div class="mb-2 flex items-center gap-2">
        <ElTag size="small" type="info">{{ formatLabel(format) }}</ElTag>
        <ElTag v-if="truncated" size="small" type="warning">已截断</ElTag>
        <div class="ml-auto">
          <ElSegmented
            v-model="mode"
            size="small"
            :options="[
              { label: '友好', value: 'friendly' },
              { label: '原文', value: 'raw' },
            ]"
          />
        </div>
      </div>

      <ElTable
        v-if="mode === 'friendly' && format === 'form'"
        :data="keyValues"
        size="small"
        border
      >
        <ElTableColumn prop="key" label="参数" width="220" />
        <ElTableColumn prop="value" label="值">
          <template #default="{ row }">
            <span class="break-all">{{ row.value }}</span>
          </template>
        </ElTableColumn>
      </ElTable>

      <pre v-else-if="mode === 'friendly'" class="body-pre">{{ prettyText }}</pre>

      <pre v-else class="body-pre">{{ raw }}</pre>
    </template>
  </div>
</template>

<style scoped>
.body-pre {
  max-height: 360px;
  padding: 8px 12px;
  overflow: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
  background-color: rgb(0 0 0 / 4%);
  border-radius: 6px;
}

.text-muted {
  color: var(--el-text-color-secondary);
}
</style>
