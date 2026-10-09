<script lang="ts" setup>
import type { SessionApi } from '#/api/session';

import { onMounted, ref } from 'vue';

import {
  ElButton,
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  getSessionApi,
  listMyDevicesApi,
  listMySessionsApi,
  revokeMyDeviceApi,
  revokeMySessionApi,
} from '#/api/session';
import {
  deviceStatusLabel,
  deviceStatusTagType,
  deviceText,
  formatTime,
  loginMethodText,
} from '#/views/sessions/format';

defineOptions({ name: 'ProfileSessionsDevices' });

const devices = ref<SessionApi.DeviceItem[]>([]);
const sessions = ref<SessionApi.SessionItem[]>([]);
const current = ref<null | SessionApi.CurrentSession>(null);
const loading = ref(false);

async function load() {
  loading.value = true;
  try {
    const [deviceList, sessionList, currentSession] = await Promise.all([
      listMyDevicesApi(),
      listMySessionsApi(false),
      // 改造前签发的凭据没有 sid，/auth/session 会返回 401；
      // 这只影响顶部概览，不该让设备与会话列表一起加载失败。
      getSessionApi().catch(() => null),
    ]);
    devices.value = deviceList ?? [];
    sessions.value = sessionList ?? [];
    current.value = currentSession ?? null;
  } finally {
    loading.value = false;
  }
}

async function signOut(row: SessionApi.SessionItem) {
  await ElMessageBox.confirm(
    row.isCurrent
      ? '这是当前会话，退出后需要重新登录。确定继续吗？'
      : '确定退出这个会话吗？对应设备上的登录会立即失效。',
    '退出会话',
    { type: 'warning' },
  );
  await revokeMySessionApi(row.sid, 'self-revoke');
  ElMessage.success('会话已退出');
  await load();
}

async function removeDevice(row: SessionApi.DeviceItem) {
  await ElMessageBox.confirm(
    `确定移除「${deviceText(row.device)}」的受信状态吗？该设备上的所有会话会被吊销，下次登录需要重新验证。`,
    '移除受信设备',
    { type: 'warning' },
  );
  await revokeMyDeviceApi(row.deviceId, 'self-revoke-device');
  ElMessage.success('设备已移除');
  await load();
}

onMounted(load);
</script>

<template>
  <div>
    <ElCard v-if="current" class="mb-4" shadow="never">
      <template #header>当前登录</template>
      <ElDescriptions :column="3" border size="small">
        <ElDescriptionsItem label="登录方式">
          {{ loginMethodText(current.loginMethod) }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="设备">
          {{ deviceText(current.device) }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="已接入子系统">
          <ElTag
            v-for="client in current.clients"
            :key="client"
            class="mr-1"
            size="small"
            type="info"
          >
            {{ client }}
          </ElTag>
          <span v-if="!current.clients?.length" class="text-gray-400">-</span>
        </ElDescriptionsItem>
        <ElDescriptionsItem label="登录时间">
          {{ formatTime(current.createdAtUtc) }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="最近活动">
          {{ formatTime(current.lastActivityAtUtc) }}
        </ElDescriptionsItem>
        <ElDescriptionsItem label="过期时间">
          {{ formatTime(current.expiresAtUtc) }}
        </ElDescriptionsItem>
      </ElDescriptions>
    </ElCard>

    <ElCard class="mb-4" shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <span>受信设备</span>
          <ElButton size="small" @click="load">刷新</ElButton>
        </div>
      </template>
      <ElTable v-loading="loading" :data="devices" row-key="deviceId" stripe>
        <ElTableColumn type="expand">
          <template #default="{ row }">
            <div class="px-8 py-2">
              <div
                v-for="session in (row as SessionApi.DeviceItem).sessions"
                :key="session.sid"
                class="flex items-center justify-between border-b py-2 last:border-0"
              >
                <div class="text-sm">
                  <span>
                    {{ formatTime(session.createdAtUtc) }} 登录 ·
                    {{ loginMethodText(session.loginMethod) }} ·
                    {{ session.ipAddress || '未知 IP' }}
                  </span>
                  <ElTag v-if="session.isCurrent" class="ml-2" size="small">
                    当前会话
                  </ElTag>
                  <ElTag
                    v-if="session.isRevoked"
                    class="ml-1"
                    size="small"
                    type="danger"
                  >
                    已吊销
                  </ElTag>
                </div>
                <ElButton
                  :disabled="session.isRevoked"
                  size="small"
                  type="danger"
                  @click="signOut(session)"
                >
                  退出
                </ElButton>
              </div>
              <div
                v-if="!row.sessions?.length"
                class="py-2 text-sm text-gray-400"
              >
                该设备当前没有活跃会话
              </div>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="设备" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">
            <div>{{ deviceText(row.device) }}</div>
            <div class="text-xs text-gray-400">
              {{ row.device?.platform || '-' }} ·
              {{ row.device?.timezone || '-' }}
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="设备 ID" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">{{ row.deviceId }}</template>
        </ElTableColumn>
        <ElTableColumn align="center" label="状态" width="170">
          <template #default="{ row }">
            <ElTag :type="deviceStatusTagType(row.status)" size="small">
              {{ deviceStatusLabel(row.status) }}
            </ElTag>
            <ElTag v-if="row.isCurrentDevice" class="ml-1" size="small">
              本机
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="活跃会话" width="90">
          <template #default="{ row }">{{ row.activeSessionCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="首次登录" width="170">
          <template #default="{ row }">
            {{ formatTime(row.firstSeenUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="最近使用" width="170">
          <template #default="{ row }">
            {{ formatTime(row.lastSeenUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="移除时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.revokedAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn fixed="right" label="操作" width="110">
          <template #default="{ row }">
            <ElButton
              :disabled="!row.isTrusted"
              size="small"
              type="danger"
              @click="removeDevice(row as SessionApi.DeviceItem)"
            >
              移除信任
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <ElCard shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <span>登录会话</span>
          <ElButton size="small" @click="load">刷新</ElButton>
        </div>
      </template>
      <ElTable v-loading="loading" :data="sessions" stripe>
        <ElTableColumn label="设备" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            {{ deviceText(row.device) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="登录方式" width="100">
          <template #default="{ row }">
            {{ loginMethodText(row.loginMethod) }}
          </template>
        </ElTableColumn>
        <ElTableColumn
          label="IP"
          prop="ipAddress"
          show-overflow-tooltip
          width="130"
        />
        <ElTableColumn label="登录时间" width="170">
          <template #default="{ row }">
            {{ formatTime(row.createdAtUtc) }}
          </template>
        </ElTableColumn>
        <ElTableColumn label="最近活动" width="170">
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
        <ElTableColumn fixed="right" label="操作" width="90">
          <template #default="{ row }">
            <ElButton
              :disabled="row.isRevoked"
              size="small"
              type="danger"
              @click="signOut(row as SessionApi.SessionItem)"
            >
              退出
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>
  </div>
</template>
