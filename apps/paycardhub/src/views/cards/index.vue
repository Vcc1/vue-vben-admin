<script lang="ts" setup>
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import {
  TablePage,
  TableSearch,
  TableSearchItem,
  useTablePage,
  type TablePageColumnDef,
} from '@vben/element-plus-ui';

import {
  ElInput,
  ElOption,
  ElPopover,
  ElSelect,
  ElTag,
  ElTooltip,
} from 'element-plus';

import {
  type CardListItem,
  type CardUsageAmount,
  type HotelOrder,
  type ProviderAccountOption,
  getCardsApi,
  getProviderAccountOptionsApi,
} from '#/api/cards';
import { getClientOptionsApi, type ApiClientOption } from '#/api/api-clients';
import ProviderSelect from '#/components/provider-select.vue';

const router = useRouter();
const keyword = ref('');
const providerCode = ref('');
const providerAccountId = ref<number | undefined>(undefined);
const apiClientCode = ref('');

const providerAccounts = ref<ProviderAccountOption[]>([]);
const apiClients = ref<ApiClientOption[]>([]);

const columns: TablePageColumnDef<CardListItem>[] = [
  { field: 'orderNo', label: '单号', minWidth: 245 },
  { field: 'hotelOrder', label: '订单信息', minWidth: 170 },
  { field: 'apiClient', label: 'API 客户端', minWidth: 160 },
  { field: 'providerAccount', label: '供应商账户', minWidth: 160 },
  { field: 'limitAmount', label: '额度 / 已使用', minWidth: 230 },
  {
    field: 'expectedBusinessStatus',
    label: '期望状态',
    width: 100,
    align: 'center',
  },
  {
    field: 'transactionHealth',
    label: '交易状态',
    width: 100,
    align: 'center',
  },
  { field: 'transactionCount', label: '交易', width: 80, align: 'center' },
  { field: 'latestTransactionAt', label: '最近交易 / 观测', minWidth: 180 },
  { field: 'expireAt', label: '有效期', width: 110, align: 'center' },
];

const table = useTablePage<CardListItem>({
  request: async ({ page, pageSize }) => {
    const res = await getCardsApi({
      keyword: keyword.value || undefined,
      providerCode: providerCode.value || undefined,
      providerAccountId: providerAccountId.value,
      apiClientCode: apiClientCode.value || undefined,
      page,
      pageSize,
    });
    return { items: res.items, total: res.totalCount };
  },
  onReset: () => {
    keyword.value = '';
    providerCode.value = '';
    providerAccountId.value = undefined;
    apiClientCode.value = '';
  },
});
const { loading } = table;

async function loadOptions() {
  const [accounts, clients] = await Promise.all([
    getProviderAccountOptionsApi(),
    getClientOptionsApi(),
  ]);
  providerAccounts.value = accounts;
  apiClients.value = clients;
}

function handleSearch() {
  void table.search().catch(() => {});
}

function handleReset() {
  void table.reset().catch(() => {});
}

function viewDetail(row: CardListItem) {
  router.push({ name: 'CardDetail', params: { id: row.id } });
}

function formatAmount(value?: number) {
  const n = Number(value);
  if (!Number.isFinite(n)) return '-';
  return new Intl.NumberFormat('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 3,
  }).format(n);
}

function formatDateTime(val?: string) {
  if (!val) return '-';
  const d = new Date(val);
  if (Number.isNaN(d.getTime())) return '-';
  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
    .format(d)
    .replaceAll('/', '-');
}

function formatDateOnly(val?: string) {
  if (!val) return '';
  const d = new Date(val);
  if (Number.isNaN(d.getTime())) return val.slice(0, 10);
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
}

function isExpired(val?: string) {
  if (!val) return false;
  const d = new Date(val);
  return Number.isNaN(d.getTime()) ? false : d.getTime() < Date.now();
}

function stayRange(h?: HotelOrder) {
  if (!h) return '';
  const ci = formatDateOnly(h.checkIn);
  const co = formatDateOnly(h.checkOut);
  if (ci && co) return `${ci} ~ ${co}`;
  return ci || co || '';
}

function expectedStatus(status: number) {
  const n = Number(status);
  if (n === 1) return { label: '期望成功', type: 'success' as const };
  if (n === 2) return { label: '期望失败', type: 'danger' as const };
  return { label: '等待', type: 'warning' as const };
}

