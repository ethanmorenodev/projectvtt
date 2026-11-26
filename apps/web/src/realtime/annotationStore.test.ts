import { describe, expect, it, vi } from 'vitest';
import { emptyAnnotationsState, get, replace, subscribe } from './annotationStore';
import type { SceneAnnotationDto, SceneAnnotationsState } from '../api/types';

const annotation = (sceneId: string, id: string, version: number): SceneAnnotationDto => ({
  id,
  sceneId,
  authorUserId: 'user-1',
  kind: 'note',
  points: [{ x: 1, y: 2 }],
  text: `Nota ${id}`,
  version,
  createdAt: '2026-11-25T00:00:00Z',
  updatedAt: '2026-11-25T00:00:00Z',
});

const state = (
  sceneId: string,
  version: number,
  items: SceneAnnotationDto[] = [],
): SceneAnnotationsState => ({ sceneId, items, version });

describe('annotationStore', () => {
  it('devuelve un estado vacío estable antes de la carga', () => {
    const first = get('scene-empty');
    expect(first).toEqual(emptyAnnotationsState('scene-empty'));
    expect(get('scene-empty')).toBe(first);
  });

  it('replace sustituye el estado por el del servidor', () => {
    const loaded = state('scene-replace', 2, [annotation('scene-replace', 'a1', 1)]);
    replace(loaded);
    expect(get('scene-replace')).toEqual(loaded);

    const next = state('scene-replace', 3, [
      annotation('scene-replace', 'a1', 1),
      annotation('scene-replace', 'a2', 2),
    ]);
    replace(next);
    expect(get('scene-replace')).toEqual(next);
  });

  it('descarta versiones antiguas o repetidas', () => {
    const current = state('scene-order', 3, [annotation('scene-order', 'a1', 2)]);
    replace(current);
    replace(state('scene-order', 2, [annotation('scene-order', 'old', 1)]));
    replace(state('scene-order', 3, [annotation('scene-order', 'other', 2)]));
    expect(get('scene-order')).toBe(current);
  });

  it('acepta el estado vacío de clear aunque reinicie la versión a 0', () => {
    replace(state('scene-clear', 4, [annotation('scene-clear', 'a1', 3)]));
    const cleared = state('scene-clear', 0);
    replace(cleared);
    expect(get('scene-clear')).toBe(cleared);

    // Un estado vacío posterior con la misma versión es solo un duplicado.
    replace(cleared);
    expect(get('scene-clear')).toBe(cleared);
  });

  it('notifica a los suscritos solo cuando el estado cambia', () => {
    const sceneId = 'scene-subscribe';
    const listener = vi.fn();
    const unsubscribe = subscribe(sceneId, listener);

    replace(state(sceneId, 1));
    expect(listener).toHaveBeenCalledTimes(1);
    replace(state(sceneId, 1));
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
    replace(state(sceneId, 2));
    expect(listener).toHaveBeenCalledTimes(1);
    expect(get(sceneId).version).toBe(2);
  });
});
