<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page, useVbenDrawer } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
  ElTag,
  ElTooltip,
} from 'element-plus';

import {
  type CreateOidcClientRequest,
  type OidcClientItem,
  type UpdateOidcClientRequest,
  createOidcClientApi,
  deleteOidcClientApi,
  listOidcClientsApi,
  regenerateOidcClientSecretApi,
  updateOidcClientApi,
} from '#/api/oidc';
import { useVbenForm } from '#/adapter/form';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:oidc:clients:create']);
const canUpdate = hasAccessByCodes(['auth:admin:oidc:clients:update']);
const canDelete = hasAccessByCodes(['auth:admin:oidc:clients:delete']);

const loading = ref(false);
const clients = ref<OidcClientItem[]>([]);
const editingClient = ref<OidcClientItem | null>(null);
const isCreate = ref(true);

const clientTypeOptions = [
  { label: '机密客户端', value: 'confidential' },
  { label: '公开客户端', value: 'public' },
];

const consentTypeOptions = [
  { label: '无需同意', value: 'none' },
  { label: '静默授权', value: 'simple' },
  { label: '需用户同意', value: 'explicit' },
  { label: '每次均需同意', value: 'systematic' },
  { label: '外部处理', value: 'external' },
];

const grantTypeOptions = [
  { label: '授权码', value: 'authorization_code' },
  { label: '客户端凭证', value: 'client_credentials' },
  { label: '密码', value: 'password' },
  { label: '刷新令牌', value: 'refresh_token' },
  { label: '隐式', value: 'implicit' },
];

const fieldTooltips: Record<string, string> = {
  clientId: '客户端的唯一标识符',
  displayName: '客户端的显示名称',
  clientType: '机密客户端需要密钥，公开客户端无需密钥',
  consentType: '用户授权同意的方式',
  clientSecret: '机密客户端的密钥',
  redirectUris: '授权成功后的回调地址',
  postLogoutRedirectUris: '登出后的回调地址',
  grantTypes: '允许的 OAuth2 授权类型',
  scopes: '允许请求的权限范围，如 openid、profile、email',
};

const [Form, formApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  schema: [
    {
      component: 'Input',
      fieldName: 'clientId',
      label: '客户端 ID',
      help: fieldTooltips.clientId,
      rules: 'required',
    },
    {
      component: 'Input',
      fieldName: 'displayName',
      label: '显示名称',
      help: fieldTooltips.displayName,
      rules: 'required',
    },
    {
      component: 'Select',
      componentProps: {
        options: clientTypeOptions,
      },
      fieldName: 'clientType',
      label: '客户端类型',
      help: fieldTooltips.clientType,
      rules: 'required',
    },
    {
      component: 'Select',
      componentProps: {
        options: consentTypeOptions,
      },
      fieldName: 'consentType',
      label: '同意类型',
      help: fieldTooltips.consentType,
      rules: 'required',
    },
    {
      component: 'Input',
      fieldName: 'clientSecret',
      label: '客户端密钥',
      help: fieldTooltips.clientSecret,
      dependencies: {
        if(values) {
          return values.clientType === 'confidential';
        },
        triggerFields: ['clientType'],
      },
    },
    {
      component: 'Input',
      componentProps: {
        type: 'textarea',
        rows: 3,
        placeholder: '多个 URI，每行一个',
      },
      fieldName: 'redirectUris',
      label: '重定向地址',
      help: fieldTooltips.redirectUris,
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        type: 'textarea',
        rows: 3,
        placeholder: '多个 URI，每行一个',
      },
      fieldName: 'postLogoutRedirectUris',
      label: '登出回调',
      help: fieldTooltips.postLogoutRedirectUris,
    },
    {
      component: 'Select',
      componentProps: {
        multiple: true,
        options: grantTypeOptions,
      },
      fieldName: 'grantTypes',
      label: '授权类型',
      help: fieldTooltips.grantTypes,
      rules: 'required',
    },
    {
      component: 'Input',
      componentProps: {
        type: 'textarea',
        rows: 2,
        placeholder: '多个 Scope，每行一个（如 openid, profile, email）',
      },
      fieldName: 'scopes',
      label: '作用域',
      help: fieldTooltips.scopes,
      rules: 'required',
    },
  ],
  showDefaultActions: false,
});

const [Drawer, drawerApi] = useVbenDrawer({
  onOpenChange(isOpen) {
    if (!isOpen) {
      editingClient.value = null;
    }
  },
});

