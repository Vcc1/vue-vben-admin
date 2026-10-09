<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';

import { Page, useVbenDrawer } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElInput,
  ElOption,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  listRequestAuditLogsApi,
  type RequestAuditLogItem,
} from '#/api/auditing';

import BodyView from './BodyView.vue';

const httpMethodOptions = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];
const takeOptions = [50, 100, 200, 500];

const filter = reactive({
  actorName: '',
  feature: '',
  httpMethod: '',
  path: '',
  take: 100,
});

const rows = ref<RequestAuditLogItem[]>([]);
const loading = ref(false);

async function load() {
  loading.value = true;
  try {
    rows.value = await listRequestAuditLogsApi({
      actorName: filter.actorName || undefined,
      feature: filter.feature || undefined,
      httpMethod: filter.httpMethod || undefined,
      path: filter.path || undefined,
      take: filter.take,
    });
  } finally {
    loading.value = false;
  }
}

const [DetailDrawer, detailDrawerApi] = useVbenDrawer();
const detail = ref<null | RequestAuditLogItem>(null);

function openDetail(row: RequestAuditLogItem) {
  detail.value = row;
  detailDrawerApi.open();
}

function formatTime(dt: null | string | undefined) {
  if (!dt) return '-';
  return new Date(dt).toLocaleString();
}

type TagType = 'danger' | 'info' | 'primary' | 'success' | 'warning';

function statusTagType(status: number): TagType {
  if (status >= 500) return 'danger';
  if (status >= 400) return 'warning';
  return 'success';
}

onMounted(load);
</script>

<template>
  <Page title="操作审计" description="查看被审计接口的请求与响应记录">
    <ElCard>
      <div class="mb-3 flex flex-wrap items-center gap-2">
        <ElInput
          v-model="filter.actorName"
          placeholder="操作人"
          clearable
          class="!w-40"
          @keyup.enter="load"
        />
        <ElInput
          v-model="filter.feature"
          placeholder="功能名称"
          clearable
          class="!w-40"
          @keyup.enter="load"
        />
        <ElInput
          v-model="filter.path"
          placeholder="路径包含"
          clearable
          class="!w-56"
          @keyup.enter="load"
        />
        <ElSelect
          v-model="filter.httpMethod"
          placeholder="方法"
          clearable
          class="!w-28"
        >
          <ElOption v-for="m in httpMethodOptions" :key="m" :label="m" :value="m" />
        </ElSelect>
        <ElSelect v-model="filter.take" class="!w-28">
          <ElOption
            v-for="t in takeOptions"
            :key="t"
            :label="`${t} 条`"
            :value="t"
          />
        </ElSelect>
        <ElButton type="primary" @click="load">查询</ElButton>
      </div>

      <ElTable :data="rows" v-loading="loading" stripe>
        <ElTableColumn label="时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.occurredAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn prop="actorName" label="操作人" width="130" show-overflow-tooltip />
        <ElTableColumn label="功能名称" width="120" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.featureName">{{ row.featureName }}</span>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作名称" min-width="150" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.operationName">{{ row.operationName }}</span>
            <!-- 存量数据没有声明过操作名，退回显示 action 名，至少能看出是哪个接口 -->
            <span v-else class="text-gray-400">{{ row.actionName || '-' }}</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="方法" width="90">
          <template #default="{ row }">
            <ElTag size="small">{{ row.httpMethod }}</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="path" label="路径" min-width="220" show-overflow-tooltip />
        <ElTableColumn label="状态" width="80" align="center">
          <template #default="{ row }">
            <ElTag :type="statusTagType(row.statusCode)" size="small">
              {{ row.statusCode }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="耗时" width="90" align="right">
          <template #default="{ row }">{{ row.durationMs }} ms</template>
        </ElTableColumn>
        <ElTableColumn prop="ipAddress" label="IP" width="130" show-overflow-tooltip />
        <ElTableColumn label="操作" width="80" fixed="right">
          <template #default="{ row }">
            <ElButton size="small" @click="openDetail(row as RequestAuditLogItem)">
              详情
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <DetailDrawer title="操作审计详情" class="w-200">
      <template v-if="detail">
        <ElDescriptions :column="2" border size="small" class="mb-4">
          <ElDescriptionsItem label="时间">
            {{ formatTime(detail.occurredAtUtc) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="耗时">
            {{ detail.durationMs }} ms
          </ElDescriptionsItem>
          <ElDescriptionsItem label="操作人">
            {{ detail.actorName }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="身份类型">
            {{ detail.actorType }} / {{ detail.authenticationType }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="方法">
            <ElTag size="small">{{ detail.httpMethod }}</ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="状态码">
            <ElTag :type="statusTagType(detail.statusCode)" size="small">
              {{ detail.statusCode }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="功能名称">
            {{ detail.featureName || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="操作名称">
            {{ detail.operationName || detail.actionName || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="路径" :span="2">
            {{ detail.path }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="控制器/动作" :span="2">
            {{ detail.controllerName || '-' }} / {{ detail.actionName || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="IP">{{ detail.ipAddress || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="CorrelationId">
            {{ detail.correlationId || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="User-Agent" :span="2">
            <span class="break-all">{{ detail.userAgent || '-' }}</span>
          </ElDescriptionsItem>
        </ElDescriptions>

        <h4 class="section-title">URL 参数</h4>
        <BodyView :raw="detail.queryString" />

        <h4 class="section-title">请求 Body</h4>
        <BodyView :raw="detail.requestBody" :truncated="detail.isRequestBodyTruncated" />

        <h4 class="section-title">响应 Body</h4>
        <BodyView :raw="detail.responseBody" :truncated="detail.isResponseBodyTruncated" />
      </template>
    </DetailDrawer>
  </Page>
</template>

<style scoped>
.section-title {
  margin: 16px 0 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
</style>