function transactionHealth(card: CardListItem) {
  const expected = Number(card.expectedBusinessStatus);
  const used = (card.usageAmounts || []).map((u) => Number(u.usedAmount) || 0);
  const isAbnormal =
    expected === 1
      ? used.every((a) => a === 0)
      : expected === 2 && used.some((a) => a > 0);
  return {
    label: isAbnormal ? '异常' : '正常',
    type: isAbnormal ? ('danger' as const) : ('success' as const),
    reason: isAbnormal
      ? expected === 1
        ? '期望成功，但所有币种的已使用金额均为 0'
        : '期望失败，但存在大于 0 的已使用金额'
      : '已使用金额符合期望状态',
  };
}

function activeUsages(card: CardListItem) {
  return (card.usageAmounts || []).filter(
    (u) =>
      Number(u.settledAmount) !== 0 ||
      Number(u.pendingAmount) !== 0 ||
      Number(u.usedAmount) !== 0,
  );
}

function usageLabel(u: CardUsageAmount) {
  return Number(u.usedAmount) < 0 ? '净入账' : '已用';
}

onMounted(() => {
  loadOptions();
});
</script>

<template>
  <Page title="卡管理">
    <TableSearch
      class="mb-4"
      :loading="loading"
      @search="handleSearch"
      @reset="handleReset"
    >
      <TableSearchItem v-slot="{ id }" label="关键词">
        <ElInput
          :id="id"
          v-model="keyword"
          placeholder="订单号 / 卡 ID / 卡号"
          clearable
        />
      </TableSearchItem>
      <TableSearchItem v-slot="{ id }" label="供应商">
        <ProviderSelect :id="id" v-model="providerCode" width="100%" />
      </TableSearchItem>
      <TableSearchItem v-slot="{ id }" label="供应商账户">
        <ElSelect
          :id="id"
          v-model="providerAccountId"
          clearable
          placeholder="全部"
        >
          <ElOption
            v-for="acc in providerAccounts"
            :key="acc.id"
            :label="`${acc.providerCode} - ${acc.accountName}`"
            :value="acc.id"
          />
        </ElSelect>
      </TableSearchItem>
      <TableSearchItem v-slot="{ id }" label="API 客户端">
        <ElSelect :id="id" v-model="apiClientCode" clearable placeholder="全部">
          <ElOption
            v-for="c in apiClients"
            :key="c.id"
            :label="c.displayName"
            :value="c.clientCode"
          />
        </ElSelect>
      </TableSearchItem>
    </TableSearch>

    <TablePage
      v-bind="table.bindings"
      :columns="columns"
      row-key="id"
      row-class-name="cursor-pointer"
      table-key="paycardhub-cards"
      title="卡列表"
      @row-click="viewDetail"
    >
      <template #column-orderNo="{ row }">
        <div class="cell-stack">
          <span class="cell-title">{{ row.orderNo || '-' }}</span>
          <span class="cell-subtitle"
            >业务单号: {{ row.businessOrderNo || '-' }}</span
          >
        </div>
      </template>

      <template #column-hotelOrder="{ row }">
        <ElPopover
          v-if="row.hotelOrder"
          placement="right"
          :width="300"
          trigger="hover"
        >
          <template #reference>
            <span class="order-info-trigger">
              {{ row.hotelOrder.mtOrderNo || '已关联酒店订单' }}
              <span class="order-info-hint">详情</span>
            </span>
          </template>
          <div class="info-grid">
            <div class="info-line">
              <span class="info-label">萌兔单号</span>
              <span class="info-value">{{
                row.hotelOrder.mtOrderNo || '-'
              }}</span>
            </div>
            <div class="info-line">
              <span class="info-label">酒店编码</span>
              <span class="info-value">{{
                row.hotelOrder.mtHotelCode || '-'
              }}</span>
            </div>
            <div class="info-line">
              <span class="info-label">SupId</span>
              <span class="info-value">{{ row.hotelOrder.supId ?? '-' }}</span>
            </div>
            <div class="info-line">
              <span class="info-label">ShotelCode</span>
              <span class="info-value">{{
                row.hotelOrder.sHotelCode || '-'
              }}</span>
            </div>
            <div class="info-line">
              <span class="info-label">入住离店</span>
              <span class="info-value">{{
                stayRange(row.hotelOrder as HotelOrder) || '-'
              }}</span>
            </div>
          </div>
        </ElPopover>
        <span v-else class="cell-subtitle">-</span>
      </template>

      <template #column-apiClient="{ row }">
        <div class="cell-stack">
          <span class="cell-title">{{
            row.apiClientName || row.apiClientCode || '-'
          }}</span>
          <span class="cell-subtitle">{{ row.apiClientCode }}</span>
        </div>
      </template>

      <template #column-providerAccount="{ row }">
        <div class="cell-stack">
          <span class="cell-title">{{ row.providerAccountName || '-' }}</span>
          <span class="cell-subtitle">#{{ row.providerAccountId }}</span>
        </div>
      </template>

      <template #column-limitAmount="{ row }">
        <div class="cell-stack">
          <span class="cell-title">
            额度 {{ formatAmount(row.limitAmountValue) }}
            {{ row.limitAmountCurrency }}
          </span>
          <template v-if="activeUsages(row as CardListItem).length">
            <span
              v-for="u in activeUsages(row as CardListItem).slice(0, 2)"
              :key="u.currency"
              class="cell-subtitle"
            >
              {{ usageLabel(u) }}
              {{ formatAmount(Math.abs(Number(u.usedAmount))) }} {{ u.currency
              }}{{ u.includesUnprojectedFacts ? ' · 含未归一' : '' }}
            </span>
            <span
              v-if="activeUsages(row as CardListItem).length > 2"
              class="cell-subtitle"
            >
              另有 {{ activeUsages(row as CardListItem).length - 2 }} 个币种
            </span>
          </template>
          <span v-else class="cell-subtitle"
            >已用 0.00 {{ row.limitAmountCurrency }}</span
          >
        </div>
      </template>

      <template #column-expectedBusinessStatus="{ row }">
        <ElTag
          :type="expectedStatus(row.expectedBusinessStatus).type"
          size="small"
        >
          {{ expectedStatus(row.expectedBusinessStatus).label }}
        </ElTag>
      </template>

      <template #column-transactionHealth="{ row }">
        <ElTooltip
          :content="transactionHealth(row as CardListItem).reason"
          placement="top"
        >
          <ElTag
            :type="transactionHealth(row as CardListItem).type"
            size="small"
          >
            {{ transactionHealth(row as CardListItem).label }}
          </ElTag>
        </ElTooltip>
      </template>

      <template #column-transactionCount="{ row }">
        <div class="cell-stack">
          <span class="cell-title">{{ row.transactionCount }}</span>
          <span class="cell-subtitle">笔</span>
        </div>
      </template>

      <template #column-latestTransactionAt="{ row }">
        <div class="cell-stack">
          <span class="cell-title">{{
            formatDateTime(row.latestTransactionAt)
          }}</span>
          <span v-if="row.hotelOrder" class="cell-subtitle">{{
            stayRange(row.hotelOrder as HotelOrder)
          }}</span>
        </div>
      </template>

      <template #column-expireAt="{ row }">
        <span :class="{ 'text-red-500 font-medium': isExpired(row.expireAt) }">
          {{ formatDateOnly(row.expireAt) || '-' }}
        </span>
      </template>
    </TablePage>
  </Page>
</template>

<style scoped>
.cell-stack {
  display: flex;
  flex-direction: column;
  gap: 2px;
  line-height: 1.35;
}

.cell-title {
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.cell-subtitle {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.info-grid {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.info-line {
  display: flex;
  align-items: baseline;
  gap: 6px;
  font-size: 12px;
  line-height: 1.4;
}

.info-label {
  flex: 0 0 auto;
  min-width: 72px;
  color: var(--el-text-color-secondary);
}

.info-value {
  flex: 1 1 auto;
  font-weight: 500;
  color: var(--el-text-color-primary);
  word-break: break-all;
}

.order-info-trigger {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  font-weight: 500;
  color: var(--el-text-color-primary);
  border-bottom: 1px dashed var(--el-border-color);
}

.order-info-hint {
  font-size: 12px;
  font-weight: 400;
  color: var(--el-color-primary);
}
</style>
