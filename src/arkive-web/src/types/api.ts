/** Standard API success response envelope */
export interface ApiResponse<T> {
  data: T;
  meta?: ApiMeta;
}

/** Pagination metadata */
export interface ApiMeta {
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Standard API error response envelope */
export interface ApiErrorResponse {
  error: ApiError;
}

/** API error details */
export interface ApiError {
  code: string;
  message: string;
  details?: unknown;
  traceId: string;
}
