/**
 * Cámara de la vista (zoom y pan). Funciones puras sobre el transformado
 * del mundo: pantalla = mundo * zoom + traslación.
 */

export interface Camera {
  tx: number;
  ty: number;
  zoom: number;
}

export const MIN_ZOOM = 0.25;
export const MAX_ZOOM = 4;

export const initialCamera = (): Camera => ({ tx: 0, ty: 0, zoom: 1 });

export function screenToWorld(camera: Camera, sx: number, sy: number): { x: number; y: number } {
  return { x: (sx - camera.tx) / camera.zoom, y: (sy - camera.ty) / camera.zoom };
}

export function worldToScreen(camera: Camera, wx: number, wy: number): { x: number; y: number } {
  return { x: wx * camera.zoom + camera.tx, y: wy * camera.zoom + camera.ty };
}

/** Traslación (pan) en píxeles de pantalla. */
export function pan(camera: Camera, dx: number, dy: number): Camera {
  return { ...camera, tx: camera.tx + dx, ty: camera.ty + dy };
}

/**
 * Zoom multiplicativo anclado a un punto de pantalla: el mundo bajo el
 * cursor permanece en el mismo pixel tras el cambio de zoom.
 */
export function zoomAt(camera: Camera, sx: number, sy: number, factor: number): Camera {
  const nextZoom = clampZoom(camera.zoom * factor);
  const anchor = screenToWorld(camera, sx, sy);
  return {
    zoom: nextZoom,
    tx: sx - anchor.x * nextZoom,
    ty: sy - anchor.y * nextZoom,
  };
}

export function clampZoom(zoom: number): number {
  return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
}
