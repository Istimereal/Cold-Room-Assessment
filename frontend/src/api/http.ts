export class ApiError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export function apiUrl(path: string): string {
  const configured = import.meta.env.VITE_API_BASE_URL;
  const base = typeof configured === "string" ? configured.replace(/\/$/, "") : "";
  return `${base}${path}`;
}

export function visibleError(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.message.trim().length > 0) {
    return error.message;
  }

  return fallback;
}

export async function request<T>(path: string, init: RequestInit, fallback: string): Promise<T> {
  let response: Response;
  try {
    response = await fetch(apiUrl(path), {
      ...init,
      headers: {
        Accept: "application/json",
        ...init.headers,
      },
    });
  } catch (error) {
    if (isAbort(error)) {
      throw error;
    }

    throw new ApiError(fallback);
  }

  if (!response.ok) {
    throw new ApiError(await readControlledError(response, fallback));
  }

  try {
    return (await response.json()) as T;
  } catch (error) {
    if (isAbort(error)) {
      throw error;
    }

    throw new ApiError(fallback);
  }
}

async function readControlledError(response: Response, fallback: string): Promise<string> {
  try {
    const body: unknown = await response.json();
    if (typeof body === "object" && body !== null && "error" in body) {
      const error = (body as { error: unknown }).error;
      if (typeof error === "string" && error.trim().length > 0) {
        return error;
      }
    }
  } catch {
    return fallback;
  }

  return fallback;
}

function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}
