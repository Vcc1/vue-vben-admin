<script lang="ts" setup generic="T extends object = Record<string, any>">
import {
  Comment,
  Fragment,
  Text,
  computed,
  isVNode,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  toRef,
  watch,
} from 'vue';

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

import type { TableInstance } from 'element-plus';
import type {
  TablePageColumnDef,
  TablePageDensity,
  TablePageEmits,
  TablePageInstance,
  TablePageProps,
  TablePageSlots,
} from './types';
import { formatColumnValue, getColumnValue } from './column-value';
import { useColumnConfig } from './use-column-config';
import { useDensity } from './use-density';

const props = withDefaults(defineProps<TablePageProps<T>>(), {
  loading: false,
  total: 0,
  page: 1,
  pageSize: 20,
  pageSizes: () => [20, 50, 100],
  pagination: true,
  stripe: true,
  scrollbarAlwaysOn: true,
  paginationLayout: 'total, sizes, prev, pager, next, jumper',
  showRefresh: true,
  showDensity: true,
  showColumnSettings: true,
});

const emit = defineEmits<TablePageEmits<T>>();
const slots = defineSlots<TablePageSlots<T>>();
const tableRef = ref<TableInstance>();
function hasSlotContent(nodes: unknown[]): boolean {
  return nodes.some((node) => {
    if (Array.isArray(node)) return hasSlotContent(node);
    if (!isVNode(node)) return typeof node === 'string' && node.trim() !== '';
    if (node.type === Comment) return false;
    if (node.type === Fragment) {
      return Array.isArray(node.children) && hasSlotContent(node.children);
    }
    if (node.type === Text) return String(node.children ?? '').trim() !== '';
    return true;
  });
}

function showToolbar() {
  return Boolean(
    props.title?.trim() ||
    props.showRefresh ||
    props.showDensity ||
    props.showColumnSettings ||
    hasSlotContent(slots['toolbar-left']?.() ?? []) ||
    hasSlotContent(slots['toolbar-extra']?.() ?? []),
  );
}

const columnsRef = toRef(props, 'columns');
const {
  density,
  rowHeight,
  load: loadDensity,
  handleChange: handleDensityChange,
} = useDensity(toRef(props, 'tableKey'));

function onDensityChange(val: string) {
  if (!['compact', 'default', 'loose'].includes(val)) return;
  handleDensityChange(val as TablePageDensity);
  nextTick(updateStickyMetrics);
}
const {
  orderedVisibleColumns,
  toggleColumnVisibility,
  getColumnFixed,
  getColumnsByFixed,
  handleDragStart,
  handleDragOver,
  handleDrop,
  handleDragEnd,
  reset: resetColumns,
} = useColumnConfig(toRef(props, 'tableKey'), columnsRef);

const columnMap = computed(() => {
  const map = new Map<string, TablePageColumnDef<T>>();
  for (const col of props.columns) {
    map.set(col.field, col);
  }
  return map;
});
const visibleColumns = computed(() =>
  orderedVisibleColumns.value.flatMap((field) => {
    const column = columnMap.value.get(field);
    return column ? [column] : [];
  }),
);

const tableProps = computed(() => {
  const options = props.tableProps;
  if (!options) return {};
  return {
    border: options.border,
    defaultSort: options.defaultSort,
    emptyText: options.emptyText,
    highlightCurrentRow: options.highlightCurrentRow,
    spanMethod: options.spanMethod,
    tooltipOptions: options.tooltipOptions,
  };
});
const rowKey = computed(() => props.rowKey);

const cardRef = ref<HTMLElement | null>(null);
let stickyMetricsObserver: ResizeObserver | null = null;
let stickyElementsObserver: MutationObserver | null = null;
let observedStickyElements: (HTMLElement | null)[] = [];

function getCardEl(): HTMLElement | null {
  const r = cardRef.value;
  if (!r) return null;
  return (r as any).$el ?? r;
}

