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

import { type CardholderDetail, getCardholderDetailApi } from '#/api/cardholders';

const route = useRoute();
const loading = ref(false);
const detail = ref<CardholderDetail | null>(null);

function formatDateTime(val?: string) {
  if (!val) return '-';
  return new Date(val).toLocaleString('zh-CN');
}

async function loadDetail() {
  const id = Number(route.params.id);
  if (!id) return;
  loading.value = true;
  try {
    detail.value = await getCardholderDetailApi(id);
  } finally {
    loading.value = false;
  }
}

onMounted(loadDetail);
</script>

<template>
  <Page title="持卡人详情" v-loading="loading">
    <template v-if="detail">
      <ElCard class="mb-4">
        <ElDescriptions :column="3" border>
          <ElDescriptionsItem label="ID">{{ detail.id }}</ElDescriptionsItem>
          <ElDescriptionsItem label="显示名称">{{ detail.displayName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="邮箱">{{ detail.email }}</ElDescriptionsItem>
          <ElDescriptionsItem label="手机号">{{ detail.mobileNumber }}</ElDescriptionsItem>
          <ElDescriptionsItem label="类型">
            <ElTag size="small">{{ detail.type }}</ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="状态">
            <ElTag :type="detail.status?.toUpperCase() === 'ACTIVE' ? 'success' : 'info'" size="small">{{ detail.status }}</ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="国籍">{{ detail.nationality || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="出生日期">{{ detail.dateOfBirth || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="城市">{{ detail.city || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="国家">{{ detail.country || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="创建时间">{{ formatDateTime(detail.createdAt) }}</ElDescriptionsItem>
          <ElDescriptionsItem label="更新时间">{{ formatDateTime(detail.updatedAt) }}</ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard class="mb-4">
        <template #header>供应商映射</template>
        <ElTable :data="detail.providerMappings" stripe size="small">
          <ElTableColumn prop="providerCode" label="供应商" width="120" />
          <ElTableColumn prop="providerAccountName" label="账户" width="200" />
          <ElTableColumn prop="providerCardholderId" label="供应商持卡人 ID" min-width="200" show-overflow-tooltip />
          <ElTableColumn prop="status" label="状态" width="100">
            <template #default="{ row }">
              <ElTag size="small">{{ row.status }}</ElTag>
            </template>
          </ElTableColumn>
          <ElTableColumn label="更新时间" width="170">
            <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
          </ElTableColumn>
        </ElTable>
      </ElCard>
    </template>
  </Page>
</template>
