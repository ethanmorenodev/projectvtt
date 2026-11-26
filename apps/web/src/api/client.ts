/**
 * Cliente HTTP mínimo: Bearer automático, errores ProblemDetails del
 * backend convertidos en ApiError. Sin dependencias de UI.
 */

export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly field: string | undefined;

  constructor(status: number, title: string, detail: string, field?: string) {
    super(detail);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.field = field;
  }
}

interface ProblemBody {
  title?: string;
  detail?: string;
  extensions?: Record<string, unknown>;
}

let token: string | null = null;

/** Fija el token Bearer usado por todas las peticiones (lo gestiona AuthContext). */
export function setApiToken(value: string | null): void {
  token = value;
}

export function getApiToken(): string | null {
  return token;
}

export interface ApiRequest {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  body?: unknown;
}

/** Evento global de sesión caducada (PMB-2.9): la UI lo escucha para
 * avisar con un toast y volver al login conservando la ruta. */
export const SESSION_EXPIRED_EVENT = 'pv:session-expired'

function notifyAuthFailure(status: number): void {
  if (status === 401 && token) {
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
  }
}

export async function apiFetch<T>(path: string, request: ApiRequest = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  if (request.body !== undefined && !(request.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(path, {
    method: request.method ?? 'GET',
    headers,
    body: request.body === undefined
    ? undefined
    : request.body instanceof FormData
      ? request.body
      : JSON.stringify(request.body),
  });

  if (!response.ok) {
    notifyAuthFailure(response.status);
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** GET autenticado devolviendo el cuerpo como Blob (imágenes y archivos). */
export async function apiFetchBlob(path: string): Promise<Blob> {
  const headers: Record<string, string> = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(path, { headers });
  if (!response.ok) {
    notifyAuthFailure(response.status);
    throw await toApiError(response);
  }

  return response.blob();
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemBody | null = null;
  try {
    problem = (await response.json()) as ProblemBody;
  } catch {
    // Respuesta sin cuerpo JSON (p. ej. 401 sin ProblemDetails).
  }

  const field =
    problem?.extensions && typeof problem.extensions.field === 'string'
      ? problem.extensions.field
      : undefined;

  return new ApiError(
    response.status,
    problem?.title ?? `Error ${response.status}.`,
    problem?.detail ?? `La petición falló con el código ${response.status}.`,
    field,
  );
}
