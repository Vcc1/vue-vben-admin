<script lang="ts" setup>
import { onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElTag,
} from 'element-plus';

import { type ProviderAccountDetail, getProviderAccountDetailApi } from '#/api/provider-accounts';

const route = useRoute();
const loading = ref(false);
const detail = ref<ProviderAccountDetail | null>(null);

function formatDateTime(val?: string) {
  if (!val) return '-';
  return new Date(val).toLocaleString('zh-CN');
}

async function loadDetail() {
  const id = Number(route.params.id);
  if (!id) return;
  loading.value = true;
  try {
    detail.value = await getProviderAccountDetailApi(id);
  } finally {
    loading.value = false;
  }
}

onMounted(loadDetail);
</script>

<template>
  <Page title="供应商账户详情" v-loading="loading">
    <template v-if="detail">
      <ElCard class="mb-4">
        <ElDescriptions :column="3" border>
          <ElDescriptionsItem label="ID">{{ detail.id }}</ElDescriptionsItem>
          <ElDescriptionsItem label="供应商">{{ detail.providerCode }}</ElDescriptionsItem>
          <ElDescriptionsItem label="账户名称">{{ detail.accountName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="启用">
            <ElTag :type="detail.isActive ? 'success' : 'info'" size="small">
              {{ detail.isActive ? '是' : '否' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="自动发卡">
            <ElTag :type="detail.autoIssueEnabled ? 'success' : 'info'" size="small">
              {{ detail.autoIssueEnabled ? '是' : '否' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="优先级">{{ detail.autoIssuePriority }}</ElDescriptionsItem>
          <ElDescriptionsItem label="最大天数" v-if="detail.autoIssueMaxDays">
            {{ detail.autoIssueMaxDays }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="币种" v-if="detail.autoIssueCurrenciesJson">
            {{ detail.autoIssueCurrenciesJson }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="订单数">{{ detail.orderCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="卡片数">{{ detail.cardCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="持卡人数">{{ detail.cardholderMappingCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="交易数">{{ detail.transactionCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="最后同步">{{ formatDateTime(detail.lastSuccessfulSyncedAt) }}</ElDescriptionsItem>
          <ElDescriptionsItem label="创建时间">{{ formatDateTime(detail.createdAt) }}</ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard>
        <template #header>配置状态</template>
        <ElDescriptions :column="4" border>
          <ElDescriptionsItem label="Base URL">
            <ElTag :type="detail.configuration.hasBaseUrl ? 'success' : 'danger'" size="small">
              {{ detail.configuration.hasBaseUrl ? '已配置' : '缺失' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="Client ID">
            <ElTag :type="detail.configuration.hasClientId ? 'success' : 'danger'" size="small">
              {{ detail.configuration.hasClientId ? '已配置' : '缺失' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="API Key">
            <ElTag :type="detail.configuration.hasApiKey ? 'success' : 'danger'" size="small">
              {{ detail.configuration.hasApiKey ? '已配置' : '缺失' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="持卡人 ID">
            <ElTag :type="detail.configuration.hasCardholderId ? 'success' : 'danger'" size="small">
              {{ detail.configuration.hasCardholderId ? '已配置' : '缺失' }}
            </ElTag>
          </ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>
    </template>
  </Page>
</template>
