import { describe, expect, it } from 'vitest';
import { MAX_MAP_BYTES, validateMapFile } from './validateMapFile';

describe('validateMapFile', () => {
  it('acepta PNG dentro del límite', () => {
    expect(validateMapFile({ name: 'plano.png', type: 'image/png', size: 1024 })).toEqual({
      ok: true,
    });
  });

  it('acepta JPEG, WebP y SVG', () => {
    for (const type of ['image/jpeg', 'image/webp', 'image/svg+xml']) {
      expect(validateMapFile({ name: 'mapa', type, size: 10 }).ok).toBe(true);
    }
  });

  it('rechaza tipos no permitidos', () => {
    const result = validateMapFile({ name: 'mapa.gif', type: 'image/gif', size: 10 });
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.reason).toContain('PNG');
    }
  });

  it('rechaza archivos vacíos y gigantes', () => {
    expect(validateMapFile({ name: 'a.png', type: 'image/png', size: 0 }).ok).toBe(false);
    expect(
      validateMapFile({ name: 'a.png', type: 'image/png', size: MAX_MAP_BYTES + 1 }).ok,
    ).toBe(false);
    expect(
      validateMapFile({ name: 'a.png', type: 'image/png', size: MAX_MAP_BYTES }).ok,
    ).toBe(true);
  });
});
