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
} from 'element-plus';

import { getClientOptionsApi, type ApiClientOption } from '#/api/api-clients';
import {
  getTranHubTransactionsApi,
  type TranHubTransactionListItem,
} from '#/api/transactions';
import EnumLabel from '#/components/enum-label.vue';
import {
  DIRECTION_LABELS,
  TRANHUB_STATUS_LABELS,
  TRANHUB_TRANTYPE_LABELS,
} from '#/utils/transaction-labels';

const loading = ref(false);
const items = ref<TranHubTransactionListItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');
const status = ref('');
const apiClientCode = ref('');
const apiClients = ref<ApiClientOption[]>([]);

async function loadData() {
  loading.value = true;
  try {
    const res = await getTranHubTransactionsApi({
      keyword: keyword.value || undefined,
      status: status.value || undefined,
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
  status.value = '';
  apiClientCode.value = '';
  handleSearch();
}

function formatDateTime(val?: string) {
  if (!val) return '-';
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
  <Page title="TranHub 交易">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-52">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput
            v-model="keyword"
            placeholder="账单号 / 商户单号 / 卡订单号"
            clearable
            @keyup.enter="handleSearch"
          />
        </div>
        <div class="w-36">
          <label class="mb-1 block text-xs text-gray-500">状态</label>
          <ElInput v-model="status" placeholder="如 Normalized" clearable />
        </div>
        <div class="w-44">
          <label class="mb-1 block text-xs text-gray-500">API 客户端</label>
          <ElSelect v-model="apiClientCode" clearable placeholder="全部">
            <ElOption
              v-for="c in apiClients"
              :key="c.id"
              :label="c.displayName"
              :value="c.clientCode"
            />
          </ElSelect>
        </div>
        <ElButton type="primary" @click="handleSearch">查询</ElButton>
        <ElButton @click="handleReset">重置</ElButton>
      </div>
    </ElCard>

    <ElCard>
      <ElTable :data="items" v-loading="loading" stripe>
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="providerAccountName" label="账户" width="140" show-overflow-tooltip />
        <ElTableColumn prop="billNo" label="账单号" width="170" show-overflow-tooltip />
        <ElTableColumn prop="merTradeNo" label="商户单号" width="160" show-overflow-tooltip />
        <ElTableColumn prop="cardOrderNo" label="卡订单号" width="150" show-overflow-tooltip />
        <ElTableColumn prop="cardNo" label="卡号" width="150" show-overflow-tooltip />
        <ElTableColumn label="交易类型" width="100">
          <template #default="{ row }">
            <EnumLabel :map="TRANHUB_TRANTYPE_LABELS" :value="row.tranType" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="方向" width="90">
          <template #default="{ row }">
            <EnumLabel :map="DIRECTION_LABELS" :value="row.direction" />
          </template>
        </ElTableColumn>
        <ElTableColumn label="交易金额" width="130">
          <template #default="{ row }">
            {{ row.saleCurrency }} {{ formatAmount(row.saleAmount) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="账单金额" width="130">
          <template #default="{ row }">
            {{ row.billCurrency }} {{ formatAmount(row.billAmount) }}
          </template>
        </ElTableColumn>
        <ElTableColumn prop="bankProcessBillType" label="银行处理类型" width="130" show-overflow-tooltip />
        <ElTableColumn label="状态" width="110">
          <template #default="{ row }">
            <EnumLabel :map="TRANHUB_STATUS_LABELS" :value="row.status" />
          </template>
        </ElTableColumn>
        <ElTableColumn prop="apiClientName" label="API 客户端" width="130" show-overflow-tooltip />
        <ElTableColumn prop="remark" label="备注" min-width="140" show-overflow-tooltip />
        <ElTableColumn label="结算时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.settleDateTime) }}</template>
        </ElTableColumn>
        <ElTableColumn label="交易时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.tranDateTime) }}</template>
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
          @size-change="
            () => {
              page = 1;
              loadData();
            }
          "
        />
      </div>
    </ElCard>
  </Page>
</template>
