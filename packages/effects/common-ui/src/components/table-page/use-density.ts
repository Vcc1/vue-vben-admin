import { computed, ref } from 'vue';

import type { TablePageDensity } from './types';

const DENSITY_OPTIONS: TablePageDensity[] = ['compact', 'default', 'loose'];
const ROW_HEIGHTS: Record<TablePageDensity, number> = {
  compact: 36,
  default: 46,
  loose: 56,
};

export function useDensity(tableKey: string) {
  const storageKey = `${tableKey}-density`;
  const density = ref<TablePageDensity>('default');

  const rowHeight = computed(() => ROW_HEIGHTS[density.value]);

  function load() {
    const saved = localStorage.getItem(storageKey) as TablePageDensity | null;
    if (saved && DENSITY_OPTIONS.includes(saved)) {
      density.value = saved;
    }
  }

  function save() {
    localStorage.setItem(storageKey, density.value);
  }

  function handleChange(val: TablePageDensity) {
    density.value = val;
    save();
  }

  return { density, rowHeight, load, handleChange };
}
