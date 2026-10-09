<script lang="ts" setup>
import type { SessionApi } from '#/api/session';

import { onMounted, ref, watch } from 'vue';

import {
  ElButton,
  ElCard,
  ElEmpty,
  ElMessage,
  ElMessageBox,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  listSessionsApi,
  listUserDevicesApi,
  revokeDeviceApi,
  revokeSessionApi,
  revokeUserSessionsApi,
} from '#/api/session';

import {
  deviceStatusLabel,
  deviceStatusTagType,
  deviceText,
  formatTime,
  levelTagType,
  loginMethodText,
} from './format';

defineOptions({ name: 'UserSessionDetail' });

const props = defineProps<{ displayName?: string; userId: string }>();

const devices = ref<SessionApi.DeviceItem[]>([]);
const sessions = ref<SessionApi.SessionItem[]>([]);
const includeRevoked = ref(true);
const loading = ref(false);

async function load() {
  if (!props.userId) return;
  loading.value = true;
  try {
    const [sessionList, deviceList] = await Promise.all([
      listSessionsApi({ includeRevoked: includeRevoked.value, userId: props.userId }),
      listUserDevicesApi(props.userId),
    ]);
    sessions.value = sessionList ?? [];
    devices.value = deviceList ?? [];
  } finally {
    loading.value = false;
  }
}

async function kick(row: SessionApi.SessionItem) {
  const { value } = await ElMessageBox.prompt(
    `确定强制下线「${row.displayName}」在 ${deviceText(row.device)} 上的登录吗？`,
    '强制下线',
    {
      confirmButtonText: '强制下线',
      cancelButtonText: '取消',
      inputPlaceholder: '吊销原因（会记入审计）',
      inputValue: 'admin-kick',
      type: 'warning',
    },
  );
  await revokeSessionApi(row.sid, value || undefined);
  ElMessage.success('已强制下线');
  await load();
}

async function kickAll() {
  await ElMessageBox.confirm(
    `确定吊销「${props.displayName || props.userId}」的全部登录凭证吗？该用户在所有设备与子系统上都会立即掉线。`,
    '全部强制下线',
    { type: 'warning' },
  );
  const result = await revokeUserSessionsApi(props.userId, 'admin-kick-all');
  ElMessage.success(`已吊销 ${result?.revoked ?? 0} 个登录凭证`);
  await load();
}

async function removeDevice(row: SessionApi.DeviceItem) {
  await ElMessageBox.confirm(
    `确定把「${deviceText(row.device)}」从该用户的受信设备中踢除吗？该设备上的会话会被吊销，记录会保留。`,
    '踢除受信设备',
    { type: 'warning' },
  );
  await revokeDeviceApi(props.userId, row.deviceId, 'admin-revoke-device');
  ElMessage.success('设备已踢除');
  await load();
}

watch(() => props.userId, load);
watch(includeRevoked, load);
onMounted(load);
</script>

<template>
  <div v-loading="loading">
    <ElCard class="mb-4" shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <span>设备（{{ devices.length }}）</span>
        </div>
      </template>
      <ElTable :data="devices" row-key="deviceId" size="small" stripe>
        <ElTableColumn label="设备" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            <div>{{ deviceText(row.device) }}</div>
            <div class="text-xs text-gray-400">{{ row.deviceId }}</div>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="状态" width="110">
          <template #default="{ row }">
            <ElTag :type="deviceStatusTagType(row.status)" size="small">
              {{ deviceStatusLabel(row.status) }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="活跃会话" width="90">
          <template #default="{ row }">{{ row.activeSessionCount }}</template>
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
              {{ formatTime(row.revokedAtUtc) }} ·
              {{ row.revokedReason || '-' }}
            </span>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="100">
          <template #default="{ row }">
            <ElButton
              :disabled="!row.isTrusted"
              link
              size="small"
              type="danger"
              @click="removeDevice(row as SessionApi.DeviceItem)"
            >
              踢除设备
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
      <ElEmpty
        v-if="!loading && devices.length === 0"
        :image-size="60"
        description="没有设备记录"
      />
    </ElCard>

    <ElCard shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <span>登录凭证（{{ sessions.length }}）</span>
          <div class="flex items-center gap-3">
            <div class="flex items-center gap-1 text-xs">
              <span>含已吊销</span>
              <ElSwitch v-model="includeRevoked" size="small" />
            </div>
            <ElButton
              :disabled="sessions.every((x) => x.isRevoked)"
              size="small"
              type="danger"
              @click="kickAll"
            >
              全部强制下线
            </ElButton>
          </div>
        </div>
      </template>
      <ElTable :data="sessions" size="small" stripe>
        <ElTableColumn label="设备" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            {{ deviceText(row.device) }}
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="等级" width="70">
          <template #default="{ row }">
            <ElTag :type="levelTagType(row.level)" size="small">
              L{{ row.level }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="登录方式" width="90">
          <template #default="{ row }">
            {{ loginMethodText(row.loginMethod) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="子系统" min-width="120">
          <template #default="{ row }">
            <ElTag
              v-for="client in row.clients"
              :key="client"
              class="mr-1"
              size="small"
              type="info"
            >
              {{ client }}
            </ElTag>
            <span v-if="!row.clients?.length" class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn
          label="IP"
          prop="ipAddress"
          show-overflow-tooltip
          width="120"
        />
        <ElTableColumn label="登录时间" width="165">
          <template #default="{ row }">
            {{ formatTime(row.createdAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="最近活动" width="165">
          <template #default="{ row }">
            {{ formatTime(row.lastActivityAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="状态" width="120">
          <template #default="{ row }">
            <ElTag v-if="row.isRevoked" size="small" type="danger">
              已吊销
            </ElTag>
            <ElTag v-else-if="row.isOnline" size="small" type="success">
              在线
            </ElTag>
            <ElTag v-else size="small" type="info">离线</ElTag>
            <ElTag v-if="row.isCurrent" class="ml-1" size="small">本机</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="吊销记录" min-width="170" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.revokedAtUtc">
              {{ formatTime(row.revokedAtUtc) }} ·
              {{ row.revokedReason || '-' }}
            </span>
            <span v-else class="text-gray-400">-</span>
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="90">
          <template #default="{ row }">
            <ElButton
              :disabled="row.isRevoked"
              link
              size="small"
              type="danger"
              @click="kick(row as SessionApi.SessionItem)"
            >
              踢下线
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
      <ElEmpty
        v-if="!loading && sessions.length === 0"
        :image-size="60"
        description="没有登录凭证"
      />
    </ElCard>
  </div>
</template>
