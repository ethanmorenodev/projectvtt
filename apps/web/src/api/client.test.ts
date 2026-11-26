import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiFetch, ApiError, setApiToken } from './client';

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });

afterEach(() => {
  setApiToken(null);
  vi.unstubAllGlobals();
});

describe('apiFetch', () => {
  it('envía el token Bearer y el cuerpo JSON', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ ok: true }));
    vi.stubGlobal('fetch', fetchMock);
    setApiToken('tok-123');

    const result = await apiFetch<{ ok: boolean }>('/api/v1/campaigns', {
      method: 'POST',
      body: { name: 'Campaña' },
    });

    expect(result).toEqual({ ok: true });
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('/api/v1/campaigns');
    expect(init.method).toBe('POST');
    expect(init.headers).toMatchObject({
      Authorization: 'Bearer tok-123',
      'Content-Type': 'application/json',
    });
    expect(init.body).toBe(JSON.stringify({ name: 'Campaña' }));
  });

  it('no envía Authorization sin token', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse([]));
    vi.stubGlobal('fetch', fetchMock);

    await apiFetch('/api/v1/campaigns');
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it('convierte ProblemDetails del backend en ApiError con campo', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(
        {
          title: 'Solicitud no válida.',
          detail: "'gridCellSize' debe estar entre 8 y 512.",
          extensions: { field: 'gridCellSize' },
        },
        400,
      ),
    );
    vi.stubGlobal('fetch', fetchMock);

    const error = await apiFetch('/api/v1/scenes/1', { method: 'PATCH', body: {} }).catch(
      (cause: unknown) => cause,
    );

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(400);
    expect(apiError.title).toBe('Solicitud no válida.');
    expect(apiError.field).toBe('gridCellSize');
    expect(apiError.message).toContain('gridCellSize');
  });

  it('tolera respuestas de error sin cuerpo JSON', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('', { status: 401 }));
    vi.stubGlobal('fetch', fetchMock);

    const error = await apiFetch('/api/v1/auth/me').catch((cause: unknown) => cause);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
  });
});
