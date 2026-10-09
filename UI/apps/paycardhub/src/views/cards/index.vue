<script lang="ts" setup>
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElInput,
  ElOption,
  ElPagination,
  ElPopover,
  ElSelect,
  ElTable,
  ElTableColumn,
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
const loading = ref(false);
const cards = ref<CardListItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');
const providerCode = ref('');
const providerAccountId = ref<number | undefined>(undefined);
const apiClientCode = ref('');

const providerAccounts = ref<ProviderAccountOption[]>([]);
const apiClients = ref<ApiClientOption[]>([]);

async function loadData() {
  loading.value = true;
  try {
    const res = await getCardsApi({
      keyword: keyword.value || undefined,
      providerCode: providerCode.value || undefined,
      providerAccountId: providerAccountId.value,
      apiClientCode: apiClientCode.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });
    cards.value = res.items;
    total.value = res.totalCount;
  } finally {
    loading.value = false;
  }
}

async function loadOptions() {
  const [accounts, clients] = await Promise.all([
    getProviderAccountOptionsApi(),
    getClientOptionsApi(),
  ]);
  providerAccounts.value = accounts;
  apiClients.value = clients;
}

function handleSearch() {
  page.value = 1;
  loadData();
}

function handleReset() {
  keyword.value = '';
  providerCode.value = '';
  providerAccountId.value = undefined;
  apiClientCode.value = '';
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

function viewDetail(card: CardListItem) {
  router.push({ name: 'CardDetail', params: { id: card.id } });
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

function relativeTime(val?: string) {
  if (!val) return '无记录';
  const d = new Date(val);
  if (Number.isNaN(d.getTime())) return '无记录';
  const seconds = Math.round((d.getTime() - Date.now()) / 1000);
  const abs = Math.abs(seconds);
  const f = new Intl.RelativeTimeFormat('zh-CN', { numeric: 'auto' });
  if (abs < 60) return f.format(seconds, 'second');
  if (abs < 3600) return f.format(Math.round(seconds / 60), 'minute');
  if (abs < 86_400) return f.format(Math.round(seconds / 3600), 'hour');
  return f.format(Math.round(seconds / 86_400), 'day');
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

function formatDateOnly(val?: string) {
  if (!val) return '';
  const d = new Date(val);
  if (Number.isNaN(d.getTime())) return val.slice(0, 10);
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
}

function stayRange(h?: HotelOrder) {
  if (!h) return '';
  const ci = formatDateOnly(h.checkIn);
  const co = formatDateOnly(h.checkOut);
  if (ci && co) return `${ci} ~ ${co}`;
  return ci || co || '';
}

onMounted(() => {
  loadOptions();
  loadData();
});
</script>

<template>
  <Page title="卡管理">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-56">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput
            v-model="keyword"
            placeholder="订单号 / 卡 ID / 卡号"
            clearable
            @keyup.enter="handleSearch"
          />
        </div>
        <div class="w-44">
          <label class="mb-1 block text-xs text-gray-500">供应商</label>
          <ProviderSelect v-model="providerCode" />
        </div>
        <div class="w-52">
          <label class="mb-1 block text-xs text-gray-500">供应商账户</label>
          <ElSelect v-model="providerAccountId" clearable placeholder="全部">
            <ElOption
              v-for="acc in providerAccounts"
              :key="acc.id"
              :label="`${acc.providerCode} - ${acc.accountName}`"
              :value="acc.id"
            />
          </ElSelect>
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
      <ElTable
        :data="cards"
        v-loading="loading"
        stripe
        row-class-name="cursor-pointer"
        @row-click="viewDetail"
      >
        <ElTableColumn label="单号" min-width="200">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">{{ row.orderNo || '-' }}</span>
              <span class="cell-subtitle">业务单号: {{ row.businessOrderNo || '-' }}</span>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="订单信息" min-width="170">
          <template #default="{ row }">
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
                  <span class="info-value">{{ row.hotelOrder.mtOrderNo || '-' }}</span>
                </div>
                <div class="info-line">
                  <span class="info-label">酒店编码</span>
                  <span class="info-value">{{ row.hotelOrder.mtHotelCode || '-' }}</span>
                </div>
                <div class="info-line">
                  <span class="info-label">SupId</span>
                  <span class="info-value">{{ row.hotelOrder.supId ?? '-' }}</span>
                </div>
                <div class="info-line">
                  <span class="info-label">ShotelCode</span>
                  <span class="info-value">{{ row.hotelOrder.sHotelCode || '-' }}</span>
                </div>
                <div class="info-line">
                  <span class="info-label">入住离店</span>
                  <span class="info-value">{{ stayRange(row.hotelOrder as HotelOrder) || '-' }}</span>
                </div>
              </div>
            </ElPopover>
            <span v-else class="cell-subtitle">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="持卡人" min-width="130">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">{{ row.cardholderName || `持卡人 #${row.cardholderId}` }}</span>
              <span class="cell-subtitle">#{{ row.cardholderId }}</span>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="API 客户端" min-width="140">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">{{ row.apiClientName || row.apiClientCode || '-' }}</span>
              <span class="cell-subtitle">{{ row.apiClientCode }}</span>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="供应商账户" min-width="160">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">{{ row.providerAccountName || '-' }}</span>
              <span class="cell-subtitle">#{{ row.providerAccountId }}</span>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="额度 / 已使用（含预授权）" min-width="230">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">
                额度 {{ formatAmount(row.limitAmountValue) }} {{ row.limitAmountCurrency }}
              </span>
              <template v-if="activeUsages(row as CardListItem).length">
                <span
                  v-for="u in activeUsages(row as CardListItem).slice(0, 2)"
                  :key="u.currency"
                  class="cell-subtitle"
                >
                  {{ usageLabel(u) }} {{ formatAmount(Math.abs(Number(u.usedAmount))) }}
                  {{ u.currency }}{{ u.includesUnprojectedFacts ? ' · 含未归一' : '' }}
                </span>
                <span v-if="activeUsages(row as CardListItem).length > 2" class="cell-subtitle">
                  另有 {{ activeUsages(row as CardListItem).length - 2 }} 个币种
                </span>
              </template>
              <span v-else class="cell-subtitle">已用 0.00 {{ row.limitAmountCurrency }}</span>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="期望状态" width="100" align="center">
          <template #default="{ row }">
            <ElTag :type="expectedStatus(row.expectedBusinessStatus).type" size="small">
              {{ expectedStatus(row.expectedBusinessStatus).label }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="交易状态" width="100" align="center">
          <template #default="{ row }">
            <ElTooltip :content="transactionHealth(row as CardListItem).reason" placement="top">
              <ElTag :type="transactionHealth(row as CardListItem).type" size="small">
                {{ transactionHealth(row as CardListItem).label }}
              </ElTag>
            </ElTooltip>
          </template>
        </ElTableColumn>
        <ElTableColumn label="交易" width="80" align="center">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">{{ row.transactionCount }}</span>
              <span class="cell-subtitle">笔</span>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="最近观测" min-width="150">
          <template #default="{ row }">
            <div class="cell-stack">
              <span class="cell-title">{{ formatDateTime(row.latestObservedAt) }}</span>
              <span class="cell-subtitle">{{ relativeTime(row.latestObservedAt) }}</span>
            </div>
          </template>
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
