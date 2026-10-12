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

import { type CardholderListItem, getCardholdersApi } from '#/api/cardholders';

const router = useRouter();
const loading = ref(false);
const cardholders = ref<CardholderListItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');

async function loadData() {
  loading.value = true;
  try {
    const res = await getCardholdersApi({
      keyword: keyword.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });
    cardholders.value = res.items;
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

function viewDetail(item: CardholderListItem) {
  router.push({ name: 'CardholderDetail', params: { id: item.id } });
}

function formatDateTime(val: string) {
  return new Date(val).toLocaleString('zh-CN');
}

onMounted(loadData);
</script>

<template>
  <Page title="持卡人管理">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-64">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput
            v-model="keyword"
            placeholder="姓名 / 邮箱 / 手机号"
            clearable
            @keyup.enter="handleSearch"
          />
        </div>
        <ElButton type="primary" @click="handleSearch">查询</ElButton>
        <ElButton @click="handleReset">重置</ElButton>
      </div>
    </ElCard>

    <ElCard>
      <ElTable
        :data="cardholders"
        v-loading="loading"
        stripe
        row-class-name="cursor-pointer"
        @row-click="viewDetail"
      >
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="displayName" label="显示名称" width="180" />
        <ElTableColumn prop="email" label="邮箱" width="200" show-overflow-tooltip />
        <ElTableColumn prop="mobileNumber" label="手机号" width="140" />
        <ElTableColumn prop="type" label="类型" width="100">
          <template #default="{ row }">
            <ElTag size="small">{{ row.type }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="status" label="状态" width="100">
          <template #default="{ row }">
            <ElTag :type="row.status?.toUpperCase() === 'ACTIVE' ? 'success' : 'info'" size="small">{{ row.status }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="nationality" label="国籍" width="90" />
        <ElTableColumn label="供应商映射" width="100" align="center">
          <template #default="{ row }">{{ row.providerMappingCount }}</template>
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
