# TablePage and TableSearch

Import these components and `useTablePage` from `@vben/element-plus-ui`.
The table uses natural height and the page owns vertical scrolling. Its built-in
horizontal scrollbar sticks above pagination; there is no extra scroll synchronization.

## Server pagination

```vue
<script setup lang="ts">
import { ref } from 'vue';
import { ElInput } from 'element-plus';
import { TablePage, TableSearch, TableSearchItem, useTablePage, type TablePageColumnDef } from '@vben/element-plus-ui';
import { getCardsApi, type CardListItem } from '#/api/cards';

const keyword = ref('');
const columns: TablePageColumnDef<CardListItem>[] = [
  { field: 'orderNo', label: 'Order', minWidth: 200, showOverflowTooltip: true },
  { field: 'transactionCount', label: 'Transactions', width: 120, sortable: 'custom' },
  { field: 'limitAmountValue', label: 'Amount', align: 'right', formatter: (value) => Number(value ?? 0).toFixed(2) },
  { field: 'actions', label: 'Actions', width: 100, fixed: 'right' },
];
const table = useTablePage<CardListItem>({
  request: async ({ page, pageSize }) => {
    const result = await getCardsApi({ page, pageSize, keyword: keyword.value || undefined });
    return { items: result.items, total: result.totalCount };
  },
  onReset: () => { keyword.value = ''; },
});
const { loading } = table;
function search() { void table.search().catch(() => {}); }
function reset() { void table.reset().catch(() => {}); }
</script>

<template>
  <TableSearch :loading="loading" @search="search" @reset="reset">
    <TableSearchItem v-slot="{ id }" label="Keyword">
      <ElInput :id="id" v-model="keyword" clearable />
    </TableSearchItem>
  </TableSearch>
  <TablePage v-bind="table.bindings" table-key="paycardhub-cards" :columns="columns" row-key="id">
    <template #column-actions="{ row }">
      <a :href="`/cards/${row.id}`" @click.stop>Details</a>
    </template>
  </TablePage>
</template>
```

`table.bindings` includes data, loading, total, pagination updates and refresh handlers.
Use either these bindings or manual event handlers for requesting data, so that a page
does not register both sets of request handlers.

- `reload()` preserves the page; `search()` starts at page one.
- `reset()` calls `onReset`, then searches page one with the current page size.
- `immediate: false` disables the initial request on mount.
- Request receives an `AbortSignal`; pass it to your HTTP client when supported.
  Late responses cannot replace newer results even when the client ignores cancellation.
- These methods return promises. Automatic pagination/refresh catches failures and
  stores them in `table.error`; `onError` can report them. Explicit method callers
  should handle rejections. Existing rows are preserved after failure.
- `sortable: 'custom'` emits `sort-change`. The page stores the new sort in its
  request parameters and calls `table.search()`; the hook does not invent API sort fields.

## Manual data and local pagination

The original `data`, `loading`, `total`, `v-model:page` and `v-model:pageSize` API
continues to work. The page slices local data or supplies server results.
`pagination-change` returns `{ page, pageSize }`; the legacy `page-change` and
`size-change` events are retained. Changing page size resets the page to one.
The new events are optional. Use either `pagination-change` or the legacy page/size
events for the same request, rather than registering both sets of handlers.

## Columns and slots

- `field` is a stable column identifier and may represent a virtual column.
  `prop` optionally selects a different data field, including a dotted property path.
- `formatter(value, row, definition, index)` handles simple display text.
  `column-${field}` slots override it and receive `row`, `column` (Element Plus),
  `definition`, `field`, `value`, `index` and the existing `$index`.
- `header-${field}`, `toolbar-left`, `toolbar-extra`, `empty` and `append` are slots.
- `type: 'selection'` enables checkbox selection; `selection-change` reports rows.
  Use `rowKey` and `reserveSelection` for selection across pages.
- `tableProps` forwards typed options: `border`, `defaultSort`, `emptyText`,
  `highlightCurrentRow`, `spanMethod`, and `tooltipOptions`. Height and scrollbar
  configuration remain owned by TablePage; unsupported runtime options are ignored.
- `scrollbarAlwaysOn` defaults to true. Set `:scrollbar-always-on="false"` for the
  Element Plus scrollbar's normal hover visibility. Its CSS sticky position stays active.
- `showRefresh`, `showDensity`, and `showColumnSettings` default to true.
- Empty or conditional toolbar slots do not create an empty toolbar when the title
  and all built-in toolbar controls are disabled.
- `selection-change`, `sort-change`, and `filter-change` forward Element Plus events;
  pages only need listeners for the behaviors they use.
- Exposed methods: `clearSelection()`, `getSelectionRows()`, `toggleRowSelection()`,
  and `resetColumns()`. They are optional; type a template ref with
  `TablePageInstance<YourRow>` when invoking them.
- `tableKey` must be stable and unique to the business table. Preferences are merged
  with the current fields when columns change; the settings menu can restore defaults.

```vue
<script setup lang="ts">
import { ref } from 'vue';
import { TablePage, type TablePageInstance } from '@vben/element-plus-ui';
import type { CardListItem } from '#/api/cards';
const tableRef = ref<TablePageInstance<CardListItem>>();
function clearSelection() { tableRef.value?.clearSelection(); }
</script>
<!-- Bind ref="tableRef" on the TablePage that already supplies columns/data. -->
```

Sticky offsets depend only on toolbar and pagination heights. ResizeObserver tracks
those elements, including replacements from conditional slots. Table content height
changes are handled by CSS sticky and Element Plus's own table/scrollbar observers.

## Search layout

Place `TableSearchItem` components directly in the default slot. Inputs remain owned
by the page and may be Element Plus controls or custom components. Bind the supplied
`id` to the input for its label; custom controls should accept and forward it.

Wide layouts keep inputs and actions on one row. On expansion, fields use the full
width of earlier rows. Actions occupy only the right side of the last row and move
to their own row when the last field row is full. Fields that do not fit are hidden
initially; an icon after search expands or collapses subsequent rows. Hidden fields
keep their model values and are excluded from keyboard focus. No toggle is shown
when all fields fit. Labels appear above inputs when the form is 960px wide or less;
wider forms keep labels beside inputs. Layout follows the component's available
width, including sidebar changes, using ResizeObserver.

`v-model:expanded` controls expansion. `minFieldWidth` defaults to 280px, item
`labelWidth` to 88px, and `searchText` / `resetText` customize button labels.

Sticky layout requires ancestor containers between the table and the page scroller
to avoid `overflow: hidden/auto`. The actual page scroller may be an inner layout
container such as `#__vben_layout_scroll`.
