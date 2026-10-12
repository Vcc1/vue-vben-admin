<template>
  <div class="page-container layout-pd">
    <el-card shadow="hover" class="word-card fill-card">
      <el-input
        v-model="search"
        placeholder="搜索词名称或词内容..."
        clearable
        class="search-box"
        prefix-icon="Search"
      />

      <div class="main-content">
        <el-col :span="4" class="left-col">
          <el-menu :default-active="activeTypeId" @select="handleTypeSelect" class="type-menu">
            <el-menu-item index="ALL">
              所有词 <span class="count">({{ searchedWords.length }})</span>
            </el-menu-item>
            <el-menu-item v-for="type in wordTypes" :key="type.id" :index="type.id.toString()">
              {{ type.worldType }}
              <span class="count">({{ getWordCountByType(type.id) }})</span>
              <span class="is-must-right">{{ type.isMustNeed }}</span>
            </el-menu-item>
          </el-menu>
        </el-col>

        <el-col :span="20" class="right-col">
          <div class="table-wrapper">
            <div style="height: 38px; text-align: left">
              <el-button type="primary" @click="handleEdit()">添加词</el-button>
            </div>
            <el-table :data="paginatedData" stripe border height="calc(100% - 90px)" highlight-current-row>
              <el-table-column prop="id" label="标识" width="120" />
              <el-table-column prop="wordTitle" label="词名称" width="120" />
              <el-table-column label="" width="50" align="right">
                <template #default="{ row }">
                  <span v-if="row.isMustNeed" style="color: blueviolet">{{ getIsMustNeed(row) }}</span>
                  <span v-else style="color: chartreuse">{{ getIsMustNeed(row) }}</span>
                </template>
              </el-table-column>
              <el-table-column label="词性质" width="80">
                <template #default="{ row }">
                  {{ getEffectTypeName(row.effectType) }}
                </template>
              </el-table-column>
              <el-table-column label="词" min-width="200">
                <template #default="{ row }">
                  <el-tag
                    v-for="(word, index) in row.parsedWords"
                    :key="index"
                    class="word-tag clickable"
                    :class="{ highlight: isMatchSearch(word) }"
                  >
                    {{ word }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column label="评分" width="60" align="center">
                <template #default="{ row }">
                  {{ row.level ?? getWordType(row.wordType_Id)?.level ?? '-' }}
                </template>
              </el-table-column>
              <el-table-column label="OTA忽略" width="80" align="center">
                <template #default="{ row }">
                  {{ row.isExcludedInOtaRoom ? '是' : '否' }}
                </template>
              </el-table-column>
              <el-table-column label="操作" width="100">
                <template #default="{ row }">
                  <el-button type="text" size="small" @click="handleEdit(row)">编辑</el-button>
                </template>
              </el-table-column>
            </el-table>
            <el-pagination
              v-model:current-page="page"
              :page-size="pageSize"
              :total="filteredWords.length"
              layout="prev, pager, next, jumper"
              background
              class="pagination-bar"
            />
          </div>
        </el-col>
      </div>
    </el-card>
    <WordEdit
      ref="wordEditRef"
      @reloadData="reloadData"
      :wordTypeListOptions="wordTypes"
      :wordListOptions="allWords"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { useWordApi } from '#/api';
import WordEdit from './WordEdit.vue';

const search = ref('');
const allWords = ref([]);
const wordTypes = ref([]);
const activeTypeId = ref('ALL');
const page = ref(1);
const pageSize = ref(100);

const wordEditRef = ref();
function handleEdit(word) {
  wordEditRef.value.open(word);
}

const { GetData } = useWordApi();

onMounted(async () => {
  try {
    reloadData();
  } catch {
    ElMessage.error('获取数据失败');
  }
});

async function reloadData() {
  const res = await GetData();
  allWords.value = (res.words ?? []).map(w => ({
    ...w,
    parsedWords: safeParseJson(w.wordsJson),
    compatibleWords: safeParseJson(w.similarWordIds),
    conflictWords: safeParseJson(w.mutuallyExclusiveWordIds),
  }));
  wordTypes.value = res.wordTypes;
}

function safeParseJson(str) {
  try {
    const parsed = JSON.parse(str);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

const searchedWords = computed(() => {
  return allWords.value.filter(
    w =>
      !search.value.toLowerCase() ||
      w.wordTitle?.toLowerCase().includes(search.value.toLowerCase()) ||
      w.parsedWords.some(p => p.toLowerCase().includes(search.value.toLowerCase())),
  );
});

const filteredWords = computed(() => {
  return searchedWords.value.filter(
    w => activeTypeId.value === 'ALL' || w.wordType_Id?.toString() === activeTypeId.value,
  );
});

const paginatedData = computed(() => {
  const start = (page.value - 1) * pageSize.value;
  return filteredWords.value.slice(start, start + pageSize.value);
});

function handleTypeSelect(id) {
  activeTypeId.value = id;
  page.value = 1;
}

function getWordCountByType(typeId) {
  return searchedWords.value.filter(w => w.wordType_Id?.toString() === typeId.toString()).length;
}

function isMatchSearch(word) {
  return search.value.toLowerCase() && word.toLowerCase().includes(search.value.toLowerCase());
}

function getWordType(typeId) {
  return wordTypes.value.find(t => t.id === typeId);
}

function getIsMustNeed(word) {
  if (word.isMustNeed != null && word.isMustNeed != '' && word.isMustNeed)
    return word.isMustNeed === 'Y' ? 'Y' : 'N';
  return getWordType(word.wordType_Id)?.isMustNeed === 'Y' ? 'Y' : 'N';
}

function getEffectTypeName(type) {
  switch (type) {
    case 0:
      return '必须';
    case 1:
      return '增益';
    case 2:
      return '减益';
    default:
      return '-';
  }
}
</script>

<style scoped lang="scss">
.page-container.layout-pd {
  height: 100%;
  box-sizing: border-box;
  padding: 20px;
  background-color: var(--el-bg-color-page);
}

.word-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  border-radius: 12px;
  padding: 20px;
  box-sizing: border-box;

  ::v-deep(.el-card__body) {
    flex: 1 1 auto;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }
}

.search-box {
  width: 300px;
  margin-bottom: 12px;
}

.main-content {
  flex: 1;
  display: flex;
  height: 100%;
  overflow: hidden;

  .left-col {
    height: 100%;
    overflow: hidden;
  }

  .right-col {
    height: 100%;
    padding-left: 20px;
    overflow: hidden;
  }
}

.type-menu {
  height: 100%;
  overflow-y: auto;
  border-right: 1px solid var(--el-border-color);
  background-color: var(--el-bg-color);

  .el-menu-item {
    border-radius: 5px;
    margin: 4px;
  }

  .el-menu-item.is-active {
    background-color: var(--el-color-primary-light-9);
    color: var(--el-color-primary);
  }
}

.table-wrapper {
  height: 100%;
  overflow-y: auto;
  padding-right: 10px;
  box-sizing: border-box;
}

.word-tag {
  margin: 2px;
  font-size: 12px;
  background-color: var(--el-color-info-light-9);
  color: var(--el-color-info);
  border: 1px solid var(--el-color-info-light-5);
}

.table-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  box-sizing: border-box;
  overflow: hidden;

  .table-body {
    flex: 1;
    height: calc(100% - 52px);
    overflow-y: auto;
  }

  .pagination-bar {
    padding: 10px 0;
    text-align: right;
  }
}

.el-menu-item {
  display: flex;
  justify-content: space-between;
  align-items: center;

  .count {
    margin-left: 5px;
    color: gray;
  }

  .is-must-right {
    margin-left: auto;
    color: red;
    font-weight: bold;
  }
}

.highlight {
  background-color: #fff3cd;
  color: #d48806;
  font-weight: 600;
  border-radius: 3px;
  padding: 0 4px;
}
</style>
