<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElEmpty,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTabs,
  ElTabPane,
} from 'element-plus';

import {
  type CardTransactionsResponse,
  getCardTransactionsApi,
} from '#/api/cards';
import EnumLabel from '#/components/enum-label.vue';
import { cardStatus } from '#/utils/card-status';
import {
  NORMALIZED_STATUS_LABELS,
  NORMALIZED_TRANSACTION_TYPE_LABELS,
} from '#/utils/transaction-labels';

const route = useRoute();
const loading = ref(false);
const data = ref<CardTransactionsResponse | null>(null);

const normalizedTransactionCount = computed(() =>
  (data.value?.normalizedLifecycles ?? []).reduce(
    (sum, lc) => sum + lc.transactions.length,
    0,
  ),
);

function formatDateTime(val?: string) {
  if (!val) return '-';
  return new Date(val).toLocaleString('zh-CN');
}

function formatAmount(amount?: number) {
  if (amount === undefined || amount === null) return '-';
  return amount.toFixed(2);
}

function syncStatusType(status: string) {
  const s = status?.toLowerCase();
  if (s === 'synced' || s === 'completed') return 'success';
  if (s === 'failed' || s === 'error') return 'danger';
  if (s === 'pending') return 'warning';
  return 'info';
}

function directionLabel(dir: string) {
  if (dir === 'Inbound' || dir === 'inbound') return { label: '贷记', type: 'success' as const };
  if (dir === 'Outbound' || dir === 'outbound') return { label: '借记', type: undefined };
  return { label: dir, type: 'info' as const };
}

async function loadDetail() {
  const cardId = Number(route.params.id);
  if (!cardId) return;
  loading.value = true;
  try {
    data.value = await getCardTransactionsApi(cardId);
  } finally {
    loading.value = false;
  }
}

onMounted(loadDetail);
</script>

