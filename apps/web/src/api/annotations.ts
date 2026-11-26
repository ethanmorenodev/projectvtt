import { apiFetch } from './client';
import type { SceneAnnotationsState, SetSceneAnnotationRequest } from './types';

/** Anotaciones compartidas de una escena (ADR-043). */

export function fetchAnnotations(sceneId: string): Promise<SceneAnnotationsState> {
  return apiFetch<SceneAnnotationsState>(
    `/api/v1/scenes/${encodeURIComponent(sceneId)}/annotations`,
  );
}

export function setAnnotations(
  sceneId: string,
  request: SetSceneAnnotationRequest,
): Promise<SceneAnnotationsState> {
  return apiFetch<SceneAnnotationsState>(
    `/api/v1/scenes/${encodeURIComponent(sceneId)}/annotations`,
    { method: 'PUT', body: request },
  );
}
