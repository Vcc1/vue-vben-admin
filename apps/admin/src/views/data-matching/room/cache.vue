<template>
  <div class="page-container layout-pd">
    <el-card shadow="hover" class="main-card">
      <div class="main-layout">
        <div class="main-left">
          <el-form :inline="true" @submit.prevent>
            <el-form-item label="">
              <el-input v-model="code" placeholder="请输入MTHotelCode" size="default" class="ml10" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="fetchData" class="ml10" size="default">查询</el-button>
            </el-form-item>
          </el-form>
          <el-tabs v-if="rawData" class="mt-4" style="height: calc(100vh - 250px)">
            <el-tab-pane label="原始数据(JSON)" style="height: 100%; overflow: auto">
              <el-scrollbar class="json-box">
                <pre>{{ formattedJson }}</pre>
              </el-scrollbar>
            </el-tab-pane>

            <el-tab-pane label="母房型列表" style="height: 100%; overflow: auto">
              <el-table :data="tableData" style="width: 100%; height: 100%" border>
                <el-table-column prop="r" label="房型ID" width="100" />
                <el-table-column prop="c" label="房型中文" />
                <el-table-column prop="e" label="房型英文" />
                <el-table-column prop="b" label="词组编号" width="120" />
              </el-table>
            </el-tab-pane>

            <el-tab-pane label="子房型匹配" style="height: 100%; overflow: auto">
              <el-table :data="subMatchTableData" border style="width: 100%; height: 100%">
                <el-table-column prop="keyword" label="子房型关键词" />
                <el-table-column prop="key" label="匹配词组" />
                <el-table-column prop="r" label="母房型ID" width="100" />
                <el-table-column prop="c" label="母房型中文" />
                <el-table-column prop="e" label="母房型英文" />
                <el-table-column prop="t" label="匹配类型" width="100" />
                <el-table-column prop="s" label="状态" width="80" />
              </el-table>
            </el-tab-pane>
          </el-tabs>
          <el-empty
            v-else
            class="empty-hint"
            description="请先输入 MTHotelCode 并点击查询"
            style="height: calc(100vh - 250px)"
            :image-size="180"
          />
        </div>
        <div class="history-panel" v-if="!isMobile">
          <div class="history-header">搜索历史</div>
          <div class="history-list">
            <div v-for="item in historyList" :key="item" class="history-item" @click="handleHistoryClick(item)">
              {{ item }}
            </div>
          </div>
        </div>
      </div>

      <el-button class="history-toggle" v-if="isMobile" type="primary" circle @click="showDrawer = true">
        历
      </el-button>
      <el-drawer v-model="showDrawer" title="搜索历史" direction="rtl">
        <div v-for="item in historyList" :key="item" class="history-item" @click="handleHistoryClick(item)">
          {{ item }}
        </div>
      </el-drawer>
    </el-card>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue';
import { useRoomMatchApi } from '#/api';
import { ElMessage } from 'element-plus';

const code = ref('');
const rawData = ref<any>(null);
const tableData = ref<any[]>([]);
const subMatchTableData = ref<any[]>([]);
const historyList = ref<string[]>([]);
const showDrawer = ref(false);
const isMobile = ref(false);

const HISTORY_KEY = 'room_match_cache_history';

const formattedJson = computed(() => (rawData.value ? JSON.stringify(rawData.value, null, 2) : ''));

const fetchData = async () => {
  const inputCode = code.value.trim();
  if (!inputCode) {
    ElMessage.warning('请输入 MTHotelCode');
    return;
  }
  try {
    const { GetHotelRoomMappingCache } = useRoomMatchApi();
    const res = await GetHotelRoomMappingCache({ MTHotelCode: inputCode });
    rawData.value = res;
    tableData.value = Object.values(res.m || {});
    subMatchTableData.value = parseSubRooms(res.s || {}, res.m || {});

    saveHistory(inputCode);
  } catch {
    ElMessage.error('查询失败');
  }
};

const parseSubRooms = (sData: any, mData: any) => {
  const list = [];
  for (const keyword in sData) {
    const wordMatches = sData[keyword];
    for (const key in wordMatches) {
      const match = wordMatches[key];
      const r = match.r;
      const mother = mData?.[r] || {};
      list.push({
        keyword,
        key,
        r,
        c: mother.c || '',
        e: mother.e || '',
        t: match.t,
        s: match.s,
      });
    }
  }
  return list;
};

const saveHistory = (newItem: string) => {
  let list = JSON.parse(localStorage.getItem(HISTORY_KEY) || '[]');
  list = list.filter(i => i !== newItem);
  list.unshift(newItem);
  if (list.length > 100) list = list.slice(0, 100);
  localStorage.setItem(HISTORY_KEY, JSON.stringify(list));
  historyList.value = list;
};

const loadHistory = () => {
  const list = JSON.parse(localStorage.getItem(HISTORY_KEY) || '[]');
  historyList.value = list;
};

const handleHistoryClick = (item: string) => {
  code.value = item;
  fetchData();
  showDrawer.value = false;
};

onMounted(() => {
  loadHistory();
  window.addEventListener('resize', updateIsMobile);
  updateIsMobile();
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateIsMobile);
});

const updateIsMobile = () => {
  isMobile.value = window.innerWidth < 1920;
};
</script>

<style scoped>
.page-container {
  padding: 20px;
}

.main-layout {
  display: flex;
}

.main-left {
  flex: 1;
  margin-right: 20px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.history-panel {
  width: 200px;
  border-left: 1px solid #ebeef5;
  padding-left: 12px;
  overflow-y: auto;
}

.history-header {
  font-weight: bold;
  margin-bottom: 10px;
}

.history-list {
  max-height: calc(100vh - 250px);
  overflow-y: auto;
}

.history-item {
  padding: 4px 8px;
  cursor: pointer;
  border-radius: 4px;
}

.history-item:hover {
  background-color: #f5f5f5;
}

.history-toggle {
  position: fixed;
  right: 20px;
  bottom: 80px;
  z-index: 1001;
}
</style>
