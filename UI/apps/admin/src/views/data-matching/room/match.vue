<template>
  <div class="page-container layout-pd" style="height: 100%">
    <el-card shadow="hover" class="main-card fill-card" style="height: 100%; overflow: hidden">
      <div class="main-layout" style="height: 100%">
        <div class="main-left" style="width: 100%; display: flex; flex-direction: column">
          <el-form :inline="true" @submit.prevent>
            <el-form-item>
              <el-autocomplete
                v-model="code"
                :fetch-suggestions="query => history.filter(item => item.includes(query))"
                placeholder="请输入 MTHotelCode"
                size="default"
                class="ml10"
                @select="onInputSelect"
                popper-class="history-autocomplete"
              >
                <template #default="{ item }">
                  <div style="display: flex; justify-content: space-between; align-items: center">
                    <span>{{ item }}</span>
                    <el-icon @click.stop="removeHistory(item)" style="cursor: pointer">
                      <CircleX />
                    </el-icon>
                  </div>
                </template>
              </el-autocomplete>
            </el-form-item>
            <el-form-item>
              <el-button class="ml10" type="primary" @click="fetchData" size="default">查询</el-button>
            </el-form-item>
          </el-form>

          <div style="text-align: right; margin-bottom: 10px">
            <el-radio-group v-model="isMixedColumn" size="small">
              <el-radio-button :value="false">分开列</el-radio-button>
              <el-radio-button :value="true">混合列</el-radio-button>
            </el-radio-group>
          </div>

          <div v-if="supHotelInfos.length > 0 && !loading" style="min-height: 0; flex: 1; overflow: hidden">
            <el-table :data="transformedRows" style="height: 100%; width: 100%; overflow: auto">
              <el-table-column fixed="left" label="母房型/代理" width="300">
                <template #default="{ row }">
                  <el-card class="room-card" shadow="hover" body-style="padding: 12px">
                    <div class="room-code">{{ row.otaRoomInfo.id }}</div>
                    <div v-if="row.otaRoomInfo.roomNameCN" class="room-title">
                      {{ row.otaRoomInfo.roomNameCN }}
                    </div>
                    <div
                      v-if="row.otaRoomInfo.roomNameEN && row.otaRoomInfo.roomNameEN !== row.otaRoomInfo.roomNameCN"
                      class="room-subtitle"
                    >
                      {{ row.otaRoomInfo.roomNameEN }}
                    </div>

                    <div class="badge-list" style="margin-top: 8px; display: flex; flex-wrap: wrap; gap: 6px">
                      <template v-for="hotel in supHotelInfos" :key="hotel.supId + '---' + hotel.sHotelCode">
                        <span
                          style="font-size: 12px; background-color: blueviolet; color: white; padding: 2px 6px; border-radius: 4px"
                          v-if="row[hotel.supId + '---' + hotel.sHotelCode] && row[hotel.supId + '---' + hotel.sHotelCode].length > 0"
                          size="mini"
                          type="primary"
                          @click="scrollToColumn(hotel.supId + '---' + hotel.sHotelCode)"
                          round
                        >
                          {{ hotel.supFlag }}
                        </span>
                      </template>
                    </div>
                    <div class="room-code">{{ row.otaRoomInfo.createTime }}</div>
                  </el-card>
                </template>
              </el-table-column>
              <template v-if="!isMixedColumn">
                <el-table-column
                  v-for="hotel in supHotelInfos"
                  :key="hotel.id"
                  :label="hotel.supFlag"
                  min-width="300"
                  :column-key="hotel.supId + '---' + hotel.sHotelCode"
                >
                  <template #default="{ row }">
                    <div class="room-card-wrapper">
                      <template v-for="room in row[hotel.supId + '---' + hotel.sHotelCode]" :key="room.id">
                        <div
                          class="room-card"
                          :class="{ 'expired-card': !room.mapping && isExpired(room.lastUpdateDate) }"
                        >
                          <span class="sup-flag-badge">{{ hotel.supFlag }}</span>
                          <div class="room-code">{{ room.id }}</div>
                          <div class="room-names">
                            <span class="room-name-cn" v-if="room.sRoomNameCN">{{ room.sRoomNameCN }}</span>
                            <span class="room-name-en" v-if="room.sRoomNameEN && room.sRoomNameEN !== room.sRoomNameCN">
                              {{ room.sRoomNameEN }}
                            </span>
                          </div>

                          <div class="bed-type" v-if="room.smokeType != 0">{{ room.smokeType }}</div>
                          <div class="bed-type" v-if="room.bedInfo && room.bedInfo !== '[]'">
                            <span v-for="(option, index) in parseBedInfo(room.bedInfo)" :key="index" class="bed-option">
                              <template v-for="(bed, bIndex) in option.Beds" :key="bIndex">
                                <span class="bed-desc">{{ bed.Num }}×{{ bed.Description }}</span>
                                <span v-if="bIndex < option.Beds.length - 1"> + </span>
                              </template>
                              <span v-if="index < parseBedInfo(room.bedInfo).length - 1" class="or-sep"> 或 </span>
                            </span>
                          </div>

                          <div class="time-info">
                            <div v-if="room.createTime">创建: {{ formatDate(room.createTime) }}</div>
                            <div v-if="room.lastUpdateDate">更新: {{ formatDate(room.lastUpdateDate) }}</div>
                            <div v-if="room.mapping && room.mapping.createTime">
                              匹配: {{ formatDate(room.mapping.createTime) }}
                            </div>
                          </div>
                          <div v-if="!room.mapping && isExpired(room.createTime)" class="expired-tag">失效</div>
                        </div>
                      </template>
                    </div>
                  </template>
                </el-table-column>
              </template>

              <template v-else>
                <el-table-column label="子房型">
                  <template #default="{ row }">
                    <div class="room-card-wrapper">
                      <template v-for="hotel in supHotelInfos" :key="hotel.supId + '---' + hotel.sHotelCode">
                        <template v-for="room in row[hotel.supId + '---' + hotel.sHotelCode]" :key="room.id">
                          <div
                            class="room-card"
                            style="width: 280px; margin: 10px"
                            :class="{ 'expired-card': !room.mapping && isExpired(room.lastUpdateDate) }"
                          >
                            <div class="badge-layer">
                              <div
                                class="createBy-flag-badge"
                                v-if="room.mapping && room.mapping.createBy != '' && room.mapping.createBy != null"
                              >
                                {{ room.mapping.createBy }}
                              </div>
                              <el-tooltip :content="hotel.supId + ''" placement="top">
                                <span class="sup-flag-badge"> {{ hotel.supFlag }}</span>
                              </el-tooltip>
                            </div>
                            <div class="room-code">{{ room.id }}</div>
                            <div class="room-names">
                              <span class="room-name-cn" v-if="room.sRoomNameCN">{{ room.sRoomNameCN }}</span>
                              <span class="room-name-en" v-if="room.sRoomNameEN && room.sRoomNameEN !== room.sRoomNameCN">
                                {{ room.sRoomNameEN }}
                              </span>
                            </div>
                            <div class="bed-type" v-if="room.smokeType != 0">{{ room.smokeType }}</div>
                            <div class="bed-type" v-if="room.bedInfo && room.bedInfo !== '[]'">
                              <span v-for="(option, index) in parseBedInfo(room.bedInfo)" :key="index" class="bed-option">
                                <template v-for="(bed, bIndex) in option.Beds" :key="bIndex">
                                  <span class="bed-desc">{{ bed.Num }}×{{ bed.Description }}</span>
                                  <span v-if="bIndex < option.Beds.length - 1"> + </span>
                                </template>
                                <span v-if="index < parseBedInfo(room.bedInfo).length - 1" class="or-sep"> 或 </span>
                              </span>
                            </div>
                            <div class="time-info">
                              <div v-if="room.createTime">创建: {{ formatDate(room.createTime) }}</div>
                              <div v-if="room.lastUpdateDate">更新: {{ formatDate(room.lastUpdateDate) }}</div>
                              <div v-if="room.mapping && room.mapping.createTime">
                                匹配: {{ formatDate(room.mapping.createTime) }}
                              </div>
                            </div>
                            <div v-if="!room.mapping && isExpired(room.createTime)" class="expired-tag">失效</div>
                          </div>
                        </template>
                      </template>
                    </div>
                  </template>
                </el-table-column>
              </template>
            </el-table>
          </div>

          <el-empty
            v-else-if="!loading"
            description="请先输入 MTHotelCode 并点击查询"
            style="height: calc(100vh - 250px)"
            :image-size="180"
          />

          <template v-else>
            <div class="loading-container">
              <el-icon class="is-loading" style="font-size: 32px">
                <LoaderCircle />
              </el-icon>
              <div style="margin-top: 10px">正在加载数据...</div>
            </div>
          </template>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed } from 'vue';
