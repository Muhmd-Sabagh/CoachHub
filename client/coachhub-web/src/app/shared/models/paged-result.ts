export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
export const emptyPage = <T>(): PagedResult<T> => ({
  items: [],
  pageNumber: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
});
