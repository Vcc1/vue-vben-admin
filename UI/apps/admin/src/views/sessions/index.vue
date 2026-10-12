<script lang="ts" setup>
import type { SessionApi } from '#/api/session';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page, useVbenDrawer } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElInput,
  ElMessage,
  ElMessageBox,
  ElSwitch,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { listSessionsApi, revokeSessionApi } from '#/api/session';

import DeviceUsageDetail from './device-usage.vue';
import {
  deviceText,
  formatTime,
  levelTagType,
  loginMethodText,
} from './format';
import UserSessionDetail from './user-detail.vue';

defineOptions({ name: 'AdminSessions' });

const { hasAccessByCodes } = useAccess();
const canRevoke = hasAccessByCodes(['auth:admin:sessions:revoke']);

interface DeviceGroup {
  deviceId: string;
  device: SessionApi.DeviceInfo | null | undefined;
  sessions: SessionApi.SessionItem[];
  activeCount: number;
  hasOnline: boolean;
  lastActivityUtc: string;
}

const filter = reactive({
  deviceKeyword: '',
  includeRevoked: false,
  keyword: '',
});

const rows = ref<SessionApi.SessionItem[]>([]);
const loading = ref(false);

const keyword = computed(() => filter.keyword.trim().toLowerCase());

const visibleRows = computed(() =>
  keyword.value
    ? rows.value.filter((row) =>
        [row.displayName, row.userName, row.ipAddress, row.deviceId]
          .filter(Boolean)
          .some((value) => String(value).toLowerCase().includes(keyword.value)),
      )
    : rows.value,
);

const deviceGroups = computed<DeviceGroup[]>(() => {
  const grouped = new Map<string, DeviceGroup>();
  const noDevice: SessionApi.SessionItem[] = [];

  for (const session of visibleRows.value) {
    if (!session.deviceId) {
      noDevice.push(session);
      continue;
    }
    let group = grouped.get(session.deviceId);
    if (!group) {
      group = {
        deviceId: session.deviceId,
        device: session.device,
        sessions: [],
        activeCount: 0,
        hasOnline: false,
        lastActivityUtc: session.lastActivityAtUtc,
      };
      grouped.set(session.deviceId, group);
    }
    group.sessions.push(session);
    if (!session.isRevoked) group.activeCount++;
    if (session.isOnline) group.hasOnline = true;
    if (session.lastActivityAtUtc > group.lastActivityUtc) {
      group.lastActivityUtc = session.lastActivityAtUtc;
    }
  }

  const result = [...grouped.values()].sort(
    (a, b) =>
      new Date(b.lastActivityUtc).getTime() -
      new Date(a.lastActivityUtc).getTime(),
  );

  if (noDevice.length > 0) {
    result.push({
      deviceId: '',
      device: noDevice[0]?.device,
      sessions: noDevice,
      activeCount: noDevice.filter((s) => !s.isRevoked).length,
      hasOnline: noDevice.some((s) => s.isOnline),
      lastActivityUtc: noDevice.reduce(
        (latest, s) =>
          s.lastActivityAtUtc > latest ? s.lastActivityAtUtc : latest,
        noDevice[0]!.lastActivityAtUtc,
      ),
    });
  }

  return result;
});

async function load() {
  loading.value = true;
  try {
    rows.value = await listSessionsApi({
      includeRevoked: filter.includeRevoked,
    });
  } finally {
    loading.value = false;
  }
}

const [DetailDrawer, detailDrawerApi] = useVbenDrawer();
const detail = ref<null | SessionApi.SessionItem>(null);

function openDetail(row: SessionApi.SessionItem) {
  detail.value = row;
  detailDrawerApi.open();
}

const [UserDrawer, userDrawerApi] = useVbenDrawer();
const userTarget = ref<null | { displayName: string; userId: string }>(null);

const [DeviceDrawer, deviceDrawerApi] = useVbenDrawer();
const deviceTarget = ref('');

function openUser(row: { displayName: string; userId: string }) {
  userTarget.value = { displayName: row.displayName, userId: row.userId };
  deviceDrawerApi.close();
  userDrawerApi.open();
}

