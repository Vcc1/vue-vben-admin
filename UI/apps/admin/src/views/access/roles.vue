<script lang="ts" setup>
import { computed, nextTick, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import {
  ElButton, ElCheckbox, ElDialog, ElInput, ElMessage, ElMessageBox,
  ElOption, ElSelect, ElTable, ElTableColumn, ElTag, ElTree,
} from 'element-plus';

import {
  type AdminRole, type PermissionNode,
  createRoleApi, deleteRoleApi, listPermissionsApi, listRolesApi,
  setRolePermissionsApi,
} from '#/api/admin-access';

import { reportAccessError } from './error';
import AccessTransfer from './access-transfer.vue';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:roles:create']);
const canAssign = hasAccessByCodes(['auth:admin:roles:assign']);
const canDelete = hasAccessByCodes(['auth:admin:roles:delete']);

const roles = ref<AdminRole[]>([]);
const tree = ref<PermissionNode[]>([]);
const loading = ref(false);
const saving = ref(false);
const createOpen = ref(false);
const editOpen = ref(false);
const roleName = ref('');
const selected = ref<AdminRole | null>(null);
const treeRef = ref<InstanceType<typeof ElTree>>();
const globalGrant = ref(false);
const wildcardGrants = ref<string[]>([]);
const newWildcard = ref('');

const flattened = computed(() => {
  const nodes: PermissionNode[] = [];
  const visit = (items: PermissionNode[]) => {
    for (const item of items) {
      nodes.push(item);
      visit(item.children || []);
    }
  };
  visit(tree.value);
  return nodes;
});

const leafPermissions = computed(() =>
  flattened.value.filter((n) => n.isEnabled !== null && (!n.children || n.children.length === 0)),
);

function getCheckedCount(): number {
  return (treeRef.value?.getCheckedKeys() || []).length;
}

const checkedCount = ref(0);

function updateCheckedCount() {
  checkedCount.value = getCheckedCount();
}

const isAllChecked = computed(
  () => leafPermissions.value.length > 0 && checkedCount.value === leafPermissions.value.length,
);

function toggleAllPermissions() {
  if (!treeRef.value) return;
  if (isAllChecked.value) {
    treeRef.value.setCheckedKeys([]);
  } else {
    treeRef.value.setCheckedKeys(leafPermissions.value.map((n) => n.code));
  }
  updateCheckedCount();
}

async function load() {
  loading.value = true;
  try {
    [roles.value, tree.value] = await Promise.all([listRolesApi(), listPermissionsApi()]);
  } catch (error) {
    reportAccessError(error);
  } finally {
    loading.value = false;
  }
}

async function create() {
  if (!roleName.value.trim()) return;
  saving.value = true;
  try {
    await createRoleApi(roleName.value.trim());
    createOpen.value = false;
    ElMessage.success('角色已创建');
    await load();
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

async function openEdit(role: AdminRole) {
  selected.value = role;
  globalGrant.value = role.permissions.includes('*');
  wildcardGrants.value = role.permissions.filter((code) => code.endsWith(':*'));
  editOpen.value = true;
  await nextTick();
  treeRef.value?.setCheckedKeys(role.permissions.filter((code) => !code.includes('*')));
  updateCheckedCount();
}

function addWildcard() {
  const code = newWildcard.value.trim();
  if (!/^[^:*]+(?::[^:*]+)*:\*$/.test(code)) {
    ElMessage.warning('格式应为 paycardhub:* 或更深层的 前缀:*');
    return;
  }
  if (!wildcardGrants.value.includes(code)) wildcardGrants.value.push(code);
  newWildcard.value = '';
}

async function save() {
  if (!selected.value) return;
  saving.value = true;
  try {
    const exact = (treeRef.value?.getCheckedKeys() || []).map(String);
    const permissions = [...exact, ...wildcardGrants.value];
    if (globalGrant.value) permissions.push('*');
    await setRolePermissionsApi(selected.value.name, permissions);
    editOpen.value = false;
    ElMessage.success('授权已更新');
    await load();
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

async function remove(role: AdminRole) {
  try {
    await ElMessageBox.confirm(`确定删除角色「${role.name}」吗？`, '删除角色', { type: 'warning' });
  } catch { return; }
  try {
    await deleteRoleApi(role.name);
    ElMessage.success('角色已删除');
    await load();
  } catch (error) {
    reportAccessError(error);
  }
}

onMounted(load);
</script>

<template>
  <Page title="角色管理">
    <div class="mb-4 flex flex-wrap justify-end gap-2">
      <AccessTransfer kind="roles" @applied="load" />
      <ElButton v-if="canCreate" type="primary" @click="roleName = ''; createOpen = true">新建角色</ElButton>
    </div>
    <ElTable :data="roles" v-loading="loading" row-key="name" stripe class="w-full">
      <ElTableColumn prop="name" label="角色" min-width="180" />
      <ElTableColumn label="类型" width="110"><template #default="{ row }"><ElTag v-if="row.isSystem" size="small" type="info">内置</ElTag><span v-else>自定义</span></template></ElTableColumn>
      <ElTableColumn prop="userCount" label="用户数" width="100" />
      <ElTableColumn label="授权" min-width="300" show-overflow-tooltip>
        <template #default="{ row }">{{ row.permissions.join('、') || '未授权' }}</template>
      </ElTableColumn>
      <ElTableColumn label="操作" width="175" fixed="right">
        <template #default="{ row }">
          <ElButton v-if="canAssign" link type="primary" @click="openEdit(row as AdminRole)">配置权限</ElButton>
          <ElButton v-if="canDelete && !row.isSystem" link type="danger" @click="remove(row as AdminRole)">删除</ElButton>
        </template>
      </ElTableColumn>
    </ElTable>

    <ElDialog v-model="createOpen" title="新建角色" width="460px" destroy-on-close>
      <ElInput v-model="roleName" maxlength="100" placeholder="角色名称" @keyup.enter="create" />
      <template #footer><ElButton @click="createOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="create">创建</ElButton></template>
    </ElDialog>

    <ElDialog v-model="editOpen" :title="`配置权限 · ${selected?.name || ''}`" width="640px" destroy-on-close>
      <div class="mb-3"><ElCheckbox v-model="globalGrant">全部权限 (*)</ElCheckbox></div>
      <div class="mb-2 flex items-center justify-between">
        <span class="text-sm font-medium">具体权限</span>
        <span class="text-sm text-gray-500">
          已选择 {{ checkedCount }} / {{ leafPermissions.length }} 个
          <ElButton link type="primary" class="ml-2" @click="toggleAllPermissions">
            {{ isAllChecked ? '取消全选' : '全选' }}
          </ElButton>
        </span>
      </div>
      <div class="max-h-72 overflow-auto border border-solid border-gray-200 px-3 py-2 dark:border-gray-700">
        <ElTree
          ref="treeRef" :data="tree" node-key="code" show-checkbox check-strictly
          :props="{ label: 'name', children: 'children', disabled: (node: any) => node.isEnabled === null }"
          @check-change="updateCheckedCount"
        >
          <template #default="{ data }"><span>{{ data.name }} <span class="ml-2 text-xs text-gray-500">{{ data.code }}</span></span></template>
        </ElTree>
      </div>
      <div class="mb-2 mt-5 text-sm font-medium">层级通配授权</div>
      <div class="mb-3 flex gap-2">
        <ElSelect v-model="newWildcard" filterable allow-create default-first-option clearable placeholder="选择层级或输入 前缀:*" class="w-full">
          <ElOption v-for="node in flattened" :key="node.code" :label="`${node.code}:*`" :value="`${node.code}:*`" />
        </ElSelect>
        <ElButton @click="addWildcard">添加</ElButton>
      </div>
      <div class="flex flex-wrap gap-2">
        <ElTag v-for="grant in wildcardGrants" :key="grant" closable @close="wildcardGrants = wildcardGrants.filter((x) => x !== grant)">{{ grant }}</ElTag>
      </div>
      <template #footer><ElButton @click="editOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="save">保存授权</ElButton></template>
    </ElDialog>
  </Page>
</template>
