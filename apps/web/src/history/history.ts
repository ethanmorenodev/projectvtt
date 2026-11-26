/**
 * Historial de cambios (Épica F, ADR-021): presentación de las entradas
 * que registra el servidor. Solo lectura: no hay peticiones que formular
 * ni estado que calcular.
 */

/** Etiqueta corta por tipo de entrada (el resumen ya viene redactado). */
const KIND_LABELS: Record<string, string> = {
  'scene-created': 'Escena',
  'token-created': 'Token',
  'actor-created': 'Ficha',
  'actor-updated': 'Ficha',
  'item-created': 'Inventario',
  'item-updated': 'Inventario',
  'item-deleted': 'Inventario',
  'effect-applied': 'Efecto',
  'effect-removed': 'Efecto',
  'roll-resolved': 'Tirada',
  'combat-started': 'Combate',
  'combat-ended': 'Combate',
};

/** Etiqueta del tipo de entrada; el tipo tal cual si es desconocido. */
export function historyLabel(kind: string): string {
  return KIND_LABELS[kind] ?? kind;
}

/** Hora UTC de la entrada en formato HH:MM (del ISO del servidor). */
export function historyTime(iso: string): string {
  return iso.slice(11, 16);
}