function updateStickyMetrics() {
  const card = getCardEl();
  if (!card) return;
  const toolbar = card.querySelector('.table-toolbar') as HTMLElement | null;
  const bottomBar = card.querySelector(
    '.sticky-bottom-bar',
  ) as HTMLElement | null;

  card.style.setProperty(
    '--table-page-toolbar-height',
    `${toolbar?.offsetHeight ?? 0}px`,
  );
  card.style.setProperty(
    '--table-page-bottom-bar-height',
    `${bottomBar?.offsetHeight ?? 0}px`,
  );
}

function observeStickyMetrics() {
  const card = getCardEl();
  if (!card) return;

  const elements = [
    card.querySelector<HTMLElement>('.table-toolbar'),
    card.querySelector<HTMLElement>('.sticky-bottom-bar'),
  ];
  if (elements.some((el, index) => el !== observedStickyElements[index])) {
    stickyMetricsObserver ??= new ResizeObserver(updateStickyMetrics);
    stickyMetricsObserver.disconnect();
    for (const el of elements) {
      if (el) stickyMetricsObserver.observe(el);
    }
    observedStickyElements = elements;
  }
  updateStickyMetrics();
}

function handlePageChange(val: number) {
  emit('update:page', val);
  emit('page-change', val);
  emit('pagination-change', { page: val, pageSize: props.pageSize });
}

function handleSizeChange(val: number) {
  emit('update:pageSize', val);
  emit('update:page', 1);
  emit('size-change', val);
  emit('pagination-change', { page: 1, pageSize: val });
}

watch(() => props.tableKey, loadDensity);

onMounted(() => {
  loadDensity();
  nextTick(() => {
    observeStickyMetrics();
    const body = getCardEl()?.querySelector('.el-card__body');
    if (body) {
      stickyElementsObserver = new MutationObserver(observeStickyMetrics);
      stickyElementsObserver.observe(body, { childList: true });
    }
  });
});

onBeforeUnmount(() => {
  stickyMetricsObserver?.disconnect();
  stickyElementsObserver?.disconnect();
});

defineExpose<TablePageInstance<T>>({
  resetColumns,
  clearSelection: () => tableRef.value?.clearSelection(),
  getSelectionRows: (): T[] => tableRef.value?.getSelectionRows() ?? [],
  toggleRowSelection: (row: T, selected?: boolean) =>
    tableRef.value?.toggleRowSelection(row, selected),
});
</script>

