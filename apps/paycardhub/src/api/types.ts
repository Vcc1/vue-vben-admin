export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageCount: number;
  pageSize: number;
  totalCount: number;
}
