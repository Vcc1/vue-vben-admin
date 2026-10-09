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
  listSecurityEventsApi,
  type SecurityEventItem,
} from '#/api/auditing';

import BodyView from './BodyView.vue';

const severityOptions = ['Critical', 'High', 'Medium', 'Low', 'Info'];
const takeOptions = [50, 100, 200, 500];

const filter = reactive({
  actorName: '',
  eventType: '',
  severity: '',
  take: 100,
});

const rows = ref<SecurityEventItem[]>([]);
const loading = ref(false);

async function load() {
  loading.value = true;
  try {
    rows.value = await listSecurityEventsApi({
      actorName: filter.actorName || undefined,
      eventType: filter.eventType || undefined,
      severity: filter.severity || undefined,
      take: filter.take,
    });
  } finally {
    loading.value = false;
  }
}

const [DetailDrawer, detailDrawerApi] = useVbenDrawer();
const detail = ref<null | SecurityEventItem>(null);

function openDetail(row: SecurityEventItem) {
  detail.value = row;
  detailDrawerApi.open();
}

function formatTime(dt: null | string | undefined) {
  if (!dt) return '-';
  return new Date(dt).toLocaleString();
}

type TagType = 'danger' | 'info' | 'primary' | 'success' | 'warning';

function severityTagType(severity: string): TagType {
  const map: Record<string, TagType> = {
    Critical: 'danger',
    High: 'danger',
    Info: 'info',
    Low: 'success',
    Medium: 'warning',
  };
  return map[severity] ?? 'info';
}

onMounted(load);
</script>

<template>
  <Page title="安全审计" description="查看 refresh token 重放等安全事件">
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
          v-model="filter.eventType"
          placeholder="事件类型包含"
          clearable
          class="!w-48"
          @keyup.enter="load"
        />
        <ElSelect
          v-model="filter.severity"
          placeholder="级别"
          clearable
          class="!w-32"
        >
          <ElOption v-for="s in severityOptions" :key="s" :label="s" :value="s" />
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
        <ElTableColumn prop="eventType" label="事件类型" width="180" show-overflow-tooltip />
        <ElTableColumn label="级别" width="90" align="center">
          <template #default="{ row }">
            <ElTag :type="severityTagType(row.severity)" size="small">
              {{ row.severity }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="summary" label="摘要" min-width="260" show-overflow-tooltip />
        <ElTableColumn prop="actorName" label="操作人" width="130" show-overflow-tooltip />
        <ElTableColumn prop="ipAddress" label="IP" width="130" show-overflow-tooltip />
        <ElTableColumn label="操作" width="80" fixed="right">
          <template #default="{ row }">
            <ElButton size="small" @click="openDetail(row as SecurityEventItem)">
              详情
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <DetailDrawer title="安全事件详情" class="w-200">
      <template v-if="detail">
        <ElDescriptions :column="2" border size="small" class="mb-4">
          <ElDescriptionsItem label="时间">
            {{ formatTime(detail.occurredAtUtc) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="级别">
            <ElTag :type="severityTagType(detail.severity)" size="small">
              {{ detail.severity }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="事件类型" :span="2">
            {{ detail.eventType }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="摘要" :span="2">
            {{ detail.summary || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="操作人">
            {{ detail.actorName }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="身份类型">
            {{ detail.actorType }} / {{ detail.authenticationType }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="IP">{{ detail.ipAddress || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="CorrelationId">
            {{ detail.correlationId || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="User-Agent" :span="2">
            <span class="break-all">{{ detail.userAgent || '-' }}</span>
          </ElDescriptionsItem>
        </ElDescriptions>

        <h4 class="section-title">详情数据</h4>
        <BodyView :raw="detail.detailJson" />
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
