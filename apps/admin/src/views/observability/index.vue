<script lang="ts" setup>
import { ref } from 'vue';

import DefaultAppPanel from './defaultApp/DefaultAppPanel.vue';
import DefaultServerPanel from './defaultServer/DefaultServerPanel.vue';

defineOptions({ name: 'ObservabilityMetricsView' });

const activeTab = ref<'app' | 'resource'>('resource');
</script>

<template>
  <div class="observability-tabs">
    <el-tabs v-model="activeTab" class="tabs-shell">
      <el-tab-pane label="服务器监测" name="resource">
        <DefaultServerPanel />
      </el-tab-pane>

      <el-tab-pane label="应用监测" name="app">
        <DefaultAppPanel />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.observability-tabs {
  height: calc(100vh - 110px);
}

.tabs-shell {
  height: 100%;
}

.tabs-shell:deep(.el-tabs__header) {
  padding-left: 12px;
  padding-right: 12px;
}

.tabs-shell:deep(.el-tabs__content) {
  height: calc(100% - 44px);
}

.tabs-shell:deep(.el-tab-pane) {
  height: 100%;
}
</style>
