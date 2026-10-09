<script lang="ts" setup>
import { onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

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

import { type UnlinkedTransactionItem, getUnlinkedTransactionsApi } from '#/api/transactions';
import EnumLabel from '#/components/enum-label.vue';
import { AIRWALLEX_STATUS_LABELS, DIRECTION_LABELS, TRANHUB_STATUS_LABELS } from '#/utils/transaction-labels';

const route = useRoute();
const loading = ref(false);
const items = ref<UnlinkedTransactionItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');
const provider = ref((route.meta.providerCode as string) || 'airwallex');

async function loadData() {
  loading.value = true;
  try {
    const res = await getUnlinkedTransactionsApi({
      keyword: keyword.value || undefined,
      providerCode: provider.value,
      page: page.value,
      pageSize: pageSize.value,
    });
    items.value = res.items;
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

watch(
  () => route.meta.providerCode,
  (val) => {
    if (typeof val === 'string' && val && val !== provider.value) {
      provider.value = val;
      handleSearch();
    }
  },
);

function formatDateTime(val: string) {
  return new Date(val).toLocaleString('zh-CN');
}

function formatAmount(amount?: number) {
  if (amount === undefined || amount === null) return '-';
  return amount.toFixed(2);
}

function linkStateType(state: string) {
  if (state === 'Linked') return 'success';
  if (state === 'Unlinked') return 'danger';
  return 'warning';
}

onMounted(loadData);
</script>

<template>
  <Page title="未关联交易">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-52">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput v-model="keyword" placeholder="商户 / 卡 ID / 订单号" clearable @keyup.enter="handleSearch" />
        </div>
        <ElButton type="primary" @click="handleSearch">查询</ElButton>
        <ElButton @click="handleReset">重置</ElButton>
      </div>
    </ElCard>

    <ElCard v-if="provider === 'airwallex'">
      <ElTable :data="items" v-loading="loading" stripe>
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="providerCardId" label="供应商卡 ID" width="180" show-overflow-tooltip />
        <ElTableColumn prop="merchantName" label="商户" min-width="160" show-overflow-tooltip />
        <ElTableColumn prop="transactionType" label="类型" width="100" />
        <ElTableColumn label="方向" width="90">
          <template #default="{ row }">
            <EnumLabel :map="DIRECTION_LABELS" :value="row.direction" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="交易金额" width="130">
          <template #default="{ row }">{{ row.transactionCurrency }} {{ formatAmount(row.transactionAmount) }}</template>
        </ElTableColumn>
        <ElTableColumn label="账单金额" width="130">
          <template #default="{ row }">{{ row.billingCurrency }} {{ formatAmount(row.billingAmount) }}</template>
        </ElTableColumn>
        <ElTableColumn prop="status" label="状态" width="100">
          <template #default="{ row }">
            <EnumLabel :map="AIRWALLEX_STATUS_LABELS" :value="row.status" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="关联状态" width="100">
          <template #default="{ row }">
            <ElTag :type="linkStateType(row.linkState)" size="small">{{ row.linkState }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="虚拟卡 ID" width="100">
          <template #default="{ row }">
            <span v-if="row.virtualCardId">{{ row.virtualCardId }}</span>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="供应商时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.providerCreatedAt) }}</template>
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

    <ElCard v-else>
      <ElTable :data="items" v-loading="loading" stripe>
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="providerCardTransactionId" label="账单号" width="180" show-overflow-tooltip />
        <ElTableColumn label="商户订单号" width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.merTradeNo">{{ row.merTradeNo }}</span>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="providerCardId" label="供应商卡 ID" width="180" show-overflow-tooltip />
        <ElTableColumn prop="transactionType" label="类型" width="110" />
        <ElTableColumn label="方向" width="90">
          <template #default="{ row }">
            <EnumLabel :map="DIRECTION_LABELS" :value="row.direction" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="交易金额" width="130">
          <template #default="{ row }">{{ row.transactionCurrency }} {{ formatAmount(row.transactionAmount) }}</template>
        </ElTableColumn>
        <ElTableColumn label="账单金额" width="130">
          <template #default="{ row }">{{ row.billingCurrency }} {{ formatAmount(row.billingAmount) }}</template>
        </ElTableColumn>
        <ElTableColumn prop="status" label="归一状态" width="110">
          <template #default="{ row }">
            <EnumLabel :map="TRANHUB_STATUS_LABELS" :value="row.status" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="供应商时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.providerCreatedAt) }}</template>
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
