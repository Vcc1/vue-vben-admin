<template>
  <el-dialog v-model="visible" :title="form.id ? '编辑词条' : '新增词条'" width="600px" @close="onClose">
    <el-form :model="form" label-width="120px">
      <el-form-item label="词分类">
        <el-select v-model="form.wordType_Id" placeholder="请选择词分类" style="width: 100%">
          <el-option v-for="item in wordTypeListOptions" :key="item.id" :label="item.worldType" :value="item.id" />
        </el-select>
      </el-form-item>
      <el-form-item label="词名称">
        <el-input v-model="form.wordTitle" />
      </el-form-item>
      <el-form-item label="词内容">
        <el-select
          v-model="form.wordsList"
          multiple
          filterable
          allow-create
          default-first-option
          placeholder="请输入词，回车确认"
          style="width: 100%"
        />
      </el-form-item>

      <el-form-item label="词性质">
        <el-select v-model="form.effectType">
          <el-option label="必须" :value="0" />
          <el-option label="增益" :value="1" />
          <el-option label="减益" :value="2" />
        </el-select>
      </el-form-item>

      <el-form-item label="是否必须词">
        <el-radio-group v-model="form.isMustNeed">
          <el-radio :label="'Y'">是</el-radio>
          <el-radio :label="'N'">否</el-radio>
          <el-radio :label="''">跟随分类</el-radio>
        </el-radio-group>
      </el-form-item>

      <el-form-item label="是否OTA忽略">
        <el-switch v-model="form.isExcludedInOtaRoom" />
      </el-form-item>

      <el-form-item label="Level积分">
        <el-input-number v-model="form.level" :min="-10" :max="10" />
      </el-form-item>

      <el-form-item label="兼容词">
        <el-select-v2
          v-model="form.compatibleWords"
          :options="wordListOptions"
          :props="{
            value: 'id',
            label: 'wordTitle',
          }"
          multiple
          filterable
          placeholder="选择兼容词"
          style="width: 100%"
        />
      </el-form-item>
      <el-form-item label="互斥词">
        <el-select-v2
          v-model="form.conflictWords"
          :options="wordListOptions"
          :props="{
            value: 'id',
            label: 'wordTitle',
          }"
          multiple
          filterable
          placeholder="选择互斥词"
          style="width: 100%"
        />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" @click="submit">保存</el-button>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, defineExpose } from 'vue';
import { ElMessageBox } from 'element-plus';
import { useWordApi } from '#/api';

const visible = ref(false);
const form = ref({});

const props = defineProps({
  wordListOptions: {
    type: Array,
    default: () => [],
  },
  wordTypeListOptions: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['submit', 'reloadData']);

function open(word = {}) {
  let parsedWords = [];
  if (typeof word.wordsJson === 'string') {
    try {
      parsedWords = JSON.parse(word.wordsJson);
    } catch {
      parsedWords = [];
    }
  } else if (Array.isArray(word.wordsJson)) {
    parsedWords = word.wordsJson;
  }

  form.value = {
    isMustNeed: '',
    isExcludedInOtaRoom: false,
    level: 0,
    wordsJson: '',
    wordsList: parsedWords,
    similarWordIds: [],
    mutuallyExclusiveWordIds: [],
    effectType: 0,
    ...word,
  };

  form.value.wordsList = parsedWords;
  visible.value = true;
}

function onClose() {
  form.value = {};
}

function renderConfirmContent(conflicts) {
  return conflicts
    .map(item => {
      return `<p><strong>${item.wordTitle}</strong> 中存在重复词：<span style="color:red">${item.duplicatedWords.join('、')}</span>，将被移除。</p>`;
    })
    .join('');
}

const { Update } = useWordApi();

function submit() {
  const currentId = form.value.id;
  const currentWords = new Set(
    form.value.wordsList
      .map(w => w.trim())
      .filter(Boolean),
  );

  const updateList = [];
  const conflicts = [];

  const currentItem = JSON.parse(JSON.stringify(form.value));
  currentItem.wordsJson = JSON.stringify([...currentWords]);
  currentItem.similarWordIds = JSON.stringify(form.value.compatibleWords ?? []);
  currentItem.mutuallyExclusiveWordIds = JSON.stringify(form.value.conflictWords ?? []);
  updateList.push(currentItem);

  props.wordListOptions.forEach(item => {
    if (!item.wordsJson) return;
    if (item.id === currentId) return;

    let parsed = [];
    try {
      parsed = typeof item.wordsJson === 'string' ? JSON.parse(item.wordsJson) : item.wordsJson;
    } catch {
      parsed = [];
    }

    const oldWords = parsed.map(w => w.trim()).filter(Boolean);
    const remainingWords = oldWords.filter(word => !currentWords.has(word));
    const duplicatedWords = oldWords.filter(word => currentWords.has(word));

    if (duplicatedWords.length > 0) {
      conflicts.push({
        id: item.id,
        wordTitle: item.wordTitle,
        duplicatedWords,
      });

      const clonedItem = JSON.parse(JSON.stringify(item));
      clonedItem.wordsJson = JSON.stringify(remainingWords);
      clonedItem.similarWordIds = JSON.stringify(item.compatibleWords ?? []);
      clonedItem.mutuallyExclusiveWordIds = JSON.stringify(item.conflictWords ?? []);
      updateList.push(clonedItem);
    }
  });

  if (conflicts.length > 0) {
    ElMessageBox.confirm(renderConfirmContent(conflicts), '存在重复词', {
      confirmButtonText: '继续保存',
      cancelButtonText: '取消',
      dangerouslyUseHTMLString: true,
      type: 'warning',
    })
      .then(() => {
        doSubmit(updateList);
      })
      .catch(() => {});
  } else {
    doSubmit(updateList);
  }
}

function doSubmit(updateList) {
  Update(updateList).then(() => {
    emit('reloadData');
    visible.value = false;
  });
}

defineExpose({ open });
</script>
