<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  ElButton,
  ElCard,
  ElDescriptions,
  ElDescriptionsItem,
  ElInput,
  ElMessage,
  ElRadioButton,
  ElRadioGroup,
  ElTable,
  ElTableColumn,
  ElTag,
} from 'element-plus';

import { baseRequestClient } from '#/api/request';

interface HotelMatchingRequest {
  hotelCode: string;
  sourceType: 'ota' | 'supplier';
  supId: number | null;
}

interface HotelMatchingSource {
  hotelCode: string;
  hotelName: string;
  isQualityValid: boolean;
  latitude?: number;
  longitude?: number;
  phone?: string;
}

interface HotelMatchingCandidate {
  hotelCode: string;
  hotelName: string;
  regionLevel1Name?: string;
  regionLevel2Name?: string;
  regionLevel3Name?: string;
  latitude?: number;
  longitude?: number;
}

interface HotelMatchingEvidence {
  distanceMeters?: number;
  mtHotelCode: string;
  nameResult?: { kind: string };
  phoneConflict?: boolean;
  phoneMatches?: boolean;
  rejectionReasons?: string[];
}

interface HotelMatchingDecision {
  candidates: HotelMatchingEvidence[];
  kind?: string;
  mtHotelCode?: string;
}

interface HotelMatchingResult {
  decision: HotelMatchingDecision;
  errors: string[];
  recalledCandidates: HotelMatchingCandidate[];
  request: HotelMatchingRequest;
  source: HotelMatchingSource;
}

const loading = ref(false);
const sourceType = ref<'ota' | 'supplier'>('supplier');
const hotelCode = ref('');
const supId = ref<number | null>(null);
const result = ref<HotelMatchingResult | null>(null);

async function handleAnalyze() {
  if (!hotelCode.value.trim()) {
    ElMessage.warning('请输入酒店代码');
    return;
  }
  if (sourceType.value === 'supplier' && !supId.value) {
    ElMessage.warning('请输入 SupId');
    return;
  }

  loading.value = true;
  result.value = null;
  try {
    const response = await baseRequestClient.post<HotelMatchingResult>(
      '/admin/hotel-matching/analyze',
      {
        hotelCode: hotelCode.value.trim(),
        sourceType: sourceType.value,
        supId: sourceType.value === 'supplier' ? supId.value : null,
      },
    );
    result.value = response;
    if (response.errors?.length) {
      ElMessage.warning(response.errors.join(', '));
    } else {
      ElMessage.success('分析完成');
    }
  } catch (error: any) {
    ElMessage.error(error.message || '分析失败');
  } finally {
    loading.value = false;
  }
}

