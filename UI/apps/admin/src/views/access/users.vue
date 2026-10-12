<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { useAccess } from '@vben/access';

import {
  ElButton, ElDialog, ElForm, ElFormItem, ElInput, ElMessage,
  ElMessageBox, ElOption, ElSelect, ElSwitch, ElTable, ElTableColumn, ElTag,
} from 'element-plus';

import {
  type AdminRole, type AdminUser, createUserApi, type CreateUserRequest,
  listRolesApi, listUsersApi, resetUserPasswordApi, setUserEnabledApi,
  setUserRolesApi, updateUserApi, type UpdateUserRequest,
} from '#/api/admin-access';
import UserSessionDetail from '#/views/sessions/user-detail.vue';

import { reportAccessError } from './error';

const { hasAccessByCodes } = useAccess();
const canCreate = hasAccessByCodes(['auth:admin:users:create']);
const canUpdate = hasAccessByCodes(['auth:admin:users:update']);
const canDelete = hasAccessByCodes(['auth:admin:users:delete']);
const canAssignRole = hasAccessByCodes(['auth:admin:roles:assign']);

const users = ref<AdminUser[]>([]);
const roles = ref<AdminRole[]>([]);
const loading = ref(false);
const saving = ref(false);
const search = ref('');
const createOpen = ref(false);
const rolesOpen = ref(false);
const passwordOpen = ref(false);
const editOpen = ref(false);
const selected = ref<AdminUser | null>(null);
const form = reactive<CreateUserRequest>({ userName: '', name: '', displayName: '', email: null, password: '', roles: [] });
const editForm = reactive<UpdateUserRequest>({ name: '', displayName: '', email: null });
const assignedRoles = ref<string[]>([]);
const newPassword = ref('');

const allRoleNames = computed(() => roles.value.map((r) => r.name));
const isAllRolesSelected = computed(
  () => assignedRoles.value.length > 0 && assignedRoles.value.length === roles.value.length,
);
const isAllFormRolesSelected = computed(
  () => form.roles.length > 0 && form.roles.length === roles.value.length,
);

function toggleAllRoles() {
  if (isAllRolesSelected.value) {
    assignedRoles.value = [];
  } else {
    assignedRoles.value = [...allRoleNames.value];
  }
}

function toggleAllFormRoles() {
  if (isAllFormRolesSelected.value) {
    form.roles = [];
  } else {
    form.roles = [...allRoleNames.value];
  }
}

const filteredUsers = computed(() => {
  const query = search.value.trim().toLocaleLowerCase();
  return query
    ? users.value.filter((user) => [user.userName, user.name, user.displayName, user.email || '']
        .some((value) => value.toLocaleLowerCase().includes(query)))
    : users.value;
});

