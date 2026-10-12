import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApp, nextTick, ref, type App } from 'vue';

import { useColumnConfig } from './use-column-config';
import {
  useTablePage,
  type TablePageRequestContext,
  type TablePageResult,
  type UseTablePageOptions,
} from './use-table-page';

const apps: App[] = [];
function mountTable<T extends object>(options: UseTablePageOptions<T>) {
  let table!: ReturnType<typeof useTablePage<T>>;
  const app = createApp({
    setup() {
      table = useTablePage(options);
      return () => null;
    },
  });
  app.mount(document.createElement('div'));
  apps.push(app);
  return table;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

afterEach(() => {
  for (const app of apps.splice(0)) app.unmount();
  localStorage.clear();
});

describe('useTablePage', () => {
  it('coalesces pagination changes into one request with the final page size', async () => {
    const request = vi.fn(async (_context: TablePageRequestContext) => ({
      items: [{ id: 1 }],
      total: 10,
    }));
    const table = mountTable({ request, immediate: false });
    table.bindings.onPaginationChange({ page: 3, pageSize: 20 });
    table.bindings.onPaginationChange({ page: 1, pageSize: 50 });
    await table.reload();
    expect(request).toHaveBeenCalledTimes(1);
    expect(request.mock.calls[0]?.[0]).toMatchObject({ page: 1, pageSize: 50 });
    expect(table.bindings.data).toEqual([{ id: 1 }]);
  });

  it('aborts the old request and prevents its late response from replacing current data', async () => {
    const first = deferred<TablePageResult<{ id: number }>>();
    const second = deferred<TablePageResult<{ id: number }>>();
    const request = vi
      .fn()
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);
    const table = mountTable<{ id: number }>({ request, immediate: false });
    const older = table.reload();
    await Promise.resolve();
    const newer = table.search();
    await Promise.resolve();
    expect(request.mock.calls[0]?.[0].signal.aborted).toBe(true);
    second.resolve({ items: [{ id: 2 }], total: 2 });
    await newer;
    first.resolve({ items: [{ id: 1 }], total: 1 });
    await older;
    expect(table.data.value).toEqual([{ id: 2 }]);
    expect(table.total.value).toBe(2);
    expect(table.loading.value).toBe(false);
  });

  it('refreshes the current page and resets filters before searching page one', async () => {
    const request = vi.fn(async (_context: TablePageRequestContext) => ({
      items: [],
      total: 0,
    }));
    const onReset = vi.fn();
    const table = mountTable({ request, onReset, immediate: false });
    table.page.value = 4;
    table.pageSize.value = 50;
    await table.reload();
    expect(request.mock.calls[0]?.[0]).toMatchObject({ page: 4, pageSize: 50 });
    await table.reset();
    expect(onReset).toHaveBeenCalledTimes(1);
    expect(request.mock.calls[1]?.[0]).toMatchObject({ page: 1, pageSize: 50 });
  });

  it('retains existing rows and releases loading when a request fails', async () => {
    const cause = new Error('network failure');
    const request = vi
      .fn()
      .mockResolvedValueOnce({ items: [{ id: 1 }], total: 1 })
      .mockRejectedValueOnce(cause);
    const table = mountTable<{ id: number }>({ request, immediate: false });
    await table.reload();
    await expect(table.reload()).rejects.toBe(cause);
    expect(table.data.value).toEqual([{ id: 1 }]);
    expect(table.error.value).toBe(cause);
    expect(table.loading.value).toBe(false);
  });
});

describe('column preferences', () => {
  it('synchronizes in-place column additions and removals while retaining saved visibility', async () => {
    const columns = ref([
      { field: 'a', label: 'A' },
      { field: 'b', label: 'B' },
    ]);
    const config = useColumnConfig('dynamic', columns);
    expect(config.orderedVisibleColumns.value).toEqual(['a', 'b']);
    config.toggleColumnVisibility('a');
    columns.value.push({ field: 'new', label: 'New' });
    await nextTick();
    expect(config.orderedVisibleColumns.value).toEqual(['b', 'new']);
    columns.value.splice(1, 1);
    await nextTick();
    expect(config.orderedVisibleColumns.value).toEqual(['new']);
    expect(config.columnConfigs.value.map((column) => column.field)).toEqual([
      'a',
      'new',
    ]);
  });

  it('loads the new table preferences when its key changes', async () => {
    localStorage.setItem(
      'second-column-config',
      JSON.stringify({ visibility: { a: false } }),
    );
    const tableKey = ref('first');
    const config = useColumnConfig(tableKey, ref([{ field: 'a', label: 'A' }]));
    expect(config.orderedVisibleColumns.value).toEqual(['a']);
    tableKey.value = 'second';
    await nextTick();
    expect(config.orderedVisibleColumns.value).toEqual([]);
  });

  it('merges renamed and added fields while retaining the valid saved order', () => {
    localStorage.setItem(
      'columns-column-config',
      JSON.stringify({
        order: ['removed', 'b', 'a'],
        visibility: { a: false },
      }),
    );
    const columns = ref([
      { field: 'a', label: 'A' },
      { field: 'b', label: 'B' },
      { field: 'new', label: 'New' },
    ]);
    const config = useColumnConfig('columns', columns);
    config.load();
    expect(config.columnConfigs.value.map((column) => column.field)).toEqual([
      'b',
      'a',
      'new',
    ]);
    expect(config.orderedVisibleColumns.value).toEqual(['b', 'new']);
    config.reset();
    expect(config.orderedVisibleColumns.value).toEqual(['a', 'b', 'new']);
  });

  it('persists unfixing a column whose default position is fixed', () => {
    const columns = ref([{ field: 'a', label: 'A', fixed: 'left' as const }]);
    const config = useColumnConfig('columns', columns);
    config.load();
    config.handleDragStart('a');
    config.handleDragOver(
      new Event('dragover', { cancelable: true }) as DragEvent,
      false,
    );
    config.handleDrop();
    config.load();
    expect(config.getColumnFixed('a')).toBe(false);
  });
});
