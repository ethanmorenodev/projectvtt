/**
 * Validación del mapa importado por el usuario (tipo y tamaño) antes de
 * mostrarlo. El almacenamiento/persistencia del archivo queda pendiente de
 * decisión (object storage, ADR-007).
 */

export const MAX_MAP_BYTES = 10 * 1024 * 1024; // 10 MiB

export const ALLOWED_MAP_TYPES: readonly string[] = [
  'image/png',
  'image/jpeg',
  'image/webp',
  'image/svg+xml',
];

export interface MapFileLike {
  name: string;
  type: string;
  size: number;
}

export type MapValidation =
  | { ok: true }
  | { ok: false; reason: string };

export function validateMapFile(file: MapFileLike): MapValidation {
  if (!ALLOWED_MAP_TYPES.includes(file.type)) {
    return {
      ok: false,
      reason: `Tipo no permitido (${file.type || 'desconocido'}). Usa PNG, JPEG, WebP o SVG.`,
    };
  }

  if (file.size <= 0) {
    return { ok: false, reason: 'El archivo está vacío.' };
  }

  if (file.size > MAX_MAP_BYTES) {
    const maxMiB = MAX_MAP_BYTES / (1024 * 1024);
    return { ok: false, reason: `El archivo supera el límite de ${maxMiB} MiB.` };
  }

  return { ok: true };
}
