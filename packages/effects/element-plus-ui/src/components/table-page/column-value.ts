import type { TablePageColumnDef } from './types';

export function getColumnValue<T extends object>(
  row: T,
  column: TablePageColumnDef<T>,
): unknown {
  const path = column.prop ?? column.field;
  if (Object.prototype.hasOwnProperty.call(row, path))
    return (row as Record<string, unknown>)[path];
  return path
    .split('.')
    .reduce<unknown>(
      (value, key) =>
        value !== null &&
        typeof value === 'object' &&
        Object.prototype.hasOwnProperty.call(value, key)
          ? (value as Record<string, unknown>)[key]
          : undefined,
      row,
    );
}

export function formatColumnValue<T extends object>(
  row: T,
  column: TablePageColumnDef<T>,
  index: number,
) {
  const value = getColumnValue(row, column);
  return column.formatter ? column.formatter(value, row, column, index) : value;
}