import { useRoomMatchApi } from '#/api';
import { ElMessage } from 'element-plus';
import { CircleX, LoaderCircle } from '@vben/icons';
import dayjs from 'dayjs';
import { useHistory } from '#/components/useHistory';

const { history, saveHistory, removeHistory } = useHistory('mthotelcode_history');

const onInputSelect = (val: string) => {
  code.value = val;
};

const code = ref('');
const supHotelInfos = ref<any[]>([]);
const otaRoomInfos = ref<any[]>([]);
const supRoomInfos = ref<any[]>([]);
const roomMappingCtrips = ref<any[]>([]);

const isMixedColumn = ref(true);

const scrollToColumn = (sHotelCode: any) => {
  const tableWrapper = document.querySelector('.el-table__body-wrapper');
  if (!tableWrapper) return;

  const targetCell = tableWrapper.querySelector(`td[column-key="${sHotelCode}"]`);
  if (!targetCell) return;

  const wrapperLeft = tableWrapper.getBoundingClientRect().left;
  const cellLeft = targetCell.getBoundingClientRect().left;

  tableWrapper.scrollLeft += cellLeft - wrapperLeft;
};

const parseBedInfo = (info: any) => {
  try {
    return JSON.parse(info);
  } catch {
    return [];
  }
};

const transformedRows = computed(() => {
  const result = [];
  const otaRoomMap = new Map();

  const supRoomInfosMap: { [key: string]: any } = {};
  for (const room of supRoomInfos.value) {
    supRoomInfosMap[room.id] = room;
  }
  const otaRoomInfosMap: { [key: string]: any } = {};
  for (const room of otaRoomInfos.value) {
    otaRoomInfosMap[room.id] = room;
  }

  const supRoomMappingMap: { [key: string]: any } = {};
  for (const room of roomMappingCtrips.value) {
    supRoomMappingMap[room.supRoomId] = room;
  }

  const roomMappingCtripsMap: { [key: string]: any } = {};
  for (const map of roomMappingCtrips.value) {
    map.otaRoomInfo = otaRoomInfosMap[map.otaRoomId];
    map.supRoomInfo = supRoomInfosMap[map.supRoomId];
    if (map.otaRoomInfo == null || map.supRoomInfo == null) {
      console.log('映射关系错误，找不到对应房型', map);
      continue;
    }
    map.supRoomInfo.mapping = map;
    if (!roomMappingCtripsMap[map.otaRoomInfo.id]) {
      roomMappingCtripsMap[map.otaRoomInfo.id] = { supRoomInfoDic: {} };
    }

    const hotelKey = map.supRoomInfo.supId + '---' + map.supRoomInfo.sHotelCode;
    if (!roomMappingCtripsMap[map.otaRoomInfo.id].supRoomInfoDic[hotelKey]) {
      roomMappingCtripsMap[map.otaRoomInfo.id].supRoomInfoDic[hotelKey] = [];
    }
    roomMappingCtripsMap[map.otaRoomInfo.id].supRoomInfoDic[hotelKey].push(map);
  }

  for (const ota of otaRoomInfos.value) {
    const row = {
      otaRoomInfo: ota,
      matchedSupRoomIds: new Set(),
    };

    for (const hotel of supHotelInfos.value) {
      const hotelKey = hotel.supId + '---' + hotel.sHotelCode;
      const matchedRooms = [];

      if (roomMappingCtripsMap[ota.id] && roomMappingCtripsMap[ota.id].supRoomInfoDic[hotelKey]) {
        const supRoomInfoTemp = roomMappingCtripsMap[ota.id].supRoomInfoDic[hotelKey];
        for (let i = 0; i < supRoomInfoTemp.length; i++) {
          matchedRooms.push(supRoomInfoTemp[i].supRoomInfo);
          row.matchedSupRoomIds.add(supRoomInfoTemp[i].id);
        }
      }
      row[hotelKey] = matchedRooms;
    }

    result.push(row);
    otaRoomMap.set(ota.id, row);
  }

  const unmatchedRow = {
    otaRoomInfo: { roomNameCN: '未匹配房型' },
  };
  let isUnmatched = false;

  const unmatchedRoomsMap: { [key: string]: any } = {};
  for (const hotel of supHotelInfos.value) {
    const hotelKey = hotel.supId + '---' + hotel.sHotelCode;
    const rooms = supRoomInfos.value.filter(room => {
      if (!(room.sHotelCode === hotel.sHotelCode && room.supId === hotel.supId && room.mapping == undefined)) {
        return false;
      }
      return !roomMappingCtripsMap[room.id];
    });

    if (rooms.length > 0) {
      isUnmatched = true;
      unmatchedRoomsMap[hotelKey] = rooms;
    }
  }

  for (const hotel of supHotelInfos.value) {
    const hotelKey = hotel.supId + '---' + hotel.sHotelCode;
    unmatchedRow[hotelKey] = unmatchedRoomsMap[hotelKey] || [];
  }

  if (isUnmatched) {
    result.push(unmatchedRow);
  }

  return result;
});