function calculateDistance(
  lat1?: number,
  lon1?: number,
  lat2?: number,
  lon2?: number,
): number | null {
  if (!lat1 || !lon1 || !lat2 || !lon2) return null;
  const rad = (v: number) => (v * Math.PI) / 180;
  const dLat = rad(lat2 - lat1);
  const dLon = rad(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(dLon / 2) ** 2;
  return 6_371_008.8 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function getDistance(candidate: HotelMatchingCandidate): number | null {
  if (!result.value?.source) return null;
  const evidence = result.value.decision.candidates.find(
    (c) => c.mtHotelCode === candidate.hotelCode,
  );
  if (evidence?.distanceMeters !== undefined) {
    return evidence.distanceMeters;
  }
  return calculateDistance(
    result.value.source.latitude,
    result.value.source.longitude,
    candidate.latitude,
    candidate.longitude,
  );
}

function getResult(candidate: HotelMatchingCandidate): {
  class: string;
  text: string;
} {
  if (!result.value) return { class: '', text: '-' };
  const evidence = result.value.decision.candidates.find(
    (c) => c.mtHotelCode === candidate.hotelCode,
  );
  const reasons = evidence?.rejectionReasons || [];
  if (reasons.length > 0) {
    return { class: 'text-red-500', text: '剔除' };
  }
  if (result.value.decision.mtHotelCode === candidate.hotelCode) {
    return { class: 'text-green-600 font-semibold', text: '匹配' };
  }
  return { class: 'text-yellow-600', text: '候选' };
}

function formatRegion(candidate: HotelMatchingCandidate): string {
  return [
    candidate.regionLevel1Name,
    candidate.regionLevel2Name,
    candidate.regionLevel3Name,
  ]
    .filter(Boolean)
    .join(' / ') || '-';
}
</script>

<template>
  <Page title="酒店匹配" description="分析酒店匹配结果，查看召回候选和决策依据">
    <ElCard class="mb-4">
      <ElForm :inline="true" @submit.prevent="handleAnalyze">
        <ElFormItem label="酒店来源">
          <ElRadioGroup v-model="sourceType">
            <ElRadioButton value="supplier">SUP</ElRadioButton>
            <ElRadioButton value="ota">OTA</ElRadioButton>
          </ElRadioGroup>
        </ElFormItem>
        <ElFormItem :label="sourceType === 'supplier' ? 'SHotelCode' : 'MTHotelCode'">
          <ElInput
            v-model="hotelCode"
            placeholder="请输入酒店代码"
            class="w-48"
          />
        </ElFormItem>
        <ElFormItem v-if="sourceType === 'supplier'" label="SupId">
          <ElInput
            v-model.number="supId"
            type="number"
            placeholder="供应商 ID"
            class="w-32"
          />
        </ElFormItem>
        <ElFormItem>
          <ElButton type="primary" :loading="loading" @click="handleAnalyze">
            分析
          </ElButton>
        </ElFormItem>
      </ElForm>
    </ElCard>

    <template v-if="result">
      <ElCard class="mb-4">
        <ElDescriptions :column="5" border>
          <ElDescriptionsItem label="来源">
            {{ result.request.sourceType }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="酒店">
            {{ result.source.hotelCode || '-' }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="数据质量">
            <ElTag :type="result.source.isQualityValid ? 'success' : 'warning'" size="small">
              {{ result.source.isQualityValid ? '有效' : '问题' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="候选数">
            {{ result.recalledCandidates.length }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="结论">
            {{ result.decision.kind || (result.request.sourceType === 'ota' ? '反向召回' : '-') }}
          </ElDescriptionsItem>
        </ElDescriptions>
      </ElCard>

      <ElCard title="三公里召回候选" class="mb-4">
        <ElTable :data="result.recalledCandidates" stripe>
          <ElTableColumn label="酒店" min-width="200">
            <template #default="{ row }">
              <div>{{ row.hotelCode || '-' }}</div>
              <div class="text-gray-500 text-sm">{{ row.hotelName || '-' }}</div>
            </template>
          </ElTableColumn>
          <ElTableColumn label="距离" width="100">
            <template #default="{ row }">
              <template v-if="getDistance(row as HotelMatchingCandidate) !== null">
                {{ Math.round(getDistance(row as HotelMatchingCandidate)!) }} m
              </template>
              <span v-else>-</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="区域" min-width="150">
            <template #default="{ row }">
              {{ formatRegion(row as HotelMatchingCandidate) }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="名称证据" width="100">
            <template #default="{ row }">
              {{ result.decision.candidates.find(c => c.mtHotelCode === (row as HotelMatchingCandidate).hotelCode)?.nameResult?.kind || '-' }}
            </template>
          </ElTableColumn>
          <ElTableColumn label="电话证据" width="100">
            <template #default="{ row }">
              <template v-if="result.decision.candidates.find(c => c.mtHotelCode === (row as HotelMatchingCandidate).hotelCode)?.phoneConflict">
                <span class="text-red-500">冲突</span>
              </template>
              <template v-else-if="result.decision.candidates.find(c => c.mtHotelCode === (row as HotelMatchingCandidate).hotelCode)?.phoneMatches">
                <span class="text-green-600">一致</span>
              </template>
              <span v-else>-</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="结果" width="80">
            <template #default="{ row }">
              <span :class="getResult(row as HotelMatchingCandidate).class">{{ getResult(row as HotelMatchingCandidate).text }}</span>
            </template>
          </ElTableColumn>
          <ElTableColumn label="规则原因" min-width="150">
            <template #default="{ row }">
              {{ result.decision.candidates.find(c => c.mtHotelCode === (row as HotelMatchingCandidate).hotelCode)?.rejectionReasons?.join(', ') || '-' }}
            </template>
          </ElTableColumn>
        </ElTable>
      </ElCard>

      <ElCard title="当前清洗结果">
        <pre class="bg-gray-50 p-4 rounded overflow-auto max-h-96 text-sm">{{ JSON.stringify(result.source, null, 2) }}</pre>
      </ElCard>
    </template>
  </Page>
</template>
