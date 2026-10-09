<script lang="ts" setup>
import { computed, ref } from 'vue';

import NetRuntimePanel from './NetRuntimePanel.vue';
import type { AppViewModel, RuntimeRange } from './types';
import { getRuntimeQueryTime, getRuntimeTargetLabels } from './appPanelUtils';

const props = defineProps<{
  app: AppViewModel | null;
}>();

const runtimeRange = ref<RuntimeRange>('15m');

const runtimeQueryTime = computed(() => getRuntimeQueryTime(props.app));
const runtimeTargetLabels = computed(() => getRuntimeTargetLabels(props.app));
</script>

<template>
  <el-card shadow="never">
    <template #header>
      <div class="runtime-header">
        <span>.NET Runtime / Process</span>
        <el-radio-group v-model="runtimeRange" size="small">
          <el-radio-button label="15m">15m</el-radio-button>
          <el-radio-button label="1h">1h</el-radio-button>
          <el-radio-button label="6h">6h</el-radio-button>
          <el-radio-button label="24h">24h</el-radio-button>
        </el-radio-group>
      </div>
    </template>
    <NetRuntimePanel
      :query-time="runtimeQueryTime"
      :target-labels="runtimeTargetLabels"
      :time-range="runtimeRange"
    />
  </el-card>
</template>

<style scoped>
.runtime-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  width: 100%;
}
</style>