const loading = ref(false);

const fetchData = async () => {
  const inputCode = code.value.trim();
  if (!inputCode) {
    ElMessage.warning('请输入 MTHotelCode');
    return;
  }

  try {
    loading.value = true;
    const { GetHotelRoomMapping } = useRoomMatchApi();

    const res = await GetHotelRoomMapping({ MTHotelCode: inputCode, isShowWord: false });
    loading.value = false;
    saveHistory(inputCode);

    otaRoomInfos.value = res.otaRoomInfos;
    roomMappingCtrips.value = res.roomMaps || [];
    supRoomInfos.value = res.supRoomInfos || [];

    const hotelMap = new Map<string, { supId: number; sHotelCode: string; supFlag: string }>();
    for (const room of supRoomInfos.value) {
      if (!room.sHotelCode) continue;
      const key = `${room.supId}---${room.sHotelCode}`;
      if (!hotelMap.has(key)) {
        hotelMap.set(key, { supId: room.supId, sHotelCode: room.sHotelCode, supFlag: String(room.supId) });
      }
    }
    supHotelInfos.value = [...hotelMap.values()];

    roomMappingCtrips.value = roomMappingCtrips.value.map(map => ({
      ...map,
      supRoomId: map.supRoomId,
      otaRoomId: map.otaRoomId,
    }));

    if (!supRoomInfos.value || !supRoomInfos.value.length) {
      ElMessage.warning('未找到供应商房型信息');
      return;
    }

    if (!otaRoomInfos.value || !otaRoomInfos.value.length) {
      ElMessage.warning('未找到 OTA 房型信息');
      return;
    }
  } catch {
    loading.value = false;
    ElMessage.error('查询失败');
  }
};

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  return dayjs(dateStr).format('YYYY-MM-DD HH:mm');
};

