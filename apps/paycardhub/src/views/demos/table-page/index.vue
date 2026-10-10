<script lang="ts" setup>
import { computed, ref } from 'vue';

import { Page, TablePage } from '@vben/common-ui';
import type { TablePageColumnDef } from '@vben/common-ui';

import {
  ElButton,
  ElInput,
  ElTag,
} from 'element-plus';

const columns: TablePageColumnDef[] = [
  { field: 'orderNo', label: '订单号', width: 160, fixed: 'left' },
  { field: 'customer', label: '客户', minWidth: 120 },
  { field: 'product', label: '产品', minWidth: 140 },
  { field: 'amount', label: '金额', minWidth: 100, align: 'right' },
  { field: 'status', label: '状态', minWidth: 100, align: 'center' },
  { field: 'createdAt', label: '创建时间', minWidth: 160 },
  { field: 'updatedAt', label: '更新时间', minWidth: 160, defaultVisible: false },
  { field: 'remark', label: '备注', minWidth: 200, defaultVisible: false },
];

const allData = generateMockData(86);

const keyword = ref('');
const page = ref(1);
const pageSize = ref(20);

const filteredData = computed(() => {
  if (!keyword.value) return allData;
  const kw = keyword.value.toLowerCase();
  return allData.filter(
    (r) =>
      r.orderNo.toLowerCase().includes(kw) ||
      r.customer.toLowerCase().includes(kw) ||
      r.product.toLowerCase().includes(kw),
  );
});

const pagedData = computed(() => {
  const start = (page.value - 1) * pageSize.value;
  return filteredData.value.slice(start, start + pageSize.value);
});

function handleRefresh() {
  keyword.value = '';
  page.value = 1;
}

function handleSearch() {
  page.value = 1;
}

function handleReset() {
  keyword.value = '';
  page.value = 1;
}

const statusMap: Record<string, { label: string; type: 'danger' | 'success' | 'warning' }> = {
  completed: { label: '已完成', type: 'success' },
  pending: { label: '待处理', type: 'warning' },
  cancelled: { label: '已取消', type: 'danger' },
};

function generateMockData(count: number) {
  const customers = ['张三', '李四', '王五', '赵六', '孙七', '周八', '吴九', '郑十'];
  const products = ['豪华大床房', '标准双床房', '行政套房', '家庭房', '总统套房'];
  const statuses = ['completed', 'pending', 'cancelled'];
  const data: Record<string, any>[] = [];
  for (let i = 0; i < count; i++) {
    const status = statuses[i % 3]!;
    const date = new Date(2026, 8, 1 + Math.floor(i / 3));
    const updatedDate = new Date(date.getTime() + 86_400_000 * (1 + (i % 5)));
    data.push({
      orderNo: `ORD-2026${String(i + 1).padStart(5, '0')}`,
      customer: customers[i % customers.length]!,
      product: products[i % products.length]!,
      amount: `${(1000 + Math.floor(Math.random() * 9000)).toLocaleString()} 元`,
      status,
      statusLabel: statusMap[status]!.label,
      statusType: statusMap[status]!.type,
      createdAt: date.toLocaleString('zh-CN'),
      updatedAt: updatedDate.toLocaleString('zh-CN'),
      remark: i % 4 === 0 ? '客户特殊要求' : '',
    });
  }
  return data;
}
</script>

<template>
  <Page title="TablePage 组件演示">
    <TablePage
      v-model:page="page"
      v-model:pageSize="pageSize"
      :columns="columns"
      :data="pagedData"
      :total="filteredData.length"
      table-key="demo-table"
      title="订单列表"
      @refresh="handleRefresh"
    >
      <template #toolbar-left>
        <div class="flex items-center gap-2">
          <ElInput
            v-model="keyword"
            clearable
            placeholder="搜索订单号/客户/产品"
            style="width: 220px"
            @keyup.enter="handleSearch"
          />
          <ElButton type="primary" @click="handleSearch">搜索</ElButton>
          <ElButton @click="handleReset">重置</ElButton>
        </div>
      </template>

      <template #column-status="{ row }">
        <ElTag :type="row.statusType" size="small">
          {{ row.statusLabel }}
        </ElTag>
      </template>
    </TablePage>
  </Page>
</template>
