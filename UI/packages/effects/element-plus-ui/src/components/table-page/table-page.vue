<script lang="ts" setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, toRef, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  ElButton,
  ElCard,
  ElCheckbox,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
  ElPagination,
  ElPopover,
  ElTable,
  ElTableColumn,
  ElTooltip,
} from 'element-plus';

import type { TablePageColumnDef } from './types';
import { useColumnConfig } from './use-column-config';
import { useDensity } from './use-density';

const props = withDefaults(
  defineProps<{
    tableKey: string;
    title?: string;
    columns: TablePageColumnDef[];
    data: Record<string, any>[];
    loading?: boolean;
    total?: number;
    page?: number;
    pageSize?: number;
    pageSizes?: number[];
    pagination?: boolean;
    stripe?: boolean;
    rowClassName?: string | ((args: any) => string);
    paginationLayout?: string;
  }>(),
  {
    loading: false,
    total: 0,
    page: 1,
    pageSize: 20,
    pageSizes: () => [20, 50, 100],
    pagination: true,
    stripe: true,
    paginationLayout: 'total, sizes, prev, pager, next, jumper',
  },
);

const emit = defineEmits<{
  refresh: [];
  'row-click': [row: Record<string, any>];
  'update:page': [page: number];
  'update:pageSize': [pageSize: number];
  'page-change': [page: number];
  'size-change': [pageSize: number];
}>();

const columnsRef = toRef(props, 'columns');
const { density, rowHeight, load: loadDensity, handleChange: handleDensityChange } = useDensity(props.tableKey);

function onDensityChange(val: string) {
  handleDensityChange(val);
  nextTick(updateHeaderStickyTop);
}
const {
  columnConfigs,
  orderedVisibleColumns,
  draggingField,
  load: loadColumnConfig,
  toggleColumnVisibility,
  getColumnFixed,
  getColumnsByFixed,
  handleDragStart,
  handleDragOver,
  handleDrop,
  handleDragEnd,
  save: saveColumnConfig,
} = useColumnConfig(props.tableKey, columnsRef);

const columnMap = computed(() => {
  const map = new Map<string, TablePageColumnDef>();
  for (const col of props.columns) {
    map.set(col.field, col);
  }
  return map;
});

const cardRef = ref<HTMLElement | null>(null);
let headerStickyRaf = 0;

function getCardEl(): HTMLElement | null {
  const r = cardRef.value;
  if (!r) return null;
  return (r as any).$el ?? r;
}

function updateHeaderStickyTop() {
  const card = getCardEl();
  if (!card) return;
  const toolbar = card.querySelector('.table-toolbar') as HTMLElement | null;
  const header = card.querySelector('.el-table__header-wrapper') as HTMLElement | null;
  if (toolbar && header && toolbar.offsetHeight > 0) {
    header.style.setProperty('top', `${toolbar.offsetHeight}px`);
  } else {
    cancelAnimationFrame(headerStickyRaf);
    headerStickyRaf = requestAnimationFrame(updateHeaderStickyTop);
  }
}

function handlePageChange(val: number) {
  emit('update:page', val);
  emit('page-change', val);
}

function handleSizeChange(val: number) {
  emit('update:pageSize', val);
  emit('update:page', 1);
  emit('size-change', val);
}

watch(() => props.columns, () => {
  loadColumnConfig();
});

onMounted(() => {
  loadDensity();
  loadColumnConfig();
  nextTick(updateHeaderStickyTop);
});

onBeforeUnmount(() => {
  cancelAnimationFrame(headerStickyRaf);
});
</script>

