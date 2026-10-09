<script lang="ts" setup>
import type { ExpediaLatencyChartData } from '../types';

import TrendChartPanel from './TrendChartPanel.vue';

const props = withDefaults(
  defineProps<{
    charts: ExpediaLatencyChartData[];
    loading?: boolean;
  }>(),
  {
    loading: false,
  },
);
</script>

<template>
  <div class="latency-shell">
    <div class="latency-note">已将 histogram bucket 的 le 转为 P50、P95 和平均耗时，业务侧不直接展示原始 bucket。</div>
    <div class="latency-grid">
      <el-card v-for="chart in props.charts" :key="chart.key" shadow="never">
        <TrendChartPanel
          :loading="props.loading"
          :series="chart.series"
          :title="chart.label"
          value-type="duration"
        />
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.latency-note {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  margin-bottom: 12px;
}

.latency-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

@media (max-width: 1280px) {
  .latency-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