function openDevice(deviceId?: null | string) {
  const target = (deviceId ?? filter.deviceKeyword).trim();
  if (!target) {
    ElMessage.warning('请先输入设备 ID');
    return;
  }
  deviceTarget.value = target;
  userDrawerApi.close();
  deviceDrawerApi.open();
}

async function revoke(row: SessionApi.SessionItem) {
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
  ElMessage.success('已强制下线，并向相关子系统推送注销通知');
  await load();
}

onMounted(load);
</script>

<template>
  <Page description="按设备聚合查看所有会话及相关数据" title="会话管理">
    <ElCard>
      <div class="mb-3 flex flex-wrap items-center gap-2">
        <ElInput
          v-model="filter.keyword"
          class="!w-56"
          clearable
          placeholder="按用户名 / IP / 设备筛选"
        />
        <div class="flex items-center gap-1 text-sm">
          <span>包含已吊销</span>
          <ElSwitch v-model="filter.includeRevoked" @change="load" />
        </div>
        <ElButton type="primary" @click="load">刷新</ElButton>

        <div class="mx-2 h-5 w-px bg-gray-200 dark:bg-gray-700"></div>

        <ElInput
          v-model="filter.deviceKeyword"
          class="!w-72"
          clearable
          placeholder="输入设备 ID，查这台设备被谁登录过"
          @keyup.enter="openDevice()"
        />
        <ElButton @click="openDevice()">查设备使用记录</ElButton>
      </div>

      <ElTable v-loading="loading" :data="deviceGroups" stripe>
        <ElTableColumn type="expand">
          <template #default="{ row }">
            <div class="px-4 py-2">
              <ElTable :data="(row as DeviceGroup).sessions" size="small" stripe>
                <ElTableColumn label="用户" min-width="140" show-overflow-tooltip>
                  <template #default="{ row: session }">
                    <ElButton
                      link
                      type="primary"
                      @click="openUser(session as SessionApi.SessionItem)"
                    >
                      {{ session.displayName }}
                    </ElButton>
                    <div class="text-xs text-gray-400">
                      {{ session.userName }}
                    </div>
                  </template>
                </ElTableColumn>
                <ElTableColumn align="center" label="等级" width="70">
                  <template #default="{ row: session }">
                    <ElTag :type="levelTagType(session.level)" size="small">
                      L{{ session.level }}
                    </ElTag>
                  </template>
                </ElTableColumn>
                <ElTableColumn label="登录方式" width="90">
                  <template #default="{ row: session }">
                    {{ loginMethodText(session.loginMethod) }}
                  </template>
                </ElTableColumn>
                <ElTableColumn label="子系统" min-width="120">
                  <template #default="{ row: session }">
                    <ElTag
                      v-for="client in session.clients"
                      :key="client"
                      class="mr-1"
                      size="small"
                      type="info"
                    >
                      {{ client }}
                    </ElTag>
                    <span
                      v-if="!session.clients?.length"
                      class="text-gray-400"
                    >
                      -
                    </span>
                  </template>
                </ElTableColumn>
                <ElTableColumn
                  label="IP"
                  prop="ipAddress"
                  show-overflow-tooltip
                  width="120"
                />
                <ElTableColumn label="登录时间" width="160">
                  <template #default="{ row: session }">
                    {{ formatTime(session.createdAtUtc) }}
                  </template>
                </ElTableColumn>
                <ElTableColumn label="最近活动" width="160">
                  <template #default="{ row: session }">
                    {{ formatTime(session.lastActivityAtUtc) }}
                  </template>
                </ElTableColumn>
                <ElTableColumn align="center" label="状态" width="120">
                  <template #default="{ row: session }">
                    <ElTag v-if="session.isRevoked" size="small" type="danger">
                      已吊销
                    </ElTag>
                    <ElTag
                      v-else-if="session.isOnline"
                      size="small"
                      type="success"
                    >
                      在线
                    </ElTag>
                    <ElTag v-else size="small" type="info">离线</ElTag>
                    <ElTag
                      v-if="session.isCurrent"
                      class="ml-1"
                      size="small"
                      type="primary"
                    >
                      本机
                    </ElTag>
                  </template>
                </ElTableColumn>
                <ElTableColumn fixed="right" label="操作" width="140">
                  <template #default="{ row: session }">
                    <ElButton
                      size="small"
                      @click="openDetail(session as SessionApi.SessionItem)"
                    >
                      详情
                    </ElButton>
                    <ElButton
                      v-if="canRevoke"
                      :disabled="session.isRevoked"
                      size="small"
                      type="danger"
                      @click="revoke(session as SessionApi.SessionItem)"
                    >
                      踢下线
                    </ElButton>
                  </template>
                </ElTableColumn>
              </ElTable>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn label="设备" min-width="240" show-overflow-tooltip>
          <template #default="{ row }">
            <div>{{ deviceText(row.device) }}</div>
            <ElButton
              v-if="row.deviceId"
              link
              type="primary"
              @click="openDevice(row.deviceId)"
            >
              <span class="text-xs">{{ row.deviceId }}</span>
            </ElButton>
            <span v-else class="text-xs text-gray-400">无设备标识</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="设备信息" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <div class="text-sm">
              {{ row.device?.platform || '-' }} ·
              {{ row.device?.timezone || '-' }}
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="会话数" width="80">
          <template #default="{ row }">
            {{ row.sessions.length }}
            <span v-if="row.activeCount < row.sessions.length" class="text-xs text-gray-400">
              ({{ row.activeCount }} 活跃)
            </span>
          </template>
        </ElTableColumn>
        <ElTableColumn align="center" label="状态" width="80">
          <template #default="{ row }">
            <ElTag v-if="row.hasOnline" size="small" type="success">
              在线
            </ElTag>
            <ElTag v-else size="small" type="info">离线</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="最近活动" width="170">
          <template #default="{ row }">
            {{ formatTime(row.lastActivityUtc) }}
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <DetailDrawer class="w-180" title="会话详情">
      <template v-if="detail">
        <ElDescriptions :column="1" border size="small">
          <ElDescriptionsItem label="会话 ID">
            <span class="break-all">{{ detail.sid }}</span>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="用户">
            {{ detail.displayName }}（{{ detail.userName }}）
          </ElDescriptionsItem>
          <ElDescriptionsItem label="当前等级">
            <ElTag :type="levelTagType(detail.level)" size="small">
              L{{ detail.level }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="登录方式">
            {{ loginMethodText(detail.loginMethod) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="设备">
            {{ deviceText(detail.device) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="设备 ID">
            <ElButton
              v-if="detail.deviceId"
              link
              type="primary"
              @click="openDevice(detail.deviceId)"
            >
              <span class="break-all">{{ detail.deviceId }}</span>
            </ElButton>
            <span v-else>-</span>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="IP">
            {{ detail.ipAddress || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="登录时间">
            {{ formatTime(detail.createdAtUtc) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="最近活动">
            {{ formatTime(detail.lastActivityAtUtc) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="过期时间">
            {{ formatTime(detail.expiresAtUtc) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="吊销信息">
            {{
              detail.isRevoked
                ? `${formatTime(detail.revokedAtUtc)} · ${detail.revokedReason || '-'}`
                : '未吊销'
            }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="User-Agent">
            <span class="break-all">{{ detail.userAgent || '-' }}</span>
          </ElDescriptionsItem>
        </ElDescriptions>
      </template>
    </DetailDrawer>

    <UserDrawer
      :footer="false"
      :title="`用户登录情况 · ${userTarget?.displayName || ''}`"
      class="w-[80%]"
    >
      <UserSessionDetail
        v-if="userTarget"
        :display-name="userTarget.displayName"
        :user-id="userTarget.userId"
      />
    </UserDrawer>

    <DeviceDrawer :footer="false" title="设备使用记录" class="w-[70%]">
      <DeviceUsageDetail
        v-if="deviceTarget"
        :device-id="deviceTarget"
        @view-user="openUser"
      />
    </DeviceDrawer>
  </Page>
</template>
