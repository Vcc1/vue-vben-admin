export type TablePageDensity = 'compact' | 'default' | 'loose';

export interface TablePageColumnDef {
  field: string;
  label: string;
  defaultVisible?: boolean;
  width?: number | string;
  minWidth?: number | string;
  align?: 'center' | 'left' | 'right';
  fixed?: 'left' | 'right' | false;
}

export interface TablePageProps {
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
}

export interface TablePageEmits {
  (e: 'refresh'): void;
  (e: 'row-click', row: Record<string, any>): void;
  (e: 'update:page', page: number): void;
  (e: 'update:pageSize', pageSize: number): void;
  (e: 'page-change', page: number): void;
  (e: 'size-change', pageSize: number): void;
}
