import type { VNode } from 'vue';
import type { TableColumnCtx, TableProps } from 'element-plus';

export type TablePageDensity = 'compact' | 'default' | 'loose';

export interface TablePageColumnDef<T extends object = Record<string, any>> {
  field: Extract<keyof T, string> | (string & {});
  prop?: string;
  label: string;
  defaultVisible?: boolean;
  width?: number | string;
  minWidth?: number | string;
  align?: 'center' | 'left' | 'right';
  fixed?: 'left' | 'right' | false;
  type?: 'default' | 'expand' | 'index' | 'selection';
  sortable?: boolean | 'custom';
  showOverflowTooltip?: boolean;
  formatter?: (
    value: unknown,
    row: T,
    column: TablePageColumnDef<T>,
    index: number,
  ) => string | number | null | undefined;
  selectable?: (row: T, index: number) => boolean;
  reserveSelection?: boolean;
}

export interface TablePagePagination {
  page: number;
  pageSize: number;
}

export type TablePageTableOptions<T extends object> = Partial<
  Pick<
    TableProps<T>,
    | 'border'
    | 'defaultSort'
    | 'emptyText'
    | 'highlightCurrentRow'
    | 'spanMethod'
    | 'tooltipOptions'
  >
>;

export interface TablePageProps<T extends object = Record<string, any>> {
  tableKey: string;
  title?: string;
  columns: TablePageColumnDef<T>[];
  data: T[];
  loading?: boolean;
  total?: number;
  page?: number;
  pageSize?: number;
  pageSizes?: number[];
  pagination?: boolean;
  stripe?: boolean;
  scrollbarAlwaysOn?: boolean;
  rowClassName?: string | ((args: { row: T; rowIndex: number }) => string);
  paginationLayout?: string;
  rowKey?: string | ((row: T) => string);
  tableProps?: TablePageTableOptions<T>;
  showRefresh?: boolean;
  showDensity?: boolean;
  showColumnSettings?: boolean;
}

export interface TablePageInstance<T extends object = Record<string, any>> {
  resetColumns(): void;
  clearSelection(): void;
  getSelectionRows(): T[];
  toggleRowSelection(row: T, selected?: boolean): void;
}

export interface TablePageEmits<T extends object = Record<string, any>> {
  (e: 'refresh'): void;
  (e: 'row-click', row: T): void;
  (e: 'update:page', page: number): void;
  (e: 'update:pageSize', pageSize: number): void;
  (e: 'page-change', page: number): void;
  (e: 'size-change', pageSize: number): void;
  (e: 'pagination-change', pagination: TablePagePagination): void;
  (e: 'selection-change', selection: T[]): void;
  (
    e: 'sort-change',
    sort: {
      column: TableColumnCtx<T>;
      prop: string | null;
      order: 'ascending' | 'descending' | null;
    },
  ): void;
  (e: 'filter-change', filters: Record<string, string[]>): void;
}

export interface TablePageCellScope<T extends object> {
  row: T;
  column: TableColumnCtx<T>;
  definition: TablePageColumnDef<T>;
  field: string;
  value: unknown;
  $index: number;
  index: number;
}

export type TablePageSlots<T extends object> = {
  'toolbar-left'?: () => VNode[];
  'toolbar-extra'?: () => VNode[];
  empty?: () => VNode[];
  append?: () => VNode[];
} & {
  [name in `column-${string}`]?: (scope: TablePageCellScope<T>) => VNode[];
} & {
  [name in `header-${string}`]?: (scope: {
    column: TableColumnCtx<T>;
    definition: TablePageColumnDef<T>;
    $index: number;
  }) => VNode[];
};
