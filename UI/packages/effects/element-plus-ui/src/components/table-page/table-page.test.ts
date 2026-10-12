import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  Comment,
  Fragment,
  createApp,
  createTextVNode,
  h,
  nextTick,
  reactive,
  ref,
  type App,
  type Component,
} from 'vue';
import { vLoading } from 'element-plus';

import TablePage from './table-page.vue';
import type {
  TablePageInstance,
  TablePageProps,
  TablePageSlots,
} from './types';

vi.mock('@vben/icons', () => ({ IconifyIcon: { render: () => null } }));

interface Row {
  id: number;
  name: string;
}
let app: App | undefined;
let root: HTMLElement;
let toolbarHeight: number;
const observers: TestResizeObserver[] = [];

class TestResizeObserver {
  readonly targets = new Set<Element>();
  constructor(private readonly callback: ResizeObserverCallback) {
    observers.push(this);
  }
  observe(target: Element) {
    this.targets.add(target);
  }
  unobserve(target: Element) {
    this.targets.delete(target);
  }
  disconnect() {
    this.targets.clear();
  }
  notify(target: Element) {
    this.callback(
      [{ target } as ResizeObserverEntry],
      this as unknown as ResizeObserver,
    );
  }
}

beforeEach(() => {
  toolbarHeight = 42;
  vi.stubGlobal('ResizeObserver', TestResizeObserver);
  vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockImplementation(
    function (this: HTMLElement) {
      if (this.classList.contains('table-toolbar')) return toolbarHeight;
      if (this.classList.contains('sticky-bottom-bar')) return 57;
      return 0;
    },
  );
  root = document.createElement('div');
  document.body.append(root);
});

afterEach(() => {
  app?.unmount();
  root.remove();
  observers.splice(0);
  localStorage.clear();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

async function settle() {
  await nextTick();
  await nextTick();
  await new Promise((resolve) => setTimeout(resolve, 0));
}

async function mountPage(
  overrides: Partial<TablePageProps<Row>> = {},
  slots: TablePageSlots<Row> = {},
  listeners: Record<string, (...args: any[]) => void> = {},
) {
  const state = reactive<TablePageProps<Row>>({
    tableKey: 'component-test',
    columns: [{ field: 'name', label: 'Name' }],
    data: [{ id: 1, name: 'Alice' }],
    ...overrides,
  });
  const instance = ref<TablePageInstance<Row>>();
  app = createApp({
    render: () =>
      h(
        TablePage as Component,
        { ...state, ...listeners, ref: instance },
        slots,
      ),
  });
  app.directive('loading', vLoading);
  app.mount(root);
  await settle();
  return { state, instance };
}

describe('TablePage', () => {
  it('forwards row keys and supported table options while preserving natural height', async () => {
    const rowKey = vi.fn((row: Row) => String(row.id));
    const warn = vi.spyOn(console, 'warn');
    await mountPage({
      rowKey,
      scrollbarAlwaysOn: false,
      tableProps: {
        border: true,
        height: 80,
        nativeScrollbar: true,
      } as TablePageProps<Row>['tableProps'],
    });
    expect(root.querySelector('.el-table--border')).not.toBeNull();
    expect(
      root
        .querySelector('.table-page-card')!
        .hasAttribute('scrollbar-always-on'),
    ).toBe(false);
    expect(root.querySelector<HTMLElement>('.el-table')!.style.height).toBe('');
    expect(
      root.querySelector('.el-table__body-wrapper .el-scrollbar__bar'),
    ).not.toBeNull();
    expect(rowKey).toHaveBeenCalledWith(
      expect.objectContaining({ id: 1, name: 'Alice' }),
    );
    expect(warn).not.toHaveBeenCalled();
  });

  it('hides empty slots and reconnects toolbar metrics after conditional slot updates', async () => {
    const visible = ref(false);
    const warn = vi.spyOn(console, 'warn');
    await mountPage(
      { showRefresh: false, showDensity: false, showColumnSettings: false },
      {
        'toolbar-left': () => [
          h(Fragment, [
            h(Comment),
            createTextVNode(' '),
            visible.value ? h('span', 'Actions') : h(Comment),
          ]),
        ],
        'toolbar-extra': () => [],
      },
    );
    const card = root.querySelector<HTMLElement>('.table-page-card')!;
    expect(root.querySelector('.table-toolbar')).toBeNull();
    expect(card.style.getPropertyValue('--table-page-toolbar-height')).toBe(
      '0px',
    );
    visible.value = true;
    await settle();
    const toolbar = root.querySelector('.table-toolbar')!;
    const observer = observers.find((candidate) =>
      candidate.targets.has(toolbar),
    )!;
    expect(observer).toBeDefined();
    expect(card.style.getPropertyValue('--table-page-toolbar-height')).toBe(
      '42px',
    );
    toolbarHeight = 78;
    observer.notify(toolbar);
    expect(card.style.getPropertyValue('--table-page-toolbar-height')).toBe(
      '78px',
    );
    visible.value = false;
    await settle();
    expect(root.querySelector('.table-toolbar')).toBeNull();
    expect(observer.targets.has(toolbar)).toBe(false);
    expect(card.style.getPropertyValue('--table-page-toolbar-height')).toBe(
      '0px',
    );
    visible.value = true;
    await settle();
    const replacement = root.querySelector('.table-toolbar')!;
    expect(replacement).not.toBe(toolbar);
    expect(observer.targets.has(replacement)).toBe(true);
    expect(warn).not.toHaveBeenCalled();
  });

  it('reconnects pagination metrics when the footer is added or removed', async () => {
    const { state } = await mountPage({ pagination: false });
    const card = root.querySelector<HTMLElement>('.table-page-card')!;
    expect(card.style.getPropertyValue('--table-page-bottom-bar-height')).toBe(
      '0px',
    );
    state.pagination = true;
    await settle();
    const footer = root.querySelector('.sticky-bottom-bar')!;
    expect(observers.some((observer) => observer.targets.has(footer))).toBe(
      true,
    );
    expect(card.style.getPropertyValue('--table-page-bottom-bar-height')).toBe(
      '57px',
    );
    state.pagination = false;
    await settle();
    expect(observers.some((observer) => observer.targets.has(footer))).toBe(
      false,
    );
    expect(card.style.getPropertyValue('--table-page-bottom-bar-height')).toBe(
      '0px',
    );
  });

  it('keeps legacy pagination listeners working without a unified event listener', async () => {
    const legacy = vi.fn();
    await mountPage({ total: 60 }, {}, { onPageChange: legacy });
    root.querySelector<HTMLButtonElement>('.btn-next')!.click();
    await settle();
    expect(legacy).toHaveBeenCalledTimes(1);
    expect(legacy).toHaveBeenCalledWith(2);
  });

  it('supports optional selection methods through the public component ref', async () => {
    const { state, instance } = await mountPage({
      columns: [
        { field: 'selection', type: 'selection', label: 'Select' },
        { field: 'name', label: 'Name' },
      ],
      rowKey: 'id',
    });
    instance.value!.toggleRowSelection(state.data[0]!, true);
    await settle();
    expect(instance.value!.getSelectionRows()).toEqual([state.data[0]]);
    instance.value!.clearSelection();
    await settle();
    expect(instance.value!.getSelectionRows()).toEqual([]);
  });
});
