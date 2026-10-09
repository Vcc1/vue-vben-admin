<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElInput,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { type NormalizedTransactionListItem, getNormalizedTransactionsApi } from '#/api/transactions';
import { getClientOptionsApi, type ApiClientOption } from '#/api/api-clients';
import EnumLabel from '#/components/enum-label.vue';
import ProviderSelect from '#/components/provider-select.vue';
import { AIRWALLEX_STATUS_LABELS } from '#/utils/transaction-labels';

const loading = ref(false);
const items = ref<NormalizedTransactionListItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');
const providerCode = ref('');
const apiClientCode = ref('');
const apiClients = ref<ApiClientOption[]>([]);

async function loadData() {
  loading.value = true;
  try {
    const res = await getNormalizedTransactionsApi({
      keyword: keyword.value || undefined,
      providerCode: providerCode.value || undefined,
      apiClientCode: apiClientCode.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });
    items.value = res.items;
    total.value = res.totalCount;
  } finally {
    loading.value = false;
  }
}

async function loadOptions() {
  apiClients.value = await getClientOptionsApi();
}

function handleSearch() {
  page.value = 1;
  loadData();
}

function handleReset() {
  keyword.value = '';
  providerCode.value = '';
  apiClientCode.value = '';
  handleSearch();
}

function formatDateTime(val: string) {
  return new Date(val).toLocaleString('zh-CN');
}

function formatAmount(amount?: number) {
  if (amount === undefined || amount === null) return '-';
  return amount.toFixed(2);
}

onMounted(() => {
  loadOptions();
  loadData();
});
</script>

<template>
  <Page title="标准化交易">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-52">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput v-model="keyword" placeholder="交易号 / 卡 ID" clearable @keyup.enter="handleSearch" />
        </div>
        <div class="w-40">
          <label class="mb-1 block text-xs text-gray-500">供应商</label>
          <ProviderSelect v-model="providerCode" />
        </div>
        <div class="w-44">
          <label class="mb-1 block text-xs text-gray-500">API 客户端</label>
          <ElSelect v-model="apiClientCode" clearable placeholder="全部">
            <ElOption v-for="c in apiClients" :key="c.id" :label="c.displayName" :value="c.clientCode" />
          </ElSelect>
        </div>
        <ElButton type="primary" @click="handleSearch">查询</ElButton>
        <ElButton @click="handleReset">重置</ElButton>
      </div>
    </ElCard>

    <ElCard>
      <ElTable :data="items" v-loading="loading" stripe>
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="transactionNo" label="交易号" width="170" show-overflow-tooltip />
        <ElTableColumn prop="providerAccountName" label="账户" width="140" show-overflow-tooltip />
        <ElTableColumn prop="maskedCardNumber" label="卡号" width="150" />
        <ElTableColumn prop="status" label="状态" width="110">
          <template #default="{ row }">
            <EnumLabel :map="AIRWALLEX_STATUS_LABELS" :value="row.status" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="终态" width="80" align="center">
          <template #default="{ row }">
            <ElTag :type="row.isTerminal ? 'success' : 'info'" size="small">
              {{ row.isTerminal ? '是' : '否' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="交易金额" width="130">
          <template #default="{ row }">{{ row.transactionCurrency }} {{ formatAmount(row.transactionAmount) }}</template>
        </ElTableColumn>
        <ElTableColumn label="账单金额" width="130">
          <template #default="{ row }">{{ row.billingCurrency }} {{ formatAmount(row.billingAmount) }}</template>
        </ElTableColumn>
        <ElTableColumn prop="apiClientName" label="API 客户端" width="130" show-overflow-tooltip />
        <ElTableColumn label="交易时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.transactionTime) }}</template>
        </ElTableColumn>
        <ElTableColumn label="事件数" width="80" align="center">
          <template #default="{ row }">{{ row.eventCount }}</template>
        </ElTableColumn>
      </ElTable>

      <div class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          @current-change="() => loadData()"
          @size-change="() => { page = 1; loadData(); }"
        />
      </div>
    </ElCard>
  </Page>
</template>
