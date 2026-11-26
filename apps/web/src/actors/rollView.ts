import type { RollBreakdown } from '../api/types';

/**
 * Etiqueta de la modalidad de tirada para la UI (ADR-015). Devuelve
 * null en modalidad normal, para no añadir ruido a la fórmula, que ya
 * la incluye. La interpretación de los efectos vive en el servidor:
 * aquí solo se traduce el campo del contrato.
 */
export function rollModeLabel(
  rollMode: RollBreakdown['rollMode'],
): string | null {
  if (rollMode === 'advantage') return 'Ventaja';
  if (rollMode === 'disadvantage') return 'Desventaja';
  return null;
}
