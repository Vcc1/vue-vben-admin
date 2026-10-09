<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
} from 'element-plus';

import {
  type CreateOidcScopeRequest,
  type OidcScopeItem,
  createOidcScopeApi,
  deleteOidcScopeApi,
  listOidcScopesApi,
} from '#/api/oidc';
import { useVbenForm } from '#/adapter/form';
import { useVbenDrawer } from '@vben/common-ui';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:oidc:scopes:create']);
const canDelete = hasAccessByCodes(['auth:admin:oidc:scopes:delete']);

const loading = ref(false);
const scopes = ref<OidcScopeItem[]>([]);

const [Form, formApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  schema: [
    {
      component: 'Input',
      fieldName: 'name',
      label: '名称',
      rules: 'required',
      componentProps: {
        placeholder: '如 openid, profile, api',
      },
    },
    {
      component: 'Input',
      fieldName: 'displayName',
      label: '显示名称',
      rules: 'required',
    },
    {
      component: 'Input',
      fieldName: 'description',
      label: '描述',
      componentProps: {
        type: 'textarea',
        rows: 3,
      },
    },
  ],
  showDefaultActions: false,
});

const [Drawer, drawerApi] = useVbenDrawer();

async function loadScopes() {
  loading.value = true;
  try {
    scopes.value = await listOidcScopesApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  drawerApi.open();
  formApi.resetForm();
}

async function handleSubmit() {
  const values = await formApi.getValues();
  await createOidcScopeApi(values as CreateOidcScopeRequest);
  ElMessage.success('作用域创建成功');
  drawerApi.close();
  await loadScopes();
}

async function handleDelete(scope: OidcScopeItem) {
  await ElMessageBox.confirm(
    `确定要删除作用域 "${scope.name}" 吗？`,
    '确认',
    { type: 'warning' },
  );
  await deleteOidcScopeApi(scope.id);
  ElMessage.success('作用域删除成功');
  await loadScopes();
}

onMounted(loadScopes);
</script>

<template>
  <Page title="OIDC 作用域" description="管理 OpenID Connect 作用域">
    <ElCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span>作用域列表</span>
          <ElButton v-if="canCreate" type="primary" @click="openCreate">新建作用域</ElButton>
        </div>
      </template>

      <ElTable :data="scopes" v-loading="loading" stripe>
        <ElTableColumn prop="name" label="名称" width="200" />
        <ElTableColumn prop="displayName" label="显示名称" width="200" />
        <ElTableColumn prop="description" label="描述" />
        <ElTableColumn label="操作" width="100" fixed="right">
          <template #default="{ row }">
            <ElButton v-if="canDelete" size="small" type="danger" @click="handleDelete(row)">
              删除
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <Drawer title="新建作用域" class="w-120">
      <Form />
      <template #footer>
        <ElButton @click="drawerApi.close()">取消</ElButton>
        <ElButton type="primary" @click="handleSubmit">创建</ElButton>
      </template>
    </Drawer>
  </Page>
</template>
