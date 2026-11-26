/**
 * Ajuste opcional de coordenadas al centro de una celda del grid
 * (cuadrada o hexagonal, ADR-023). El servidor no conoce esta
 * preferencia: el cliente envía ya las coordenadas ajustadas
 * (PROJECT_SPEC §2 P0).
 */

import type { GridShape } from '../api/types';
import { snapToHexCenter } from '../grid/hex';

export function snapToGrid(x: number, y: number, cellSize: number): { x: number; y: number } {
  const size = cellSize > 0 ? cellSize : 1;
  return {
    x: Math.round(x / size) * size,
    y: Math.round(y / size) * size,
  };
}

/** Ajuste al centro de la celda según la forma del grid de la escena. */
export function snapPoint(
  x: number,
  y: number,
  cellSize: number,
  shape: GridShape,
): { x: number; y: number } {
  return shape === 'hex'
    ? snapToHexCenter(x, y, cellSize > 0 ? cellSize : 1)
    : snapToGrid(x, y, cellSize);
}