<template>
  <ElCard ref="cardRef" class="table-page-card">
    <!-- Toolbar -->
    <div class="table-toolbar">
      <div class="toolbar-left">
        <span v-if="title" class="toolbar-title">{{ title }}</span>
        <slot name="toolbar-left" />
      </div>
      <div class="toolbar-actions">
        <slot name="toolbar-extra" />
        <ElTooltip content="刷新" placement="top">
          <ElButton circle size="small" @click="emit('refresh')">
            <IconifyIcon icon="lucide:refresh-cw" class="text-sm" />
          </ElButton>
        </ElTooltip>
        <ElDropdown trigger="click" @command="onDensityChange">
          <ElButton circle size="small">
            <IconifyIcon icon="lucide:rows-3" class="text-sm" />
          </ElButton>
          <template #dropdown>
            <ElDropdownMenu>
              <ElDropdownItem command="compact" :class="{ 'is-active': density === 'compact' }">
                紧凑
              </ElDropdownItem>
              <ElDropdownItem command="default" :class="{ 'is-active': density === 'default' }">
                默认
              </ElDropdownItem>
              <ElDropdownItem command="loose" :class="{ 'is-active': density === 'loose' }">
                宽松
              </ElDropdownItem>
            </ElDropdownMenu>
          </template>
        </ElDropdown>
        <ElPopover placement="bottom-end" trigger="click" :width="240">
          <template #reference>
            <ElButton circle size="small">
              <IconifyIcon icon="lucide:settings-2" class="text-sm" />
            </ElButton>
          </template>
          <div class="column-settings">
            <div class="column-settings-title">列展示</div>
            <div class="column-section">
              <div class="section-label">固定在左侧</div>
              <div
                class="column-drop-zone"
                @dragover="handleDragOver($event, 'left')"
                @drop="handleDrop"
              >
                <div
                  v-for="col in getColumnsByFixed('left')"
                  :key="col.field"
                  :data-field="col.field"
                  class="column-item"
                  draggable="true"
                  @dragstart="handleDragStart(col.field)"
                  @dragend="handleDragEnd"
                >
                  <span class="drag-handle"></span>
                  <ElCheckbox
                    :model-value="col.visible"
                    @mousedown.stop
                    @change="toggleColumnVisibility(col.field)"
                  >
                    {{ columnMap.get(col.field)?.label }}
                  </ElCheckbox>
                </div>
              </div>
            </div>
            <div class="column-section">
              <div class="section-label">不固定</div>
              <div
                class="column-drop-zone"
                @dragover="handleDragOver($event, false)"
                @drop="handleDrop"
              >
                <div
                  v-for="col in getColumnsByFixed(false)"
                  :key="col.field"
                  :data-field="col.field"
                  class="column-item"
                  draggable="true"
                  @dragstart="handleDragStart(col.field)"
                  @dragend="handleDragEnd"
                >
                  <span class="drag-handle">⋮</span>
                  <ElCheckbox
                    :model-value="col.visible"
                    @mousedown.stop
                    @change="toggleColumnVisibility(col.field)"
                  >
                    {{ columnMap.get(col.field)?.label }}
                  </ElCheckbox>
                </div>
              </div>
            </div>
            <div class="column-section">
              <div class="section-label">固定在右侧</div>
              <div
                class="column-drop-zone"
                @dragover="handleDragOver($event, 'right')"
                @drop="handleDrop"
              >
                <div
                  v-for="col in getColumnsByFixed('right')"
                  :key="col.field"
                  :data-field="col.field"
                  class="column-item"
                  draggable="true"
                  @dragstart="handleDragStart(col.field)"
                  @dragend="handleDragEnd"
                >
                  <span class="drag-handle">⋮</span>
                  <ElCheckbox
                    :model-value="col.visible"
                    @mousedown.stop
                    @change="toggleColumnVisibility(col.field)"
                  >
                    {{ columnMap.get(col.field)?.label }}
                  </ElCheckbox>
                </div>
              </div>
            </div>
          </div>
        </ElPopover>
      </div>
    </div>

    <!-- Table -->
    <ElTable
      :data="data"
      v-loading="loading"
      :stripe="stripe"
      :row-class-name="rowClassName"
      :row-style="{ height: rowHeight + 'px' }"
      @row-click="(row: Record<string, any>) => emit('row-click', row)"
    >
      <ElTableColumn
        v-for="field in orderedVisibleColumns"
        :key="field"
        :label="columnMap.get(field)?.label"
        :width="columnMap.get(field)?.width"
        :min-width="columnMap.get(field)?.minWidth"
        :align="columnMap.get(field)?.align"
        :fixed="getColumnFixed(field) || undefined"
      >
        <template #default="scope">
          <slot :name="`column-${field}`" v-bind="scope">
            {{ scope.row[field] }}
          </slot>
        </template>
      </ElTableColumn>
    </ElTable>

    <!-- Sticky bottom bar -->
    <div v-if="pagination" class="sticky-bottom-bar">
      <div class="pagination-bar">
        <ElPagination
          :current-page="page"
          :page-size="pageSize"
          :total="total"
          :page-sizes="pageSizes"
          :layout="paginationLayout"
          @current-change="handlePageChange"
          @size-change="handleSizeChange"
        />
      </div>
    </div>
  </ElCard>
</template>

<style scoped>
.table-page-card {
  overflow: visible;
}

.table-page-card :deep(.el-card__body) {
  overflow: visible;
  padding: 0;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: var(--el-card-bg-color);
  border-bottom: 1px solid var(--el-border-color-lighter);
  position: sticky;
  top: 0;
  z-index: 10;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.toolbar-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-actions .el-button {
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  transition: all 0.2s;
}

.toolbar-actions .el-button:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.column-settings {
  padding: 4px 0;
}

.column-settings-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  margin-bottom: 8px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.column-section {
  margin-bottom: 12px;
}

.column-section:last-child {
  margin-bottom: 0;
}

.section-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 6px;
  padding-left: 4px;
}

.column-drop-zone {
  min-height: 40px;
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
  padding: 4px;
  transition: background-color 0.2s;
}

.column-drop-zone:has(.column-item:hover) {
  background: var(--el-fill-color);
}

.column-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  background: var(--el-bg-color);
  border-radius: 4px;
  margin-bottom: 4px;
  cursor: move;
  transition: all 0.2s;
  user-select: none;
}

.column-item:last-child {
  margin-bottom: 0;
}

.column-item:hover {
  background: var(--el-fill-color-light);
}

.column-item:active {
  opacity: 0.7;
}

.drag-handle {
  color: var(--el-text-color-placeholder);
  font-size: 14px;
  cursor: grab;
  flex-shrink: 0;
}

.drag-handle:active {
  cursor: grabbing;
}

.column-item .el-checkbox {
  flex: 1;
}

.table-page-card :deep(.el-table) {
  --el-table-row-hover-bg-color: var(--el-fill-color-light);
  overflow: visible;
}

.table-page-card :deep(.el-table__inner-wrapper) {
  overflow: visible;
}

.table-page-card :deep(.el-table__header-wrapper) {
  position: sticky;
  top: 0;
  z-index: 5;
  background: var(--el-card-bg-color);
}

.table-page-card :deep(.el-table th) {
  background: var(--el-fill-color-light) !important;
  font-weight: 600;
}

.table-page-card :deep(.el-table .el-table__body tr td) {
  transition: background-color 0.2s;
}

.table-page-card :deep(.is-active) {
  color: var(--el-color-primary);
  font-weight: 600;
}

.sticky-bottom-bar {
  position: sticky;
  bottom: 0;
  z-index: 6;
  background: var(--el-card-bg-color);
  border-top: 1px solid var(--el-border-color-lighter);
}

.pagination-bar {
  padding: 12px 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
