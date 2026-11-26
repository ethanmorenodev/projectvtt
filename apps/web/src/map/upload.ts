/**
 * Mapa persistido de la escena (ADR-028): subida, descarga y borrado del
 * mapa compartido en el servidor. El archivo se valida también en la
 * web (`validateMapFile`) por UX, pero el servidor es la autoridad.
 */

import { ApiError, apiFetch, apiFetchBlob } from '../api/client';
import type { MapAssetDto, MapUpdatedEvent } from '../api/types';

/** Sube (o reemplaza) el mapa de la escena. */
export async function uploadSceneMap(
  sceneId: string,
  file: File,
): Promise<MapAssetDto> {
  const form = new FormData();
  form.append('file', file, file.name);
  return await apiFetch<MapAssetDto>(`/api/v1/scenes/${sceneId}/map`, {
    method: 'PUT',
    body: form,
  });
}

/**
 * Descarga el mapa compartido como URL de objeto para el canvas; null si
 * la escena no tiene mapa.
 */
export async function fetchSceneMap(
  sceneId: string,
): Promise<{ url: string; fileName: string } | null> {
  try {
    const blob = await apiFetchBlob(`/api/v1/scenes/${sceneId}/map`);
    return { url: URL.createObjectURL(blob), fileName: 'mapa compartido' };
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) {
      return null;
    }
    throw cause;
  }
}

/** Borra el mapa de la escena (GM+). */
export async function deleteSceneMap(sceneId: string): Promise<void> {
  await apiFetch(`/api/v1/scenes/${sceneId}/map`, { method: 'DELETE' });
}

/** ¿El evento `MapUpdated` afecta a esta escena? */
export function shouldReloadMap(event: MapUpdatedEvent, sceneId: string): boolean {
  return event.sceneId === sceneId;
}

/** ¿El evento indica que la escena se quedó sin mapa? */
export function mapWasRemoved(event: MapUpdatedEvent): boolean {
  return event.map === null;
}

/** Nombre del archivo para mostrar en la UI (o genérico). */
export function mapDisplayName(event: MapUpdatedEvent): string {
  return event.map?.fileName ?? 'mapa compartido';
}