async function load() {
  loading.value = true;
  try {
    [users.value, roles.value] = await Promise.all([listUsersApi(), listRolesApi()]);
  } catch (error) {
    reportAccessError(error);
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  Object.assign(form, { userName: '', name: '', displayName: '', email: null, password: '', roles: [] });
  createOpen.value = true;
}

async function create() {
  if (!form.userName.trim() || !form.name.trim() || !form.displayName.trim() || !form.password) {
    ElMessage.warning('请填写用户名、姓名、昵称和密码');
    return;
  }
  saving.value = true;
  try {
    await createUserApi({ ...form, email: form.email?.trim() || null });
    createOpen.value = false;
    ElMessage.success('用户已创建');
    await load();
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

function openRoles(user: AdminUser) {
  selected.value = user;
  assignedRoles.value = [...user.roles];
  rolesOpen.value = true;
}

async function saveRoles() {
  if (!selected.value) return;
  saving.value = true;
  try {
    await setUserRolesApi(selected.value.id, assignedRoles.value);
    rolesOpen.value = false;
    ElMessage.success('角色已更新');
    await load();
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

function openPassword(user: AdminUser) {
  selected.value = user;
  newPassword.value = '';
  passwordOpen.value = true;
}

async function savePassword() {
  if (!selected.value || !newPassword.value) return;
  saving.value = true;
  try {
    await resetUserPasswordApi(selected.value.id, newPassword.value);
    passwordOpen.value = false;
    newPassword.value = '';
    ElMessage.success('密码已重置');
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

function openEdit(user: AdminUser) {
  selected.value = user;
  Object.assign(editForm, { name: user.name, displayName: user.displayName, email: user.email });
  editOpen.value = true;
}

async function saveEdit() {
  if (!selected.value || !editForm.name.trim() || !editForm.displayName.trim()) {
    ElMessage.warning('请填写姓名和昵称');
    return;
  }
  saving.value = true;
  try {
    await updateUserApi(selected.value.id, { ...editForm, email: editForm.email?.trim() || null });
    editOpen.value = false;
    ElMessage.success('用户信息已更新');
    await load();
  } catch (error) {
    reportAccessError(error);
  } finally {
    saving.value = false;
  }
}

async function toggle(user: AdminUser) {
  try {
    await ElMessageBox.confirm(
      `确定要${user.isEnabled ? '停用' : '启用'}用户「${user.userName}」吗？`,
      '确认状态变更', { type: 'warning' },
    );
  } catch { return; }
  try {
    await setUserEnabledApi(user.id, !user.isEnabled);
    ElMessage.success('状态已更新');
    await load();
  } catch (error) {
    reportAccessError(error);
  }
}

function date(value: null | string) {
  return value ? new Date(value).toLocaleString() : '—';
}

const [SessionDrawer, sessionDrawerApi] = useVbenDrawer();
const sessionTarget = ref<AdminUser | null>(null);

function openSessions(user: AdminUser) {
  sessionTarget.value = user;
  sessionDrawerApi.open();
}

onMounted(load);
</script>

<template>
  <Page title="用户列表">
    <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
      <ElInput v-model="search" clearable placeholder="搜索用户名、姓名或邮箱" class="!w-72" />
      <ElButton v-if="canCreate" type="primary" @click="openCreate">新建用户</ElButton>
    </div>
    <ElTable :data="filteredUsers" v-loading="loading" row-key="id" stripe class="w-full">
      <ElTableColumn prop="userName" label="用户名" min-width="140" />
      <ElTableColumn prop="name" label="姓名" min-width="120" />
      <ElTableColumn prop="displayName" label="昵称" min-width="120" />
      <ElTableColumn prop="email" label="邮箱" min-width="180" show-overflow-tooltip />
      <ElTableColumn label="角色" min-width="210">
        <template #default="{ row }">
          <ElTag v-for="role in row.roles" :key="role" size="small" class="mr-1">{{ role }}</ElTag>
          <span v-if="!row.roles.length">—</span>
        </template>
      </ElTableColumn>
      <ElTableColumn label="启用" width="90">
        <template #default="{ row }"><ElSwitch v-if="canUpdate" :model-value="row.isEnabled" @click.prevent="toggle(row as AdminUser)" /><ElTag v-else :type="row.isEnabled ? 'success' : 'danger'" size="small">{{ row.isEnabled ? '启用' : '停用' }}</ElTag></template>
      </ElTableColumn>
      <ElTableColumn label="最近登录" width="190"><template #default="{ row }">{{ date(row.lastLoginAtUtc) }}</template></ElTableColumn>
      <ElTableColumn label="操作" width="330" fixed="right">
        <template #default="{ row }">
          <ElButton v-if="canUpdate" link type="primary" @click="openEdit(row as AdminUser)">编辑</ElButton>
          <ElButton v-if="canAssignRole" link type="primary" @click="openRoles(row as AdminUser)">分配角色</ElButton>
          <ElButton v-if="canUpdate" link @click="openPassword(row as AdminUser)">重置密码</ElButton>
          <ElButton link type="primary" @click="openSessions(row as AdminUser)">登录情况</ElButton>
        </template>
      </ElTableColumn>
    </ElTable>

    <ElDialog v-model="createOpen" title="新建用户" width="520px" destroy-on-close>
      <ElForm :model="form" label-width="90px">
        <ElFormItem label="用户名" required><ElInput v-model="form.userName" maxlength="256" /></ElFormItem>
        <ElFormItem label="姓名" required><ElInput v-model="form.name" maxlength="100" /></ElFormItem>
        <ElFormItem label="昵称" required><ElInput v-model="form.displayName" maxlength="100" /></ElFormItem>
        <ElFormItem label="邮箱"><ElInput v-model="form.email" type="email" /></ElFormItem>
        <ElFormItem label="初始密码" required><ElInput v-model="form.password" type="password" show-password autocomplete="new-password" /></ElFormItem>
        <ElFormItem label="角色">
          <div class="w-full">
            <div class="mb-2 flex items-center justify-between">
              <span class="text-sm text-gray-500">已选择 {{ form.roles.length }} / {{ roles.length }} 个角色</span>
              <ElButton link type="primary" @click="toggleAllFormRoles">
                {{ isAllFormRolesSelected ? '取消全选' : '全选' }}
              </ElButton>
            </div>
            <ElSelect v-model="form.roles" multiple class="w-full" placeholder="选择角色">
              <ElOption v-for="role in roles" :key="role.name" :label="role.name" :value="role.name" />
            </ElSelect>
          </div>
        </ElFormItem>
      </ElForm>
      <template #footer><ElButton @click="createOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="create">创建</ElButton></template>
    </ElDialog>

    <ElDialog v-model="rolesOpen" :title="`分配角色 · ${selected?.userName || ''}`" width="480px" destroy-on-close>
      <div class="mb-2 flex items-center justify-between">
        <span class="text-sm text-gray-500">已选择 {{ assignedRoles.length }} / {{ roles.length }} 个角色</span>
        <ElButton link type="primary" @click="toggleAllRoles">
          {{ isAllRolesSelected ? '取消全选' : '全选' }}
        </ElButton>
      </div>
      <ElSelect v-model="assignedRoles" multiple class="w-full" placeholder="选择角色">
        <ElOption v-for="role in roles" :key="role.name" :label="role.name" :value="role.name" />
      </ElSelect>
      <template #footer><ElButton @click="rolesOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="saveRoles">保存</ElButton></template>
    </ElDialog>

    <ElDialog v-model="passwordOpen" :title="`重置密码 · ${selected?.userName || ''}`" width="480px" destroy-on-close>
      <ElInput v-model="newPassword" type="password" show-password autocomplete="new-password" placeholder="输入新密码" />
      <template #footer><ElButton @click="passwordOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" :disabled="!newPassword" @click="savePassword">重置</ElButton></template>
    </ElDialog>

    <ElDialog v-model="editOpen" :title="`编辑用户 · ${selected?.userName || ''}`" width="520px" destroy-on-close>
      <ElForm :model="editForm" label-width="90px">
        <ElFormItem label="姓名" required><ElInput v-model="editForm.name" maxlength="100" /></ElFormItem>
        <ElFormItem label="昵称" required><ElInput v-model="editForm.displayName" maxlength="100" /></ElFormItem>
        <ElFormItem label="邮箱"><ElInput v-model="editForm.email" type="email" /></ElFormItem>
      </ElForm>
      <template #footer><ElButton @click="editOpen = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="saveEdit">保存</ElButton></template>
    </ElDialog>

    <SessionDrawer
      :footer="false"
      :title="`登录情况 · ${sessionTarget?.displayName || sessionTarget?.userName || ''}`"
      class="w-[80%]"
    >
      <UserSessionDetail
        v-if="sessionTarget"
        :display-name="sessionTarget.displayName"
        :user-id="sessionTarget.id"
      />
    </SessionDrawer>
  </Page>
</template>
