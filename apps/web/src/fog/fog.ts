/**
 * Niebla de guerra manual (Épica F, ADR-020): estado de celdas
 * reveladas (lo demás queda oculto) y utilidades de celda. El servidor
 * valida permisos y coordenadas; aquí solo se calcula la celda de un
 * punto del mundo y se consulta el estado.
 */

import type { FogCellDto, FogStateDto, GridShape } from '../api/types';
import { worldToHexCell } from '../grid/hex';

/** Límites de sanidad de las coordenadas; el servidor valida lo mismo. */
export const MIN_CELL = -10000;
export const MAX_CELL = 10000;

/** Estado inicial: nada revelado (todo oculto). */
export function emptyFogState(sceneId: string, gridCellSize: number): FogStateDto {
  return { sceneId, gridCellSize, revealed: [] };
}

/** ¿Está revelada la celda (x, y)? */
export function isRevealed(state: FogStateDto, x: number, y: number): boolean {
  return state.revealed.some((cell) => cell.x === x && cell.y === y);
}

/** Celda del grid que contiene un punto del mundo (negativos hacia abajo). */
export function worldToCell(x: number, y: number, gridCellSize: number): FogCellDto {
  return { x: Math.floor(x / gridCellSize), y: Math.floor(y / gridCellSize) };
}

/**
 * Celda que contiene un punto según la forma del grid (ADR-023):
 * cuadrada por defecto; hexagonal flat-top con coordenadas odd-q.
 */
export function worldToGridCell(
  x: number,
  y: number,
  gridCellSize: number,
  shape: GridShape,
): FogCellDto {
  return shape === 'hex'
    ? worldToHexCell(x, y, gridCellSize)
    : worldToCell(x, y, gridCellSize);
}