<template>
  <Page title="卡片详情" v-loading="loading">
    <template v-if="data">
      <ElCard class="mb-4">
        <ElDescriptions :column="3" border>
          <ElDescriptionsItem label="卡 ID">{{ data.card.id }}</ElDescriptionsItem>
          <ElDescriptionsItem label="供应商">{{ data.card.providerCode }}</ElDescriptionsItem>
          <ElDescriptionsItem label="供应商卡 ID">{{ data.card.providerCardId }}</ElDescriptionsItem>
          <ElDescriptionsItem label="卡号">{{ data.card.maskedCardNumber }}</ElDescriptionsItem>
          <ElDescriptionsItem label="额度">{{ data.card.limitAmountCurrency }} {{ formatAmount(data.card.limitAmountValue) }}</ElDescriptionsItem>
          <ElDescriptionsItem label="状态">
            <ElTag :type="cardStatus(data.card.status).type" size="small">
              {{ cardStatus(data.card.status).label }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="订单号">{{ data.card.orderNo }}</ElDescriptionsItem>
          <ElDescriptionsItem label="业务订单号">{{ data.card.businessOrderNo }}</ElDescriptionsItem>
          <ElDescriptionsItem label="预期状态">{{ data.card.expectedBusinessStatus }}</ElDescriptionsItem>
          <ElDescriptionsItem label="API 客户端">{{ data.card.apiClientName || data.card.apiClientCode }}</ElDescriptionsItem>
          <ElDescriptionsItem label="供应商账户">{{ data.card.providerAccountName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="持卡人">{{ data.card.cardholderName || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="邮箱" v-if="data.card.email">{{ data.card.email }}</ElDescriptionsItem>
          <ElDescriptionsItem label="手机号" v-if="data.card.mobileNumber">{{ data.card.mobileNumber }}</ElDescriptionsItem>
          <ElDescriptionsItem label="国籍" v-if="data.card.individualNationality">{{ data.card.individualNationality }}</ElDescriptionsItem>
          <ElDescriptionsItem label="创建时间">{{ formatDateTime(data.card.createdAt) }}</ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard v-if="data.card.hotelOrder" class="mb-4">
        <template #header>酒店订单</template>
        <ElDescriptions :column="3" border>
          <ElDescriptionsItem label="MT 订单号">{{ data.card.hotelOrder.mtOrderNo || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="MT 酒店代码">{{ data.card.hotelOrder.mtHotelCode || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="S 酒店代码">{{ data.card.hotelOrder.sHotelCode || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="Sup ID">{{ data.card.hotelOrder.supId || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="入住时间">{{ formatDateTime(data.card.hotelOrder.checkIn) }}</ElDescriptionsItem>
          <ElDescriptionsItem label="退房时间">{{ formatDateTime(data.card.hotelOrder.checkOut) }}</ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard v-if="data.usageAmounts.length > 0" class="mb-4">
        <template #header>用量统计</template>
        <ElTable :data="data.usageAmounts" stripe size="small">
          <ElTableColumn prop="currency" label="币种" width="90" />
          <ElTableColumn label="授权" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.totalAuthorized) }}</template>
          </ElTableColumn>
          <ElTableColumn label="结算" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.settledAmount) }}</template>
          </ElTableColumn>
          <ElTableColumn label="待处理" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.pendingAmount) }}</template>
          </ElTableColumn>
          <ElTableColumn label="已使用" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.usedAmount) }}</template>
          </ElTableColumn>
          <ElTableColumn label="贷记" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.billingAmount) }}</template>
          </ElTableColumn>
          <ElTableColumn label="冲正" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.totalReversed) }}</template>
          </ElTableColumn>
          <ElTableColumn label="拒绝" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.totalDeclined) }}</template>
          </ElTableColumn>
          <ElTableColumn label="过期" width="110" align="right">
            <template #default="{ row }">{{ formatAmount(row.totalExpired) }}</template>
          </ElTableColumn>
          <ElTableColumn label="额度币种" width="100" align="center">
            <template #default="{ row }">
              <ElTag v-if="row.isLimitCurrency" type="warning" size="small">是</ElTag>
            </template>
          </ElTableColumn>
        </ElTable>
      </ElCard>

      <ElCard>
        <ElTabs>
          <ElTabPane :label="`标准化交易 (${normalizedTransactionCount})`">
            <div
              v-for="lc in data.normalizedLifecycles"
              :key="lc.id"
              class="mb-4 rounded border p-3"
            >
              <div class="mb-2 flex flex-wrap items-center gap-2">
                <ElTag size="small" type="warning">生命周期</ElTag>
                <ElTag size="small">{{ lc.status }}</ElTag>
                <span class="text-xs text-gray-500">供应商 {{ lc.providerCode }}</span>
                <span v-if="lc.providerLifecycleId" class="font-mono text-xs text-gray-400">
                  {{ lc.providerLifecycleId }}
                </span>
                <span class="text-xs text-gray-400">卡 {{ lc.providerCardId }}</span>
                <span class="text-xs text-gray-400">最近观测 {{ formatDateTime(lc.lastObservedAt) }}</span>
              </div>

              <div v-if="lc.billingAmounts.length > 0" class="mb-2">
                <div class="mb-1 text-xs font-medium text-gray-500">金额</div>
                <ElTable :data="lc.billingAmounts" stripe size="small" border>
                  <ElTableColumn prop="currency" label="币种" width="90" />
                  <ElTableColumn label="授权" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalAuthorized) }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="借记" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalDebited) }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="贷记" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalCredited) }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="待处理" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalPending) }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="冲正" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalReversed) }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="拒绝" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalDeclined) }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="过期" align="right">
                    <template #default="{ row }">{{ formatAmount(row.totalExpired) }}</template>
                  </ElTableColumn>
                </ElTable>
              </div>

              <div>
                <div class="mb-1 text-xs font-medium text-gray-500">
                  交易 ({{ lc.transactions.length }})
                </div>
                <ElTable :data="lc.transactions" stripe size="small" border>
                  <ElTableColumn type="expand">
                    <template #default="{ row }">
                      <div class="px-8 py-2">
                        <div class="mb-1 text-xs font-medium text-gray-500">
                          事件 ({{ row.events.length }})
                        </div>
                        <ElTable v-if="row.events.length > 0" :data="row.events" stripe size="small">
                          <ElTableColumn prop="eventNo" label="事件号" min-width="160" show-overflow-tooltip />
                          <ElTableColumn prop="eventType" label="事件类型" width="120" />
                          <ElTableColumn label="状态" width="110">
                            <template #default="{ row: ev }">
                              <ElTag :type="syncStatusType(ev.eventStatus)" size="small">{{ ev.eventStatus }}</ElTag>
                            </template>
                          </ElTableColumn>
                          <ElTableColumn prop="processResult" label="处理结果" width="100" />
                          <ElTableColumn label="交易金额" width="140" align="right">
                            <template #default="{ row: ev }">
                              {{ ev.transactionCurrency }} {{ formatAmount(ev.transactionAmount) }}
                            </template>
                          </ElTableColumn>
                          <ElTableColumn label="账单金额" width="140" align="right">
                            <template #default="{ row: ev }">
                              {{ ev.billingCurrency }} {{ formatAmount(ev.billingAmount) }}
                            </template>
                          </ElTableColumn>
                          <ElTableColumn label="时间" width="170">
                            <template #default="{ row: ev }">{{ formatDateTime(ev.occurredAt) }}</template>
                          </ElTableColumn>
                        </ElTable>
                        <ElEmpty v-else :image-size="40" description="暂无事件" />
                      </div>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn prop="transactionNo" label="交易号" min-width="180" show-overflow-tooltip />
                  <ElTableColumn label="类型" width="100">
                    <template #default="{ row }">
                      <EnumLabel :map="NORMALIZED_TRANSACTION_TYPE_LABELS" :value="String(row.transactionType)" />
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="状态" width="110">
                    <template #default="{ row }">
                      <EnumLabel :map="NORMALIZED_STATUS_LABELS" :value="row.transactionStatus" />
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="交易金额" width="140" align="right">
                    <template #default="{ row }">
                      {{ row.transactionCurrency }} {{ formatAmount(row.transactionAmount) }}
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="账单金额" width="140" align="right">
                    <template #default="{ row }">
                      {{ row.billingCurrency }} {{ formatAmount(row.billingAmount) }}
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="实际扣款" width="140" align="right">
                    <template #default="{ row }">
                      <span v-if="row.realDeductAmount">
                        {{ row.realDeductCurrency }} {{ formatAmount(row.realDeductAmount) }}
                      </span>
                      <span v-else>-</span>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="终态" width="70" align="center">
                    <template #default="{ row }">
                      <ElTag v-if="row.isTerminal" type="success" size="small">是</ElTag>
                      <ElTag v-else type="info" size="small">否</ElTag>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="事件" width="70" align="center">
                    <template #default="{ row }">{{ row.events.length }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="交易时间" width="170">
                    <template #default="{ row }">{{ formatDateTime(row.transactionTime) }}</template>
                  </ElTableColumn>
                </ElTable>
                <ElEmpty v-if="lc.transactions.length === 0" :image-size="40" description="暂无交易" />
              </div>
            </div>
            <ElEmpty v-if="data.normalizedLifecycles.length === 0" description="暂无标准化交易" />
          </ElTabPane>

          <ElTabPane :label="`供应商交易 (${data.transactions.length})`">
            <div v-for="txPair in data.transactions" :key="txPair.providerTransaction.id" class="mb-4 rounded border p-3">
              <div class="mb-2 flex items-center gap-2">
                <ElTag size="small">{{ txPair.providerTransaction.transactionType }}</ElTag>
                <ElTag :type="directionLabel(txPair.providerTransaction.direction).type" size="small">
                  {{ directionLabel(txPair.providerTransaction.direction).label }}
                </ElTag>
                <ElTag :type="syncStatusType(txPair.providerTransaction.transactionStatus)" size="small">
                  {{ txPair.providerTransaction.transactionStatus }}
                </ElTag>
                <span class="text-sm font-medium">
                  {{ txPair.providerTransaction.transactionCurrency }} {{ formatAmount(txPair.providerTransaction.transactionAmount) }}
                </span>
                <span class="text-xs text-gray-400">{{ txPair.providerTransaction.merchantName }}</span>
              </div>

              <ElDescriptions :column="4" size="small" border class="mb-2">
                <ElDescriptionsItem label="供应商交易 ID">{{ txPair.providerTransaction.providerCardTransactionId }}</ElDescriptionsItem>
                <ElDescriptionsItem label="账单金额">{{ txPair.providerTransaction.billingCurrency }} {{ formatAmount(txPair.providerTransaction.billingAmount) }}</ElDescriptionsItem>
                <ElDescriptionsItem label="实际扣款" v-if="txPair.providerTransaction.realDeductAmount">
                  {{ txPair.providerTransaction.realDeductCurrency }} {{ formatAmount(txPair.providerTransaction.realDeductAmount) }}
                </ElDescriptionsItem>
                <ElDescriptionsItem label="供应商时间">{{ formatDateTime(txPair.providerTransaction.providerCreatedAt) }}</ElDescriptionsItem>
                <ElDescriptionsItem label="标准交易号" v-if="txPair.normalizedTransaction">
                  {{ txPair.normalizedTransaction.transactionNo }}
                </ElDescriptionsItem>
                <ElDescriptionsItem label="标准状态" v-if="txPair.normalizedTransaction">
                  <ElTag size="small">{{ txPair.normalizedTransaction.status }}</ElTag>
                </ElDescriptionsItem>
                <ElDescriptionsItem label="生命周期" v-if="txPair.providerLifecycle">
                  {{ txPair.providerLifecycle.providerLifecycleId }}
                </ElDescriptionsItem>
              </ElDescriptions>

              <div v-if="txPair.events.length > 0" class="mt-2">
                <div class="mb-1 text-xs font-medium text-gray-500">事件</div>
                <ElTable :data="txPair.events" stripe size="small">
                  <ElTableColumn label="事件类型" width="120">
                    <template #default="{ row }">{{ row.providerEvent.eventType }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="状态" width="100">
                    <template #default="{ row }">
                      <ElTag :type="syncStatusType(row.providerEvent.eventStatus)" size="small">{{ row.providerEvent.eventStatus }}</ElTag>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="处理结果" width="100">
                    <template #default="{ row }">{{ row.providerEvent.processResult }}</template>
                  </ElTableColumn>
                  <ElTableColumn label="交易金额" width="140">
                    <template #default="{ row }">
                      {{ row.providerEvent.transactionCurrency }} {{ formatAmount(row.providerEvent.transactionAmount) }}
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="账单金额" width="140">
                    <template #default="{ row }">
                      {{ row.providerEvent.billingCurrency }} {{ formatAmount(row.providerEvent.billingAmount) }}
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="已标准化" width="90" align="center">
                    <template #default="{ row }">
                      <ElTag v-if="row.normalizedEvent" type="success" size="small">是</ElTag>
                      <ElTag v-else type="info" size="small">否</ElTag>
                    </template>
                  </ElTableColumn>
                  <ElTableColumn label="时间" width="170">
                    <template #default="{ row }">{{ formatDateTime(row.providerEvent.providerCreatedAt) }}</template>
                  </ElTableColumn>
                </ElTable>
              </div>
            </div>
            <ElEmpty v-if="data.transactions.length === 0" description="暂无交易" />
          </ElTabPane>

          <ElTabPane :label="`变更日志 (${data.changes.length})`">
            <ElTable :data="data.changes" stripe size="small">
              <ElTableColumn prop="changeLogId" label="ID" width="70" />
              <ElTableColumn prop="changeType" label="类型" width="130" />
              <ElTableColumn prop="currency" label="币种" width="80" />
              <ElTableColumn prop="providerSyncStatus" label="同步状态" width="110">
                <template #default="{ row }">
                  <ElTag :type="syncStatusType(row.providerSyncStatus)" size="small">{{ row.providerSyncStatus }}</ElTag>
                </template>
              </ElTableColumn>
              <ElTableColumn prop="apiClientName" label="API 客户端" width="130" />
              <ElTableColumn prop="apiKeyName" label="API Key" width="120" />
              <ElTableColumn prop="ipAddress" label="IP" width="130" />
              <ElTableColumn label="请求时间" width="170">
                <template #default="{ row }">{{ formatDateTime(row.requestedAt) }}</template>
              </ElTableColumn>
              <ElTableColumn label="完成时间" width="170">
                <template #default="{ row }">{{ formatDateTime(row.providerCompletedAt) }}</template>
              </ElTableColumn>
              <ElTableColumn label="变更前" min-width="180">
                <template #default="{ row }">
                  <span class="break-all font-mono text-xs">{{ row.beforeValueJson }}</span>
                </template>
              </ElTableColumn>
              <ElTableColumn label="变更后" min-width="180">
                <template #default="{ row }">
                  <span class="break-all font-mono text-xs">{{ row.afterValueJson }}</span>
                </template>
              </ElTableColumn>
              <ElTableColumn label="错误" min-width="180">
                <template #default="{ row }">
                  <span v-if="row.providerErrorCode" class="text-red-500">
                    {{ row.providerErrorCode }}: {{ row.providerErrorMessage }}
                  </span>
                  <span v-else>-</span>
                </template>
              </ElTableColumn>
            </ElTable>
          </ElTabPane>
        </ElTabs>
      </ElCard>
    </template>
  </Page>
</template>
