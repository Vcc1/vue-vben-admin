<script lang="ts" setup>
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElInput,
  ElPagination,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { type ApiClientListItem, getClientsApi } from '#/api/api-clients';

const router = useRouter();
const loading = ref(false);
const clients = ref<ApiClientListItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');

async function loadData() {
  loading.value = true;
  try {
    const res = await getClientsApi({
      keyword: keyword.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });
    clients.value = res.items;
    total.value = res.totalCount;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  loadData();
}

function handleReset() {
  keyword.value = '';
  handleSearch();
}

function handlePageChange(val: number) {
  page.value = val;
  loadData();
}

function handleSizeChange(val: number) {
  pageSize.value = val;
  page.value = 1;
  loadData();
}

function viewDetail(item: ApiClientListItem) {
  router.push({ name: 'ApiClientDetail', params: { id: item.id } });
}

function formatDateTime(val: string) {
  return new Date(val).toLocaleString('zh-CN');
}

onMounted(loadData);
</script>

<template>
  <Page title="API 客户端">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-56">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput v-model="keyword" placeholder="客户端代码 / 名称" clearable @keyup.enter="handleSearch" />
        </div>
        <ElButton type="primary" @click="handleSearch">查询</ElButton>
        <ElButton @click="handleReset">重置</ElButton>
      </div>
    </ElCard>

    <ElCard>
      <ElTable
        :data="clients"
        v-loading="loading"
        stripe
        row-class-name="cursor-pointer"
        @row-click="viewDetail"
      >
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="clientCode" label="客户端代码" width="150" />
        <ElTableColumn prop="displayName" label="显示名称" min-width="180" />
        <ElTableColumn label="启用" width="80" align="center">
          <template #default="{ row }">
            <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '是' : '否' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="API Key 数" width="100" align="center">
          <template #default="{ row }">{{ row.apiKeyCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="订单数" width="90" align="center">
          <template #default="{ row }">{{ row.orderCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="卡片数" width="90" align="center">
          <template #default="{ row }">{{ row.cardCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="创建时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </ElTableColumn>
      </ElTable>

      <div class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          @current-change="handlePageChange"
          @size-change="handleSizeChange"
        />
      </div>
    </ElCard>
  </Page>
</template>
