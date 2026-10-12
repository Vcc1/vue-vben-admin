import { onMounted, onScopeDispose, ref, shallowRef } from 'vue';

import type { TablePagePagination } from './types';

export interface TablePageRequestContext extends TablePagePagination {
  signal: AbortSignal;
}

export interface TablePageResult<T> {
  items: T[];
  total: number;
}

export interface UseTablePageOptions<T> {
  request: (context: TablePageRequestContext) => Promise<TablePageResult<T>>;
  immediate?: boolean;
  pageSize?: number;
  onReset?: () => void;
  onError?: (error: unknown) => void;
}

export function useTablePage<T extends object>(
  options: UseTablePageOptions<T>,
) {
  const data = shallowRef<T[]>([]);
  const loading = ref(false);
  const total = ref(0);
  const page = ref(1);
  const pageSize = ref(options.pageSize ?? 20);
  const error = shallowRef<unknown>();
  let requestId = 0;
  let controller: AbortController | undefined;
  let queued: Promise<void> | undefined;
  let disposed = false;

  async function execute() {
    if (disposed) return;
    const id = ++requestId;
    controller?.abort();
    controller = new AbortController();
    loading.value = true;
    error.value = undefined;
    try {
      const result = await options.request({
        page: page.value,
        pageSize: pageSize.value,
        signal: controller.signal,
      });
      if (id !== requestId) return;
      data.value = result.items;
      total.value = result.total;
    } catch (cause) {
      if (id !== requestId) return;
      error.value = cause;
      options.onError?.(cause);
      throw cause;
    } finally {
      if (id === requestId) loading.value = false;
    }
  }

  // Coalesce pagination updates emitted together by Element Plus.
  function reload(): Promise<void> {
    if (!queued) {
      queued = Promise.resolve().then(() => {
        queued = undefined;
        return execute();
      });
    }
    return queued;
  }

  function search() {
    page.value = 1;
    return reload();
  }

  function reset() {
    options.onReset?.();
    return search();
  }

  function handleReload() {
    void reload().catch(() => {});
  }

  function handlePaginationChange(value: TablePagePagination) {
    page.value = value.page;
    pageSize.value = value.pageSize;
    handleReload();
  }

  onMounted(() => {
    if (options.immediate !== false) handleReload();
  });
  onScopeDispose(() => {
    disposed = true;
    requestId++;
    controller?.abort();
  });

  return {
    data,
    loading,
    total,
    page,
    pageSize,
    error,
    reload,
    search,
    reset,
    get bindings() {
      return {
        data: data.value,
        loading: loading.value,
        total: total.value,
        page: page.value,
        pageSize: pageSize.value,
        'onUpdate:page': (value: number) => {
          page.value = value;
        },
        'onUpdate:pageSize': (value: number) => {
          pageSize.value = value;
        },
        onPaginationChange: handlePaginationChange,
        onRefresh: handleReload,
      };
    },
  };
}
