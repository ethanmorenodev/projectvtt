/**
 * Anotaciones compartidas por escena (ADR-043). El store recibe el estado
 * directo (`SceneAnnotationsState`): la carga HTTP y el evento
 * `SceneAnnotationsUpdated.event.annotations` alimentan el mismo sitio.
 * Las versiones obsoletas o repetidas se descartan para que un evento
 * atrasado tras reconectar no restaure un estado antiguo.
 */

import { useCallback, useSyncExternalStore } from 'react';
import type { SceneAnnotationsState } from '../api/types';

const states = new Map<string, SceneAnnotationsState>();
const listeners = new Map<string, Set<() => void>>();

export function emptyAnnotationsState(sceneId: string): SceneAnnotationsState {
  return { sceneId, items: [], version: 0 };
}

function ensure(sceneId: string): SceneAnnotationsState {
  let state = states.get(sceneId);
  if (!state) {
    state = emptyAnnotationsState(sceneId);
    states.set(sceneId, state);
  }
  return state;
}

/** Instantánea estable por escena (requisito de `useSyncExternalStore`). */
export function get(sceneId: string): SceneAnnotationsState {
  return ensure(sceneId);
}

export function subscribe(sceneId: string, listener: () => void): () => void {
  let set = listeners.get(sceneId);
  if (!set) {
    set = new Set();
    listeners.set(sceneId, set);
  }
  set.add(listener);
  return () => {
    set?.delete(listener);
  };
}

/** Sustituye el estado por el servidor (HTTP o evento en vivo). */
export function replace(state: SceneAnnotationsState): void {
  const current = ensure(state.sceneId);
  // `clear:true` reinicia la versión a 0: ese estado vacío es una
  // actualización legítima, no un evento atrasado.
  const isClear = state.version === 0 && state.items.length === 0 && current.version > 0;
  if (!isClear && state.version <= current.version) {
    return;
  }
  states.set(state.sceneId, state);
  listeners.get(state.sceneId)?.forEach((listener) => listener());
}

export function useSceneAnnotations(sceneId: string): SceneAnnotationsState {
  const subscribeToScene = useCallback(
    (listener: () => void) => subscribe(sceneId, listener),
    [sceneId],
  );
  const getSnapshot = useCallback(() => get(sceneId), [sceneId]);
  return useSyncExternalStore(subscribeToScene, getSnapshot);
}
