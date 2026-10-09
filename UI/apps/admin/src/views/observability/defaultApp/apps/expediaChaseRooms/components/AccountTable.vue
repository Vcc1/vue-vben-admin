<script lang="ts" setup>
import type {
  ExpediaAccountTableRow,
  ExpediaAccountTableSortKey,
  ExpediaOverviewFilter,
} from '../types';
import type { TableInstance } from 'element-plus';

import { nextTick, ref, computed, watch } from 'vue';

import { formatDurationMs, formatInteger } from '../formatters';

const props = withDefaults(
  defineProps<{
    focusFilter: ExpediaOverviewFilter;
    loading?: boolean;
    rows: ExpediaAccountTableRow[];
  }>(),
  {
    loading: false,
  },
);

const tableRef = ref<TableInstance>();
const sortState = ref<{
  key: ExpediaAccountTableSortKey;
  order: 'ascending' | 'descending';
}>({
  key: 'error15m',
  order: 'descending',
});

function resolveSortState(filter: ExpediaOverviewFilter): {
  key: ExpediaAccountTableSortKey;
  order: 'ascending' | 'descending';
} {
  if (!filter) {
    return {
      key: 'error15m' as ExpediaAccountTableSortKey,
      order: 'descending' as const,
    };
  }

  if (filter.kind === 'accountMetric') {
    return {
      key: filter.value,
      order: 'descending' as const,
    };
  }

  if (filter.kind === 'taskResult') {
    const taskResultSortMap: Record<'error' | 'no_price' | 'offline' | 'success', ExpediaAccountTableSortKey> = {
      error: 'error15m',
      no_price: 'noPrice15m',
      offline: 'offline15m',
      success: 'success15m',
    };

    return {
      key: taskResultSortMap[filter.value] ?? 'error15m',
      order: 'descending' as const,
    };
  }

  return {
    key: 'error15m' as ExpediaAccountTableSortKey,
    order: 'descending' as const,
  };
}

function applySortState(nextState: {
  key: ExpediaAccountTableSortKey;
  order: 'ascending' | 'descending';
}) {
  sortState.value = nextState;
  nextTick(() => {
    tableRef.value?.sort(nextState.key, nextState.order);
  });
}

watch(
  () => props.focusFilter,
  (value) => {
    applySortState(resolveSortState(value));
  },
  { deep: true, immediate: true },
);

const sortedRows = computed(() => {
  const { key, order } = sortState.value;
  const factor = order === 'ascending' ? 1 : -1;

  return [...props.rows].sort((left, right) => {
    const primaryCompare =
      key === 'accountName'
        ? left.accountName.localeCompare(right.accountName)
        : (Number(left[key]) || 0) - (Number(right[key]) || 0);

    if (primaryCompare !== 0) {
      return primaryCompare * factor;
    }

    const errorCompare = right.error15m - left.error15m;
    if (errorCompare !== 0) {
      return errorCompare;
    }

    const inProgressCompare = right.inProgressTaskCount - left.inProgressTaskCount;
    if (inProgressCompare !== 0) {
      return inProgressCompare;
    }

    return left.accountName.localeCompare(right.accountName);
  });
});

function handleSortChange(sort: {
  order: 'ascending' | 'descending' | null;
  prop: null | string;
}) {
  if (!sort.prop || !sort.order) {
    applySortState(resolveSortState(props.focusFilter));
    return;
  }

  sortState.value = {
    key: sort.prop as ExpediaAccountTableSortKey,
    order: sort.order,
  };
}
</script>

<template>
  <el-table
    ref="tableRef"
    v-loading="props.loading"
    :data="sortedRows"
    border
    empty-text="暂无账号维度数据"
    height="560"
    size="small"
    stripe
    @sort-change="handleSortChange"
  >
    <el-table-column fixed="left" label="account_name" min-width="180" prop="accountName" sortable="custom" />
    <el-table-column label="当前 worker 数" min-width="120" prop="workerCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.workerCount) }}</template>
    </el-table-column>
    <el-table-column label="当前处理中任务数" min-width="140" prop="inProgressTaskCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.inProgressTaskCount) }}</template>
    </el-table-column>
    <el-table-column label="15 分钟 success" min-width="120" prop="success15m" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.success15m) }}</template>
    </el-table-column>
    <el-table-column label="15 分钟 no_price" min-width="130" prop="noPrice15m" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.noPrice15m) }}</template>
    </el-table-column>
    <el-table-column label="15 分钟 offline" min-width="120" prop="offline15m" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.offline15m) }}</template>
    </el-table-column>
    <el-table-column label="15 分钟 error" min-width="120" prop="error15m" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.error15m) }}</template>
    </el-table-column>
    <el-table-column label="query 平均耗时" min-width="150" prop="queryAvgMs" sortable="custom">
      <template #default="{ row }">{{ formatDurationMs(row.queryAvgMs) }}</template>
    </el-table-column>
    <el-table-column label="query P95" min-width="130" prop="queryP95Ms" sortable="custom">
      <template #default="{ row }">{{ formatDurationMs(row.queryP95Ms) }}</template>
    </el-table-column>
    <el-table-column label="persist 平均耗时" min-width="150" prop="persistAvgMs" sortable="custom">
      <template #default="{ row }">{{ formatDurationMs(row.persistAvgMs) }}</template>
    </el-table-column>
    <el-table-column label="login_refresh 次数" min-width="140" prop="loginRefreshCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.loginRefreshCount) }}</template>
    </el-table-column>
    <el-table-column label="shared_sha_refresh 次数" min-width="180" prop="sharedShaRefreshCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.sharedShaRefreshCount) }}</template>
    </el-table-column>
    <el-table-column label="cache_hit_memory 次数" min-width="170" prop="cacheHitMemoryCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.cacheHitMemoryCount) }}</template>
    </el-table-column>
    <el-table-column label="cache_hit_db 次数" min-width="150" prop="cacheHitDbCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.cacheHitDbCount) }}</template>
    </el-table-column>
    <el-table-column label="cache_hit_file 次数" min-width="150" prop="cacheHitFileCount" sortable="custom">
      <template #default="{ row }">{{ formatInteger(row.cacheHitFileCount) }}</template>
    </el-table-column>
  </el-table>
</template>

