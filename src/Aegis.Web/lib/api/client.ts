import { ApiError } from "../types/common";

type RequestOptions = RequestInit & {
  parseJson?: boolean;
};

export async function apiRequest<T>(url: string, options: RequestOptions = {}): Promise<T> {
  const response = await fetch(url, {
    credentials: "include",
    cache: "no-store",
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options.headers ?? {}),
    },
  });

  if (!response.ok) {
    let errorBody: ApiError | null = null;
    try {
      errorBody = (await response.json()) as ApiError;
    } catch {
      // ignored
    }

    throw errorBody ?? {
      code: `http_${response.status}`,
      message: response.statusText,
    } satisfies ApiError;
  }

  if (response.status === 204 || options.parseJson === false) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