<template>
  <ElCard ref="cardRef" class="table-page-card">
    <!-- Toolbar -->
    <div v-if="showToolbar()" class="table-toolbar">
      <div class="toolbar-left">
        <span v-if="title" class="toolbar-title">{{ title }}</span>
        <slot name="toolbar-left" />
      </div>
      <div class="toolbar-actions">
        <slot name="toolbar-extra" />
        <ElTooltip v-if="showRefresh" content="刷新" placement="top">
          <ElButton
            circle
            size="small"
            :loading="loading"
            aria-label="刷新"
            @click="emit('refresh')"
          >
            <IconifyIcon icon="lucide:refresh-cw" class="text-sm" />
          </ElButton>
        </ElTooltip>
        <ElDropdown
          v-if="showDensity"
          trigger="click"
          @command="onDensityChange"
        >
          <ElButton circle size="small" aria-label="行密度">
            <IconifyIcon icon="lucide:rows-3" class="text-sm" />
          </ElButton>
          <template #dropdown>
            <ElDropdownMenu>
              <ElDropdownItem
                command="compact"
                :class="{ 'is-active': density === 'compact' }"
              >
                紧凑
              </ElDropdownItem>
              <ElDropdownItem
                command="default"
                :class="{ 'is-active': density === 'default' }"
              >
                默认
              </ElDropdownItem>
              <ElDropdownItem
                command="loose"
                :class="{ 'is-active': density === 'loose' }"
              >
                宽松
              </ElDropdownItem>
            </ElDropdownMenu>
          </template>
        </ElDropdown>
        <ElPopover
          v-if="showColumnSettings"
          placement="bottom-end"
          trigger="click"
          :width="240"
        >
          <template #reference>
            <ElButton circle size="small" aria-label="列设置">
              <IconifyIcon icon="lucide:settings-2" class="text-sm" />
            </ElButton>
          </template>
          <div class="column-settings">
            <div class="column-settings-title">
              列展示
              <ElButton text size="small" @click="resetColumns"
                >恢复默认</ElButton
              >
            </div>
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
      ref="tableRef"
      v-bind="tableProps"
      :data="data"
      v-loading="loading"
      :stripe="stripe"
      :row-class-name="rowClassName"
      :row-style="{ height: rowHeight + 'px' }"
      :row-key="rowKey"
      :scrollbar-always-on="scrollbarAlwaysOn"
      @row-click="(row: T) => emit('row-click', row)"
      @selection-change="(rows: T[]) => emit('selection-change', rows)"
      @sort-change="(sort) => emit('sort-change', sort)"
      @filter-change="(filters) => emit('filter-change', filters)"
    >
      <ElTableColumn
        v-for="col in visibleColumns"
        :key="col.field"
        :prop="col.prop ?? col.field"
        :type="col.type ?? 'default'"
        :label="col.label"
        :width="col.width"
        :min-width="col.minWidth"
        :align="col.align"
        :fixed="getColumnFixed(col.field) || undefined"
        :sortable="col.sortable ?? false"
        :show-overflow-tooltip="col.showOverflowTooltip"
        :selectable="col.selectable"
        :reserve-selection="col.reserveSelection"
      >
        <template
          v-if="col.type !== 'selection' && col.type !== 'index'"
          #default="scope"
        >
          <slot
            :name="`column-${col.field}`"
            v-bind="scope"
            :definition="col"
            :field="col.field"
            :value="getColumnValue(scope.row, col)"
            :index="scope.$index"
          >
            {{ formatColumnValue(scope.row, col, scope.$index) }}
          </slot>
        </template>
        <template v-if="slots[`header-${col.field}`]" #header="scope">
          <slot
            :name="`header-${col.field}`"
            v-bind="scope"
            :definition="col"
          />
        </template>
      </ElTableColumn>
      <template v-if="slots.empty" #empty><slot name="empty" /></template>
      <template v-if="slots.append" #append><slot name="append" /></template>
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
  --table-page-bottom-bar-height: 0px;
  --table-page-toolbar-height: 0px;
  overflow: visible;
}

.table-page-card :deep(.el-card__body) {
  overflow: visible;
  padding: 0;
}

.table-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
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
  min-width: 0;
  flex-wrap: wrap;
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
  margin-left: auto;
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
  display: flex;
  align-items: center;
  justify-content: space-between;
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

.table-page-card :deep(.el-table__body-wrapper) {
  overflow: visible;
}

.table-page-card :deep(.el-table__body-wrapper > .el-scrollbar) {
  height: auto;
  overflow: visible;
}

.table-page-card
  :deep(.el-table__body-wrapper > .el-scrollbar > .el-scrollbar__wrap) {
  height: auto;
  overflow-x: auto;
  overflow-y: hidden;
}

.table-page-card
  :deep(
    .el-table__body-wrapper > .el-scrollbar > .el-scrollbar__bar.is-horizontal
  ) {
  /* Keep the built-in bar outside the horizontal scroll container for page-level sticky. */
  position: sticky;
  bottom: var(--table-page-bottom-bar-height);
  left: auto;
  right: auto;
  height: 8px;
  margin: 0 2px;
  z-index: 6;
  background: var(--el-fill-color-lighter);
}

.table-page-card
  :deep(
    .el-table:not(.el-table--scrollable-x)
      > .el-table__inner-wrapper
      > .el-table__body-wrapper
      > .el-scrollbar
      > .el-scrollbar__bar.is-horizontal
  ) {
  display: none;
}

.table-page-card
  :deep(
    .el-table__body-wrapper > .el-scrollbar > .el-scrollbar__bar.is-vertical
  ) {
  display: none;
}

.table-page-card :deep(.el-table__header-wrapper) {
  position: sticky;
  top: var(--table-page-toolbar-height);
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

.pagination-bar :deep(.el-pagination) {
  flex-wrap: wrap;
  justify-content: flex-end;
  row-gap: 8px;
}
</style>
