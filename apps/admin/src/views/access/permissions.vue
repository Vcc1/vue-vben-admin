<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import {
  ElButton, ElDialog, ElForm, ElFormItem, ElInput, ElMessage,
  ElMessageBox, ElSwitch, ElTable, ElTableColumn, ElTag,
} from 'element-plus';

import {
  type PermissionNode, createPermissionApi, deletePermissionApi,
  listPermissionsApi, updatePermissionApi,
} from '#/api/admin-access';

import { reportAccessError } from './error';
import AccessTransfer from './access-transfer.vue';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:permissions:create']);
const canUpdate = hasAccessByCodes(['auth:admin:permissions:update']);
const canDelete = hasAccessByCodes(['auth:admin:permissions:delete']);

const permissions = ref<PermissionNode[]>([]);
const loading = ref(false);
const saving = ref(false);
const search = ref('');
const dialogOpen = ref(false);
const editing = ref<PermissionNode | null>(null);
const form = reactive({ code: '', name: '', description: '', isEnabled: true });

const visible = computed(() => {
  const query = search.value.trim().toLocaleLowerCase();
  if (!query) return permissions.value;
  const filter = (nodes: PermissionNode[]): PermissionNode[] => nodes.flatMap((node) => {
    if (`${node.code} ${node.name} ${node.description || ''}`.toLocaleLowerCase().includes(query)) {
      return [node];
    }
    const children = filter(node.children || []);
    return children.length ? [{ ...node, children }] : [];
  });
  return filter(permissions.value);
});

async function load() {
  loading.value = true;
  try {
    permissions.value = await listPermissionsApi();
  } catch (error) {
    reportAccessError(error);
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editing.value = null;
  Object.assign(form, { code: '', name: '', description: '', isEnabled: true });
  dialogOpen.value = true;
}

function openEdit(node: PermissionNode) {
  editing.value = node;
  Object.assign(form, {
    code: node.code, name: node.name,
    description: node.description || '', isEnabled: node.isEnabled,
  });
  dialogOpen.value = true;
}

async function save() {
  const code = form.code.trim();
  if (!editing.value && (!code || code.includes('*') || code.startsWith(':') || code.endsWith(':') || code.includes('::'))) {
    ElMessage.warning('权限码应为冒号分隔的具体权限，不能包含 * 或空层级');
    return;
  }
  if (!form.name.trim()) {
    ElMessage.warning('请输入权限名称');
    return;
  }
  saving.value = true;
  try {
    const data = { name: form.name.trim(), description: form.description.trim() || null, isEnabled: form.isEnabled };
    if (editing.value) {
      await updatePermissionApi(editing.value.code, data);
    } else {
      await createPermissionApi({ code, ...data });
    }
    dialogOpen.value = false;
    ElMessage.success('权限已保存');
    await load();
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

async function remove(node: PermissionNode) {
  try {
    await ElMessageBox.confirm(
      `删除「${node.code}」会移除该权限的精确角色授权，确定继续吗？`,
      '删除权限', { type: 'warning' },
    );
  } catch { return; }
  try {
    await deletePermissionApi(node.code);
    ElMessage.success('权限已删除');
    await load();
  } catch (error) {
    reportAccessError(error);
  }
}

onMounted(load);
</script>

<template>
  <Page title="权限管理">
    <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
      <ElInput v-model="search" clearable placeholder="搜索权限码、名称或说明" class="!w-80" />
      <div class="flex flex-wrap gap-2">
        <AccessTransfer kind="permissions" @applied="load" />
        <ElButton v-if="canCreate" type="primary" @click="openCreate">新建权限</ElButton>
      </div>
    </div>
    <ElTable :data="visible" v-loading="loading" row-key="code" :tree-props="{ children: 'children' }" default-expand-all stripe class="w-full">
      <ElTableColumn prop="name" label="名称" min-width="180" />
      <ElTableColumn prop="code" label="权限码" min-width="260" show-overflow-tooltip />
      <ElTableColumn prop="description" label="说明" min-width="180" show-overflow-tooltip />
      <ElTableColumn label="状态" width="100">
        <template #default="{ row }">
          <ElTag v-if="row.isEnabled === null" size="small" type="info">目录</ElTag>
          <ElTag v-else :type="row.isEnabled ? 'success' : 'info'" size="small">{{ row.isEnabled ? '启用' : '停用' }}</ElTag>
        </template>
      </ElTableColumn>
      <ElTableColumn label="操作" width="130" fixed="right">
        <template #default="{ row }">
          <template v-if="row.isEnabled !== null">
            <ElButton v-if="canUpdate" link type="primary" @click="openEdit(row as PermissionNode)">编辑</ElButton>
            <ElButton v-if="canDelete" link type="danger" @click="remove(row as PermissionNode)">删除</ElButton>
          </template>
        </template>
      </ElTableColumn>
    </ElTable>

    <ElDialog v-model="dialogOpen" :title="editing ? '编辑权限' : '新建权限'" width="520px" destroy-on-close>
      <ElForm :model="form" label-width="90px">
        <ElFormItem label="权限码" required>
          <ElInput v-model="form.code" :disabled="!!editing" maxlength="100" placeholder="如 paycardhub:cards:read" />
        </ElFormItem>
        <ElFormItem label="名称" required><ElInput v-model="form.name" maxlength="200" /></ElFormItem>
        <ElFormItem label="说明"><ElInput v-model="form.description" type="textarea" :rows="3" maxlength="500" show-word-limit /></ElFormItem>
        <ElFormItem label="启用"><ElSwitch v-model="form.isEnabled" /></ElFormItem>
      </ElForm>
      <template #footer><ElButton @click="dialogOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="save">保存</ElButton></template>
    </ElDialog>
  </Page>
</template>
