<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElDatePicker,
  ElDialog,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { getProviderAccountsApi, type ProviderAccountListItem } from '#/api/provider-accounts';
import {
  backfillTranHubTransactionsApi,
  type RawTransactionItem,
  queryProviderRawTransactionsApi,
} from '#/api/raw-transactions';

const loading = ref(false);
const syncing = ref(false);
const accounts = ref<ProviderAccountListItem[]>([]);
const providerAccountId = ref<number | undefined>();
const dateRange = ref<[string, string]>(['', '']);
const billType = ref('settle');
const items = ref<RawTransactionItem[]>([]);
const totalCount = ref(0);
const hasMore = ref(false);
const rawJsonDialogVisible = ref(false);
const rawJsonContent = ref('');

const selectedAccount = computed(() =>
  accounts.value.find((a) => a.id === providerAccountId.value),
);
const isTranHub = computed(
  () => selectedAccount.value?.providerCode?.toUpperCase() === 'TRANHUB',
);

const billTypeOptions = [
  { label: '清算账单 (settle)', value: 'settle' },
  { label: '授权账单 (auth)', value: 'auth' },
];

function formatDate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function initDateRange() {
  const today = new Date();
  const end = new Date(today);
  end.setDate(end.getDate() - 1);
  const start = new Date(today);
  start.setDate(start.getDate() - 7);
  dateRange.value = [formatDate(start), formatDate(end)];
}

async function loadAccounts() {
  const res = await getProviderAccountsApi({ page: 1, pageSize: 100 });
  accounts.value = res.items.filter((a) => a.isActive);
}

async function handleQuery() {
  if (!providerAccountId.value || !dateRange.value[0] || !dateRange.value[1]) {
    return;
  }
  loading.value = true;
  try {
    const res = await queryProviderRawTransactionsApi({
      providerAccountId: providerAccountId.value,
      startDate: dateRange.value[0],
      endDate: dateRange.value[1],
      billType: billType.value || undefined,
      maxRecords: 500,
    });
    items.value = res.items;
    totalCount.value = res.totalCount;
    hasMore.value = res.hasMore;
  } finally {
    loading.value = false;
  }
}

async function handleSync() {
  if (!providerAccountId.value || !dateRange.value[0] || !dateRange.value[1]) {
    ElMessage.warning('请先选择供应商账户和日期范围。');
    return;
  }
  if (!isTranHub.value) {
    ElMessage.warning('同步入库仅支持 TranHub 账户，请选择 TranHub 账户。');
    return;
  }
  try {
    await ElMessageBox.confirm(
      `将按定时同步逻辑，把 ${dateRange.value[0]} 至 ${dateRange.value[1]} 的 TranHub 交易（仅 PCH 订单）同步入库，是否继续？`,
      '同步确认',
      { type: 'warning', confirmButtonText: '开始同步', cancelButtonText: '取消' },
    );
  } catch {
    return;
  }
  syncing.value = true;
  try {
    const res = await backfillTranHubTransactionsApi({
      providerAccountId: providerAccountId.value,
      startDate: dateRange.value[0],
      endDate: dateRange.value[1],
      updateWatermark: false,
    });
    if (res.isDisabled) {
      ElMessage.warning('TranHub 交易同步当前未启用（配置 Enabled=false）。');
      return;
    }
    ElMessage.success(
      `同步完成：入库 ${res.syncedEvents} 条，标准化 ${res.normalizedEvents} 条，外部 ${res.externalEvents} 条，失败 ${res.failedEvents} 条。`,
    );
  } finally {
    syncing.value = false;
  }
}

function showRawJson(row: RawTransactionItem) {
  try {
    rawJsonContent.value = JSON.stringify(JSON.parse(row.rawJson || '{}'), null, 2);
  } catch {
    rawJsonContent.value = row.rawJson || '';
  }
  rawJsonDialogVisible.value = true;
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
  initDateRange();
  loadAccounts();
});
</script>

<template>
  <Page title="原始交易查询">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-56">
          <label class="mb-1 block text-xs text-gray-500">供应商账户</label>
          <ElSelect v-model="providerAccountId" placeholder="选择账户" clearable filterable>
            <ElOption
              v-for="a in accounts"
              :key="a.id"
              :label="`[${a.providerCode}] ${a.accountName}`"
              :value="a.id"
            />
          </ElSelect>
        </div>
        <div class="w-[360px] shrink-0">
          <label class="mb-1 block text-xs text-gray-500">日期范围</label>
          <ElDatePicker
            v-model="dateRange"
            type="daterange"
            value-format="YYYY-MM-DD"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            :clearable="false"
            style="width: 100%"
          />
        </div>
        <div class="w-44">
          <label class="mb-1 block text-xs text-gray-500">账单类型</label>
          <ElSelect v-model="billType" placeholder="全部">
            <ElOption
              v-for="opt in billTypeOptions"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </ElSelect>
        </div>
        <ElButton type="primary" :loading="loading" @click="handleQuery">查询</ElButton>
        <ElButton type="success" :loading="syncing" @click="handleSync">同步入库</ElButton>
      </div>
    </ElCard>

    <ElCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span>查询结果</span>
          <span class="text-sm text-gray-500">
            共 {{ totalCount }} 条
            <ElTag v-if="hasMore" type="warning" size="small" class="ml-2">还有更多数据未显示</ElTag>
          </span>
        </div>
      </template>

      <ElTable :data="items" v-loading="loading" stripe max-height="600">
        <ElTableColumn type="index" label="#" width="50" />
        <ElTableColumn prop="providerTransactionId" label="交易ID" width="180" show-overflow-tooltip />
        <ElTableColumn prop="transactionType" label="类型" width="100" />
        <ElTableColumn label="交易金额" width="140">
          <template #default="{ row }">
            {{ row.transactionCurrency }} {{ formatAmount(row.transactionAmount) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="账单金额" width="140">
          <template #default="{ row }">
            {{ row.billingCurrency }} {{ formatAmount(row.billingAmount) }}
          </template>
        </ElTableColumn>
        <ElTableColumn prop="cardNumber" label="卡号" width="160" show-overflow-tooltip />
        <ElTableColumn prop="merchantName" label="商户/订单号" min-width="160" show-overflow-tooltip />
        <ElTableColumn label="交易时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.transactionTime) }}</template>
        </ElTableColumn>
        <ElTableColumn label="清算时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.settleTime) }}</template>
        </ElTableColumn>
        <ElTableColumn prop="status" label="状态" width="100">
          <template #default="{ row }">
            <ElTag v-if="row.status" size="small">{{ row.status }}</ElTag>
            <span v-else>-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="remark" label="备注" width="140" show-overflow-tooltip />
        <ElTableColumn label="操作" width="80" fixed="right">
          <template #default="{ row }">
            <ElButton link type="primary" size="small" @click="showRawJson(row as RawTransactionItem)">JSON</ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <ElDialog v-model="rawJsonDialogVisible" title="原始 JSON" width="700px">
      <ElInput
        type="textarea"
        :model-value="rawJsonContent"
        :rows="20"
        readonly
        class="font-mono"
      />
    </ElDialog>
  </Page>
</template>
