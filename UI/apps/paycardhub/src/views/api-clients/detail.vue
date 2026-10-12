<script lang="ts" setup>
import { onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { type ApiClientDetail, getClientDetailApi } from '#/api/api-clients';

const route = useRoute();
const loading = ref(false);
const detail = ref<ApiClientDetail | null>(null);

function formatDateTime(val?: string) {
  if (!val) return '-';
  return new Date(val).toLocaleString('zh-CN');
}

async function loadDetail() {
  const id = Number(route.params.id);
  if (!id) return;
  loading.value = true;
  try {
    detail.value = await getClientDetailApi(id);
  } finally {
    loading.value = false;
  }
}

onMounted(loadDetail);
</script>

<template>
  <Page title="API 客户端详情" v-loading="loading">
    <template v-if="detail">
      <ElCard class="mb-4">
        <ElDescriptions :column="3" border>
          <ElDescriptionsItem label="ID">{{ detail.id }}</ElDescriptionsItem>
          <ElDescriptionsItem label="客户端代码">{{ detail.clientCode }}</ElDescriptionsItem>
          <ElDescriptionsItem label="显示名称">{{ detail.displayName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="启用">
            <ElTag :type="detail.isActive ? 'success' : 'info'" size="small">
              {{ detail.isActive ? '是' : '否' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="订单数">{{ detail.orderCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="卡片数">{{ detail.cardCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="交易数">{{ detail.transactionCount }}</ElDescriptionsItem>
          <ElDescriptionsItem label="创建时间">{{ formatDateTime(detail.createdAt) }}</ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard>
        <template #header>API Key 列表</template>
        <ElTable :data="detail.apiKeys" stripe size="small">
          <ElTableColumn prop="id" label="ID" width="70" />
          <ElTableColumn prop="keyName" label="密钥名称" width="180" />
          <ElTableColumn label="启用" width="80" align="center">
            <template #default="{ row }">
              <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
                {{ row.isActive ? '是' : '否' }}
              </ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn prop="allowedIpAddresses" label="允许 IP" min-width="250" show-overflow-tooltip />
          <ElTableColumn label="创建时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
          </ElTableColumn>
        </ElTable>
      </ElCard>
    </template>
  </Page>
</template>