const isExpired = (dateStr: string) => {
  if (!dateStr) return false;
  const createDate = dayjs(dateStr);
  const twoMonthsAgo = dayjs().subtract(2, 'month');
  return createDate.isBefore(twoMonthsAgo);
};
</script>

<style scoped>
.fill-card {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.fill-card ::v-deep(.el-card__body) {
  flex: 1 1 auto;
  min-height: 0;
}

.loading-container {
  height: calc(100vh - 250px);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #999;
}

.bed-type {
  font-size: 12px;
  color: #555;
  line-height: 1.4;
  margin-top: 4px;
}

.bed-option {
  display: inline-block;
  margin-right: 4px;
}

.bed-desc {
  white-space: nowrap;
}

.or-sep {
  margin: 0 4px;
  color: #999;
}

.room-card {
  margin-bottom: 12px;
}

.room-title {
  font-weight: bold;
  font-size: 16px;
}

.room-subtitle {
  font-size: 13px;
  color: #666;
}

.room-card-wrapper {
  display: flex;
  flex-wrap: wrap;
  overflow-y: auto;
}

.room-card {
  width: 100%;
  box-sizing: border-box;
  margin-bottom: 8px;
}

.page-container {
  padding: 20px;
}

.main-layout {
  display: flex;
}

.main-left {
  flex: 1;
}

.match-cell {
  color: green;
  font-weight: bold;
}
</style>

<style scoped lang="scss">
.room-cards-wrapper {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.room-card {
  position: relative;
  min-width: 120px;
  background: #f9fafb;
  border-radius: 10px;
  box-shadow: 0 1px 5px #0001;
  padding: 8px 12px 12px 12px;
  margin-bottom: 4px;
  transition: box-shadow 0.2s;
  cursor: pointer;
  border: 1px solid #eee;
}

.room-card:hover {
  box-shadow: 0 2px 12px #0002;
  border-color: #409eff;
}

.room-card.expired-card {
  background-color: #f0f0f0 !important;
  color: #999 !important;
  border-color: #dcdfe6 !important;
  cursor: not-allowed;
}

.room-card.expired-card .room-name-cn,
.room-card.expired-card .room-name-en,
.room-card.expired-card .bed-type {
  background-color: #e0e0e0 !important;
  color: #888 !important;
  filter: grayscale(100%);
}

.expired-tag {
  position: absolute;
  right: 10px;
  bottom: 10px;
  font-size: 16px;
  color: #999;
  font-weight: bold;
  transform: rotate(-15deg);
  border: 2px solid #999;
  padding: 2px 8px;
  border-radius: 4px;
  opacity: 0.6;
  pointer-events: none;
}

.time-info {
  margin-top: 6px;
  font-size: 11px;
  color: #888;
  line-height: 1.3;
  border-top: 1px dashed #eee;
  padding-top: 4px;
}

.room-names {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-bottom: 2px;
}

.room-name-cn {
  background: #fff7d6;
  color: #b08900;
  font-weight: bold;
  padding: 2px 6px;
  border-radius: 5px;
}

.room-name-en {
  background: #e6f7ff;
  color: #007acc;
  font-weight: bold;
  padding: 2px 6px;
  border-radius: 5px;
  margin-top: 2px;
}

.room-code {
  color: #aaa;
  font-size: 12px;
  margin-top: 3px;
}

.bed-type {
  width: 100%;
  margin-top: 3px;
  background: #e9fbe7;
  color: #179d25;
  font-size: 12px;
  padding: 1px 4px;
  border-radius: 4px;
  display: inline-block;
}

.badge-layer {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  padding: 4px;
  pointer-events: none;
  display: flex;
  justify-content: flex-end;
  pointer-events: none;
  flex-direction: row;
  flex-wrap: nowrap;
}

.sup-flag-badge {
  background: #409eff;
  color: #fff;
  font-size: 12px;
  padding: 0 7px;
  border-radius: 10px;
  font-weight: bold;
  z-index: 5;
  pointer-events: auto;
}

.createBy-flag-badge {
  background-color: #f56c6c;
  color: white;
  font-size: 10px;
  border-radius: 10px;
  box-shadow: 0 0 2px rgba(0, 0, 0, 0.15);
  white-space: nowrap;
  z-index: 10;
  margin-right: 5px;
  pointer-events: auto;
}
</style>

<style scoped>
.room-card {
  background: var(--el-bg-color);
  color: var(--el-text-color-primary);
  border: 1px solid var(--el-border-color);
}

.room-card:hover {
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.2);
  border-color: var(--el-color-primary);
}

.room-name-cn {
  background: var(--el-color-warning-light-9);
  color: var(--el-color-warning);
}

.room-name-en {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.bed-type {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
}

.sup-flag-badge {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.createBy-flag-badge {
  background-color: var(--el-color-danger);
  color: var(--el-color-white);
}
</style>
