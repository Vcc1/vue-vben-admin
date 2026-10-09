<script lang="ts" setup>
import type { SessionApi } from '#/api/session';

import { onMounted, ref, watch } from 'vue';

import {
  ElAlert,
  ElButton,
  ElEmpty,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { listDeviceUsageApi } from '#/api/session';

import {
  deviceStatusLabel,
  deviceStatusTagType,
  formatTime,
} from './format';

defineOptions({ name: 'DeviceUsageDetail' });

const props = defineProps<{ deviceId: string }>();

const emit = defineEmits<{
  (e: 'viewUser', payload: { displayName: string; userId: string }): void;
}>();

const rows = ref<SessionApi.DeviceUsageItem[]>([]);
const loading = ref(false);

async function load() {
  if (!props.deviceId) return;
  loading.value = true;
  try {
    rows.value = (await listDeviceUsageApi(props.deviceId)) ?? [];
  } finally {
    loading.value = false;
  }
}

watch(() => props.deviceId, load);
onMounted(load);
</script>

<template>
  <div v-loading="loading">
    <ElAlert :closable="false" class="mb-3" type="info">
      <template #title>
        设备 <span class="break-all font-mono">{{ deviceId }}</span>
      </template>
      共 {{ rows.length }} 个用户从这台设备登录过。被踢出受信设备的记录同样保留，便于排查共享设备与凭证泄露。
    </ElAlert>

    <ElTable :data="rows" size="small" stripe>
      <ElTableColumn label="用户" min-width="150" show-overflow-tooltip>
        <template #default="{ row }">
          <div class="font-medium">{{ row.displayName || row.userName }}</div>
          <div class="text-xs text-gray-400">{{ row.userName }}</div>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="受信状态" width="110">
        <template #default="{ row }">
          <ElTag :type="deviceStatusTagType(row.status)" size="small">
            {{ deviceStatusLabel(row.status) }}
          </ElTag>
          <ElTag v-if="row.isOnline" class="ml-1" size="small" type="warning">
            在线
          </ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn align="center" label="登录次数" width="90">
        <template #default="{ row }">{{ row.sessionCount }}</template>
      </ElTableColumn>
      <ElTableColumn label="首次登录" width="165">
        <template #default="{ row }">
          {{ formatTime(row.firstSeenUtc) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="最近使用" width="165">
        <template #default="{ row }">
          {{ formatTime(row.lastSeenUtc) }}
        </template>
      </ElTableColumn>
      <ElTableColumn label="踢除记录" min-width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <span v-if="row.revokedAtUtc">
            {{ formatTime(row.revokedAtUtc) }} · {{ row.revokedReason || '-' }}
          </span>
          <span v-else class="text-gray-400">-</span>
        </template>
      </ElTableColumn>
      <ElTableColumn fixed="right" label="操作" width="110">
        <template #default="{ row }">
          <ElButton
            link
            size="small"
            type="primary"
            @click="
              emit('viewUser', {
                displayName: row.displayName || row.userName,
                userId: row.userId,
              })
            "
          >
            查看该用户
          </ElButton>
        </template>
      </ElTableColumn>
    </ElTable>

    <ElEmpty
      v-if="!loading && rows.length === 0"
      :image-size="60"
      description="没有用户从这台设备登录过"
    />
  </div>
</template>
