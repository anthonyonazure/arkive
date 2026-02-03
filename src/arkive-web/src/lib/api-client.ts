import type { ApiResponse, ApiErrorResponse } from "@/types/api";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:7296/api";

type TokenProvider = () => Promise<string | null>;

let _tokenProvider: TokenProvider | null = null;

export function setTokenProvider(provider: TokenProvider) {
  _tokenProvider = provider;
}

class ApiClientError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
    public readonly traceId: string,
    public readonly details?: unknown
  ) {
    super(message);
    this.name = "ApiClientError";
  }
}

async function request<T>(
  path: string,
  options?: RequestInit
): Promise<ApiResponse<T>> {
  const url = `${API_BASE_URL}${path}`;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options?.headers as Record<string, string>),
  };

  if (_tokenProvider) {
    const token = await _tokenProvider();
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorBody: ApiErrorResponse;
    try {
      errorBody = (await response.json()) as ApiErrorResponse;
    } catch {
      throw new ApiClientError(
        "NETWORK_ERROR",
        `Request failed with status ${response.status}`,
        response.status,
        ""
      );
    }
    throw new ApiClientError(
      errorBody.error.code,
      errorBody.error.message,
      response.status,
      errorBody.error.traceId,
      errorBody.error.details
    );
  }

  return (await response.json()) as ApiResponse<T>;
}

async function requestRaw(
  path: string,
  options?: RequestInit
): Promise<string> {
  const url = `${API_BASE_URL}${path}`;

  const headers: Record<string, string> = {
    ...(options?.headers as Record<string, string>),
  };

  if (_tokenProvider) {
    const token = await _tokenProvider();
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  const response = await fetch(url, { ...options, headers });

  if (!response.ok) {
    throw new ApiClientError(
      "EXPORT_ERROR",
      `Request failed with status ${response.status}`,
      response.status,
      ""
    );
  }

  return response.text();
}

export const apiClient = {
  get: <T>(path: string, options?: RequestInit) =>
    request<T>(path, { ...options, method: "GET" }),

  getRaw: (path: string, options?: RequestInit) =>
    requestRaw(path, { ...options, method: "GET" }),

  post: <T>(path: string, body: unknown, options?: RequestInit) =>
    request<T>(path, {
      ...options,
      method: "POST",
      body: JSON.stringify(body),
    }),

  put: <T>(path: string, body: unknown, options?: RequestInit) =>
    request<T>(path, {
      ...options,
      method: "PUT",
      body: JSON.stringify(body),
    }),

  patch: <T>(path: string, body: unknown, options?: RequestInit) =>
    request<T>(path, {
      ...options,
      method: "PATCH",
      body: JSON.stringify(body),
    }),

  delete: <T>(path: string, options?: RequestInit) =>
    request<T>(path, { ...options, method: "DELETE" }),
};

export { ApiClientError };