async function loadClients() {
  loading.value = true;
  try {
    clients.value = await listOidcClientsApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  isCreate.value = true;
  drawerApi.open();
  formApi.resetForm();
  formApi.setValues({
    clientType: 'public',
    consentType: 'none',
    grantTypes: ['authorization_code'],
    scopes: 'openid',
  });
}

function openEdit(client: OidcClientItem) {
  isCreate.value = false;
  editingClient.value = client;
  drawerApi.open();
  formApi.setValues({
    clientId: client.clientId,
    clientType: client.clientType,
    consentType: client.consentType,
    displayName: client.displayName,
    grantTypes: client.grantTypes,
    postLogoutRedirectUris: client.postLogoutRedirectUris.join('\n'),
    redirectUris: client.redirectUris.join('\n'),
    scopes: client.scopes.join('\n'),
  });
}

function textToLines(text: string): string[] {
  return text
    .split('\n')
    .map((s) => s.trim())
    .filter(Boolean);
}

async function handleSubmit() {
  const values = await formApi.getValues();
  const data = {
    clientType: values.clientType,
    consentType: values.consentType,
    displayName: values.displayName,
    grantTypes: values.grantTypes,
    postLogoutRedirectUris: textToLines(values.postLogoutRedirectUris || ''),
    redirectUris: textToLines(values.redirectUris || ''),
    scopes: textToLines(values.scopes || ''),
  };

  if (isCreate.value) {
    await createOidcClientApi({
      ...data,
      clientId: values.clientId,
      clientSecret: values.clientSecret || undefined,
      clientType: values.clientType,
    } as CreateOidcClientRequest);
    ElMessage.success('客户端创建成功');
  } else {
    await updateOidcClientApi(
      editingClient.value!.id,
      data as UpdateOidcClientRequest,
    );
    ElMessage.success('客户端更新成功');
  }
  drawerApi.close();
  await loadClients();
}

async function handleDelete(client: OidcClientItem) {
  await ElMessageBox.confirm(
    `确定要删除客户端 "${client.displayName}" (${client.clientId}) 吗？`,
    '确认',
    { type: 'warning' },
  );
  await deleteOidcClientApi(client.id);
  ElMessage.success('客户端删除成功');
  await loadClients();
}

async function handleRegenerateSecret(client: OidcClientItem) {
  await ElMessageBox.confirm(
    `确定要重置 "${client.displayName}" 的密钥吗？旧密钥将失效。`,
    '确认',
    { type: 'warning' },
  );
  const { secret } = await regenerateOidcClientSecretApi(client.id);
  ElMessage.success(`新密钥：${secret}`);
}

onMounted(loadClients);
</script>

<template>
  <Page title="OIDC 客户端" description="管理 OpenID Connect 客户端应用">
    <ElCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span>客户端列表</span>
          <ElButton v-if="canCreate" type="primary" @click="openCreate">新建客户端</ElButton>
        </div>
      </template>

      <ElTable :data="clients" v-loading="loading" stripe>
        <ElTableColumn prop="clientId" width="180">
          <template #header>
            <ElTooltip content="Client ID - 客户端唯一标识符" placement="top">
              <span>客户端 ID</span>
            </ElTooltip>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="displayName" width="200">
          <template #header>
            <ElTooltip content="Display Name - 客户端的显示名称" placement="top">
              <span>显示名称</span>
            </ElTooltip>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="clientType" width="150">
          <template #header>
            <ElTooltip content="Client Type - 机密客户端需要密钥，公开客户端无需密钥" placement="top">
              <span>客户端类型</span>
            </ElTooltip>
          </template>
          <template #default="{ row }">
            <ElTag :type="row.clientType === 'confidential' ? 'danger' : 'info'" size="small">
              {{ row.clientType === 'confidential' ? '机密' : '公开' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn min-width="200">
          <template #header>
            <ElTooltip content="Grant Types - 允许的 OAuth2 授权类型" placement="top">
              <span>授权类型</span>
            </ElTooltip>
          </template>
          <template #default="{ row }">
            <ElTag
              v-for="g in row.grantTypes"
              :key="g"
              size="small"
              class="mr-1"
            >
              {{ g }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn min-width="150">
          <template #header>
            <ElTooltip content="Scopes - 允许请求的权限范围" placement="top">
              <span>作用域</span>
            </ElTooltip>
          </template>
          <template #default="{ row }">
            <ElTag
              v-for="s in row.scopes"
              :key="s"
              size="small"
              type="success"
              class="mr-1"
            >
              {{ s }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn width="80">
          <template #header>
            <ElTooltip content="Secret - 客户端是否配置了密钥" placement="top">
              <span>密钥</span>
            </ElTooltip>
          </template>
          <template #default="{ row }">
            <ElTag v-if="row.hasClientSecret" size="small" type="warning">有</ElTag>
            <span v-else class="text-gray-400">无</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <ElButton v-if="canUpdate" size="small" @click="openEdit(row)">编辑</ElButton>
            <ElButton
              v-if="canUpdate && row.clientType === 'confidential'"
              size="small"
              type="warning"
              @click="handleRegenerateSecret(row)"
            >
              重置密钥
            </ElButton>
            <ElButton v-if="canDelete" size="small" type="danger" @click="handleDelete(row)">
              删除
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <Drawer title="OIDC 客户端" class="w-150">
      <Form />
      <template #footer>
        <ElButton @click="drawerApi.close()">取消</ElButton>
        <ElButton type="primary" @click="handleSubmit">
          {{ isCreate ? '创建' : '更新' }}
        </ElButton>
      </template>
    </Drawer>
  </Page>
</template>
