import { computed, ref, type Ref } from 'vue';

import type { TablePageColumnDef } from './types';

interface InternalColumnConfig {
  field: string;
  visible: boolean;
  fixed: 'left' | 'right' | false;
}

interface ColumnConfigDelta {
  visibility?: Record<string, boolean>;
  fixed?: Record<string, 'left' | 'right'>;
  order?: string[];
}

export function useColumnConfig(
  tableKey: string,
  columns: Ref<TablePageColumnDef[]>,
) {
  const storageKey = `${tableKey}-column-config`;
  const columnConfigs = ref<InternalColumnConfig[]>([]);
  const draggingField = ref<string | null>(null);

  const orderedVisibleColumns = computed(() =>
    columnConfigs.value.filter((c) => c.visible).map((c) => c.field),
  );

  function buildDefault(): InternalColumnConfig[] {
    return columns.value.map((col) => ({
      field: col.field,
      visible: col.defaultVisible ?? true,
      fixed: col.fixed || false,
    }));
  }

  function load() {
    try {
      const saved = localStorage.getItem(storageKey);
      if (!saved) {
        columnConfigs.value = buildDefault();
        return;
      }

      const delta = JSON.parse(saved) as ColumnConfigDelta;
      const visibilityMap = delta.visibility || {};
      const fixedMap = delta.fixed || {};

      const orderedFields =
        delta.order && delta.order.length === columns.value.length
          ? delta.order
          : columns.value.map((c) => c.field);

      columnConfigs.value = orderedFields.map((field) => {
        const defaultCol = columns.value.find((c) => c.field === field);
        const visible =
          field in visibilityMap
            ? visibilityMap[field]!
            : (defaultCol?.defaultVisible ?? true);
        const fixed = fixedMap[field] || defaultCol?.fixed || false;
        return { field, visible, fixed };
      });
    } catch {
      columnConfigs.value = buildDefault();
    }
  }

  function save() {
    const delta: ColumnConfigDelta = {};
    const defaultMap = new Map(columns.value.map((c) => [c.field, c]));
    const defaultOrder = columns.value.map((c) => c.field);
    const currentOrder = columnConfigs.value.map((c) => c.field);

    const visibilityDelta: Record<string, boolean> = {};
    for (const col of columnConfigs.value) {
      const defaultVisible = defaultMap.get(col.field)?.defaultVisible ?? true;
      if (col.visible !== defaultVisible) {
        visibilityDelta[col.field] = col.visible;
      }
    }
    if (Object.keys(visibilityDelta).length > 0) {
      delta.visibility = visibilityDelta;
    }

    const fixedDelta: Record<string, 'left' | 'right'> = {};
    for (const col of columnConfigs.value) {
      if (col.fixed && col.fixed !== false) {
        fixedDelta[col.field] = col.fixed;
      }
    }
    if (Object.keys(fixedDelta).length > 0) {
      delta.fixed = fixedDelta;
    }

    if (JSON.stringify(currentOrder) !== JSON.stringify(defaultOrder)) {
      delta.order = currentOrder;
    }

    localStorage.setItem(storageKey, JSON.stringify(delta));
  }

  function toggleColumnVisibility(field: string) {
    const col = columnConfigs.value.find((c) => c.field === field);
    if (col) {
      col.visible = !col.visible;
      save();
    }
  }

  function getColumnFixed(field: string): 'left' | 'right' | boolean {
    return columnConfigs.value.find((c) => c.field === field)?.fixed || false;
  }

  function getColumnsByFixed(fixed: 'left' | 'right' | false) {
    return columnConfigs.value.filter((c) => c.fixed === fixed);
  }

  function handleDragStart(field: string) {
    draggingField.value = field;
  }

  function handleDragOver(
    e: DragEvent,
    targetFixed: 'left' | 'right' | false,
  ) {
    e.preventDefault();
    if (!draggingField.value) return;

    const draggedCol = columnConfigs.value.find(
      (c) => c.field === draggingField.value,
    );
    if (!draggedCol) return;

    if (draggedCol.fixed !== targetFixed) {
      draggedCol.fixed = targetFixed;
      return;
    }

    const target = (e.target as HTMLElement).closest(
      '[data-field]',
    ) as HTMLElement | null;
    if (!target) return;
    const targetField = target.dataset.field!;
    if (targetField === draggingField.value) return;

    const targetIdx = columnConfigs.value.findIndex(
      (c) => c.field === targetField,
    );
    const draggedIdx = columnConfigs.value.findIndex(
      (c) => c.field === draggingField.value,
    );
    if (targetIdx < 0 || draggedIdx < 0) return;

    const [removed] = columnConfigs.value.splice(draggedIdx, 1);
    columnConfigs.value.splice(targetIdx, 0, removed);
  }

  function handleDrop() {
    if (draggingField.value) {
      save();
    }
    draggingField.value = null;
  }

  function handleDragEnd() {
    draggingField.value = null;
  }

  return {
    columnConfigs,
    orderedVisibleColumns,
    draggingField,
    load,
    save,
    toggleColumnVisibility,
    getColumnFixed,
    getColumnsByFixed,
    handleDragStart,
    handleDragOver,
    handleDrop,
    handleDragEnd,
  };
}
