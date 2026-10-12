import { computed, ref, toValue, type MaybeRefOrGetter } from 'vue';

import type { TablePageDensity } from './types';

const DENSITY_OPTIONS: TablePageDensity[] = ['compact', 'default', 'loose'];
const ROW_HEIGHTS: Record<TablePageDensity, number> = {
  compact: 36,
  default: 46,
  loose: 56,
};

export function useDensity(tableKey: MaybeRefOrGetter<string>) {
  const storageKey = () => `${toValue(tableKey)}-density`;
  const density = ref<TablePageDensity>('default');

  const rowHeight = computed(() => ROW_HEIGHTS[density.value]);

  function load() {
    density.value = 'default';
    try {
      const saved = localStorage.getItem(
        storageKey(),
      ) as TablePageDensity | null;
      if (saved && DENSITY_OPTIONS.includes(saved)) density.value = saved;
    } catch {
      density.value = 'default';
    }
  }

  function save() {
    try {
      localStorage.setItem(storageKey(), density.value);
    } catch (error) {
      console.warn('TablePage density preference could not be saved.', error);
    }
  }

  function handleChange(val: TablePageDensity) {
    density.value = val;
    save();
  }

  return { density, rowHeight, load, handleChange };
}
