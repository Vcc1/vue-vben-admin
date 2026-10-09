<script lang="ts" setup>
import { onMounted, ref } from 'vue';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { useAccess } from '@vben/access';

import {
  ElButton,
  ElCard,
  ElMessage,
  ElMessageBox,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  type NotificationTypeDto,
  type NotificationTypeInput,
  createNotificationTypeApi,
  deleteNotificationTypeApi,
  getNotificationTypesApi,
  updateNotificationTypeApi,
} from '#/api/notification';
import { useVbenForm } from '#/adapter/form';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:notification:create']);
const canUpdate = hasAccessByCodes(['auth:admin:notification:update']);
const canDelete = hasAccessByCodes(['auth:admin:notification:delete']);

const loading = ref(false);
const types = ref<NotificationTypeDto[]>([]);
const editingType = ref<NotificationTypeDto | null>(null);
const isCreate = ref(true);

const [Form, formApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  schema: [
    {
      component: 'Input',
      fieldName: 'typeCode',
      label: '类型代码',
      rules: 'required',
      componentProps: {
        placeholder: '如 account_security',
      },
    },
    {
      component: 'Input',
      fieldName: 'displayName',
      label: '显示名称',
      rules: 'required',
      componentProps: {
        placeholder: '如 账户安全',
      },
    },
    {
      component: 'Input',
      fieldName: 'description',
      label: '描述',
      componentProps: {
        type: 'textarea',
        rows: 2,
        placeholder: '可选，描述该类型通知的用途',
      },
    },
    {
      component: 'Switch',
      fieldName: 'isEnabled',
      label: '启用',
      defaultValue: true,
    },
  ],
  showDefaultActions: false,
});

const [Drawer, drawerApi] = useVbenDrawer({
  onOpenChange(isOpen) {
    if (!isOpen) {
      editingType.value = null;
    }
  },
});

async function loadTypes() {
  loading.value = true;
  try {
    types.value = await getNotificationTypesApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  isCreate.value = true;
  drawerApi.open();
  formApi.resetForm();
  formApi.setValues({ isEnabled: true });
}

function openEdit(type: NotificationTypeDto) {
  isCreate.value = false;
  editingType.value = type;
  drawerApi.open();
  formApi.setValues({
    description: type.description || '',
    displayName: type.displayName,
    isEnabled: type.isEnabled,
    typeCode: type.typeCode,
  });
}

async function handleSubmit() {
  const values = await formApi.getValues();

  const input: NotificationTypeInput = {
    allowedChannels: ['in_app'],
    description: values.description || null,
    displayName: values.displayName,
    isEnabled: values.isEnabled,
    typeCode: values.typeCode,
  };

  if (isCreate.value) {
    await createNotificationTypeApi(input);
    ElMessage.success('通知类型创建成功');
  } else {
    await updateNotificationTypeApi(editingType.value!.id, input);
    ElMessage.success('通知类型更新成功');
  }
  drawerApi.close();
  await loadTypes();
}

async function handleDelete(type: NotificationTypeDto) {
  await ElMessageBox.confirm(
    `确定要删除通知类型 "${type.displayName}" 吗？`,
    '确认',
    { type: 'warning' },
  );
  await deleteNotificationTypeApi(type.id);
  ElMessage.success('通知类型删除成功');
  await loadTypes();
}

onMounted(loadTypes);
</script>

<template>
  <Page title="通知类型管理" description="管理系统中的通知类型">
    <ElCard>
      <template #header>
        <div class="flex items-center justify-between">
          <span>类型列表</span>
          <ElButton v-if="canCreate" type="primary" @click="openCreate">新建类型</ElButton>
        </div>
      </template>

      <ElTable :data="types" v-loading="loading" stripe>
        <ElTableColumn prop="typeCode" label="类型代码" width="180" />
        <ElTableColumn prop="displayName" label="显示名称" width="180" />
        <ElTableColumn prop="description" label="描述" min-width="200" show-overflow-tooltip />
        <ElTableColumn label="渠道" width="120">
          <template #default>
            <ElTag size="small">站内信</ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="启用" width="90" align="center">
          <template #default="{ row }">
            <ElTag :type="row.isEnabled ? 'success' : 'info'" size="small">
              {{ row.isEnabled ? '是' : '否' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="系统" width="80" align="center">
          <template #default="{ row }">
            <ElTag v-if="row.isSystem" size="small" type="warning">是</ElTag>
            <span v-else class="text-gray-400">否</span>
          </template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <ElButton v-if="canUpdate" size="small" @click="openEdit(row as NotificationTypeDto)">编辑</ElButton>
            <ElButton
              v-if="canDelete"
              size="small"
              type="danger"
              :disabled="row.isSystem"
              @click="handleDelete(row as NotificationTypeDto)"
            >
              删除
            </ElButton>
          </template>
        </ElTableColumn>
      </ElTable>
    </ElCard>

    <Drawer :title="isCreate ? '新建通知类型' : '编辑通知类型'" class="w-120">
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
