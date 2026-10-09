<script lang="ts" setup>
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElCheckbox,
  ElDialog,
  ElForm,
  ElFormItem,
  ElInput,
  ElInputNumber,
  ElMessage,
  ElOption,
  ElPagination,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import {
  type ProviderAccountListItem,
  type UpsertProviderAccountRequest,
  createProviderAccountApi,
  getProviderAccountDetailApi,
  getProviderAccountsApi,
  updateProviderAccountApi,
} from '#/api/provider-accounts';
import ProviderSelect from '#/components/provider-select.vue';

const router = useRouter();
const loading = ref(false);
const accounts = ref<ProviderAccountListItem[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const keyword = ref('');
const providerCode = ref('');

const dialogVisible = ref(false);
const dialogLoading = ref(false);
const editingId = ref<number | null>(null);
const suppressProviderWatch = ref(false);
const formProvider = ref('airwallex');
const formAccountName = ref('');
const formIsActive = ref(true);
const formAutoIssueEnabled = ref(false);
const formAutoIssuePriority = ref(0);
const formAutoIssueMaxDays = ref<number | undefined>(undefined);
const formAutoIssueCurrencies = ref('');
const formConfig = ref<Record<string, string>>({});

interface ConfigFieldDef {
  key: string;
  label: string;
  required?: boolean;
  password?: boolean;
  placeholder?: string;
  default?: string;
}

const PROVIDER_CONFIG_FIELDS: Record<string, ConfigFieldDef[]> = {
  airwallex: [
    { key: 'baseUrl', label: 'Base URL', required: true, placeholder: 'https://api.airwallex.com' },
    { key: 'clientId', label: 'Client ID', required: true },
    { key: 'apiKey', label: 'API Key', required: true, password: true },
    { key: 'cardholderId', label: 'Cardholder ID', required: true },
    { key: 'cardholderName', label: 'Cardholder Name' },
    { key: 'createdBy', label: 'Created By', default: 'PayCardHub' },
    { key: 'nickNamePrefix', label: 'NickName Prefix', default: 'pch' },
  ],
  tranhub: [
    { key: 'baseUrl', label: 'Base URL', required: true },
    { key: 'merchantNo', label: 'Merchant No', required: true },
    { key: 'aesKey', label: 'AES Key', required: true, password: true },
    { key: 'sha256Key', label: 'SHA256 Key', required: true, password: true },
    { key: 'productCode', label: 'Product Code', default: 'VCC20CARD' },
    { key: 'vccCardType', label: 'VCC Card Type' },
    { key: 'vccCardSubType', label: 'VCC Card Sub Type' },
    { key: 'vccCardEmail', label: 'VCC Card Email' },
    { key: 'allowedCallbackIps', label: 'Allowed Callback IPs' },
  ],
};

const currentConfigFields = computed(() => PROVIDER_CONFIG_FIELDS[formProvider.value] ?? []);

async function loadData() {
  loading.value = true;
  try {
    const res = await getProviderAccountsApi({
      keyword: keyword.value || undefined,
      providerCode: providerCode.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    });
    accounts.value = res.items;
    total.value = res.totalCount;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  loadData();
}

function handleReset() {
  keyword.value = '';
  providerCode.value = '';
  handleSearch();
}

function handlePageChange(val: number) {
  page.value = val;
  loadData();
}

function handleSizeChange(val: number) {
  pageSize.value = val;
  page.value = 1;
  loadData();
}

function viewDetail(item: ProviderAccountListItem) {
  router.push({ name: 'ProviderAccountDetail', params: { id: item.id } });
}

function openCreateDialog() {
  editingId.value = null;
  formProvider.value = 'airwallex';
  formAccountName.value = '';
  formIsActive.value = true;
  formAutoIssueEnabled.value = false;
  formAutoIssuePriority.value = 0;
  formAutoIssueMaxDays.value = undefined;
  formAutoIssueCurrencies.value = '';
  resetFormConfig();
  dialogVisible.value = true;
}

async function openEditDialog(item: ProviderAccountListItem) {
  suppressProviderWatch.value = true;
  editingId.value = item.id;
  formProvider.value = item.providerCode.toLowerCase();
  formAccountName.value = item.accountName;
  formIsActive.value = item.isActive;
  formAutoIssueEnabled.value = item.autoIssueEnabled;
  formAutoIssuePriority.value = item.autoIssuePriority;
  formAutoIssueMaxDays.value = item.autoIssueMaxDays ?? undefined;
  formAutoIssueCurrencies.value = item.autoIssueCurrenciesJson ?? '';

  const defaults: Record<string, string> = {};
  for (const field of currentConfigFields.value) {
    if (field.default !== undefined) {
      defaults[field.key] = field.default;
    }
  }

  try {
    const detail = await getProviderAccountDetailApi(item.id);
    const rawConfig = detail.rawConfiguration ?? {};
    const merged: Record<string, string> = { ...defaults };
    for (const [key, value] of Object.entries(rawConfig)) {
      if (value !== null && value !== undefined) {
        merged[key] = value;
      }
    }
    formConfig.value = merged;
  } catch {
    formConfig.value = defaults;
  }

  suppressProviderWatch.value = false;
  dialogVisible.value = true;
}

function resetFormConfig() {
  formConfig.value = {};
  for (const field of currentConfigFields.value) {
    if (field.default !== undefined) {
      formConfig.value[field.key] = field.default;
    }
  }
}

watch(formProvider, () => {
  if (suppressProviderWatch.value) return;
  const preserved: Record<string, string> = {};
  for (const field of currentConfigFields.value) {
    if (formConfig.value[field.key]) {
      preserved[field.key] = formConfig.value[field.key];
    }
  }
  resetFormConfig();
  Object.assign(formConfig.value, preserved);
});

async function handleSubmit() {
  if (!formAccountName.value.trim()) {
    ElMessage.warning('请输入账户名称');
    return;
  }

  const missing = currentConfigFields.value
    .filter((f) => f.required && !formConfig.value[f.key]?.trim());
  if (missing.length > 0) {
    if (!editingId.value) {
      ElMessage.warning(`请填写必填项：${missing.map((f) => f.label).join('、')}`);
      return;
    }
    ElMessage.warning(`以下字段未填写（可稍后补充）：${missing.map((f) => f.label).join('、')}`);
  }

  dialogLoading.value = true;
  try {
    const data: UpsertProviderAccountRequest = {
      providerCode: formProvider.value,
      accountName: formAccountName.value.trim(),
      isActive: formIsActive.value,
      configuration: { ...formConfig.value },
      autoIssueEnabled: formAutoIssueEnabled.value,
      autoIssuePriority: formAutoIssuePriority.value,
      autoIssueMaxDays: formAutoIssueMaxDays.value,
      autoIssueCurrenciesJson: formAutoIssueCurrencies.value.trim() || undefined,
    };

    if (editingId.value) {
      await updateProviderAccountApi(editingId.value, data);
      ElMessage.success('更新成功');
    } else {
      await createProviderAccountApi(data);
      ElMessage.success('创建成功');
    }

    dialogVisible.value = false;
    loadData();
  } finally {
    dialogLoading.value = false;
  }
}

function formatDateTime(val: string) {
  return new Date(val).toLocaleString('zh-CN');
}

onMounted(loadData);
</script>

<template>
  <Page title="供应商账户">
    <ElCard class="mb-4">
      <div class="flex flex-wrap items-end gap-3">
        <div class="w-56">
          <label class="mb-1 block text-xs text-gray-500">关键词</label>
          <ElInput v-model="keyword" placeholder="账户名称" clearable @keyup.enter="handleSearch" />
        </div>
        <div class="w-44">
          <label class="mb-1 block text-xs text-gray-500">供应商</label>
          <ProviderSelect v-model="providerCode" />
        </div>
        <ElButton type="primary" @click="handleSearch">查询</ElButton>
        <ElButton @click="handleReset">重置</ElButton>
        <ElButton type="success" @click="openCreateDialog">新建</ElButton>
      </div>
    </ElCard>

    <ElCard>
      <ElTable
        :data="accounts"
        v-loading="loading"
        stripe
        row-class-name="cursor-pointer"
        @row-click="viewDetail"
      >
        <ElTableColumn prop="id" label="ID" width="70" />
        <ElTableColumn prop="providerCode" label="供应商" width="110" />
        <ElTableColumn prop="accountName" label="账户名称" min-width="180" show-overflow-tooltip />
        <ElTableColumn label="启用" width="80" align="center">
          <template #default="{ row }">
            <ElTag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '是' : '否' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn label="自动发卡" width="90" align="center">
          <template #default="{ row }">
            <ElTag :type="row.autoIssueEnabled ? 'success' : 'info'" size="small">
              {{ row.autoIssueEnabled ? '是' : '否' }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="autoIssuePriority" label="优先级" width="80" align="center" />
        <ElTableColumn label="持卡人数" width="90" align="center">
          <template #default="{ row }">{{ row.cardholderMappingCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="卡片数" width="90" align="center">
          <template #default="{ row }">{{ row.cardCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="交易数" width="90" align="center">
          <template #default="{ row }">{{ row.transactionCount }}</template>
        </ElTableColumn>
        <ElTableColumn label="最后同步" width="170">
          <template #default="{ row }">{{ formatDateTime(row.lastSuccessfulSyncedAt) }}</template>
        </ElTableColumn>
        <ElTableColumn label="创建时间" width="170">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </ElTableColumn>
        <ElTableColumn label="操作" width="100" fixed="right">
          <template #default="{ row }">
            <ElButton link type="primary" size="small" @click.stop="openEditDialog(row)">编辑</ElButton>
          </template>
        </ElTableColumn>
      </ElTable>

      <div class="mt-4 flex justify-end">
        <ElPagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          @current-change="handlePageChange"
          @size-change="handleSizeChange"
        />
      </div>
    </ElCard>

    <ElDialog
      v-model="dialogVisible"
      :title="editingId ? '编辑供应商账户' : '新建供应商账户'"
      width="640px"
      :close-on-click-modal="false"
    >
      <ElForm label-width="140px" v-loading="dialogLoading">
        <ElFormItem label="供应商" required>
          <ElSelect v-model="formProvider" :disabled="!!editingId" style="width: 100%">
            <ElOption label="Airwallex" value="airwallex" />
            <ElOption label="TranHub" value="tranhub" />
          </ElSelect>
        </ElFormItem>
        <ElFormItem label="账户名称" required>
          <ElInput v-model="formAccountName" placeholder="请输入账户名称" />
        </ElFormItem>

        <ElFormItem v-for="field in currentConfigFields" :key="field.key" :label="field.label" :required="field.required">
          <ElInput
            v-model="formConfig[field.key]"
            :type="field.password ? 'password' : 'text'"
            :show-password="field.password"
            :placeholder="field.placeholder || `请输入${field.label}`"
          />
        </ElFormItem>

        <ElFormItem label="启用">
          <ElCheckbox v-model="formIsActive" />
        </ElFormItem>

        <ElFormItem label="自动发卡">
          <ElCheckbox v-model="formAutoIssueEnabled" />
        </ElFormItem>

        <ElFormItem v-if="formAutoIssueEnabled" label="优先级">
          <ElInputNumber v-model="formAutoIssuePriority" :min="0" />
        </ElFormItem>

        <ElFormItem v-if="formAutoIssueEnabled" label="最大天数">
          <ElInputNumber v-model="formAutoIssueMaxDays" :min="1" placeholder="可选" />
        </ElFormItem>

        <ElFormItem v-if="formAutoIssueEnabled" label="币种(JSON)">
          <ElInput v-model="formAutoIssueCurrencies" placeholder='如 ["USD","HKD"]' />
        </ElFormItem>
      </ElForm>

      <template #footer>
        <ElButton @click="dialogVisible = false">取消</ElButton>
        <ElButton type="primary" :loading="dialogLoading" @click="handleSubmit">
          {{ editingId ? '保存' : '创建' }}
        </ElButton>
      </template>
    </ElDialog>
  </Page>
</template>
