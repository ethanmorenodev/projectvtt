import { describe, expect, it } from 'vitest';
import type { FogStateDto } from '../api/types';
import { emptyFogState, isRevealed, worldToCell, worldToGridCell } from './fog';

const fogFixture = (revealed: Array<{ x: number; y: number }> = []): FogStateDto => ({
  sceneId: 'scene-1',
  gridCellSize: 32,
  revealed,
});

describe('estado de la niebla', () => {
  it('emptyFogState deja todo oculto', () => {
    expect(emptyFogState('scene-1', 32)).toEqual({
      sceneId: 'scene-1',
      gridCellSize: 32,
      revealed: [],
    });
  });

  it('isRevealed solo marca las celdas reveladas', () => {
    const fog = fogFixture([{ x: 1, y: -2 }]);
    expect(isRevealed(fog, 1, -2)).toBe(true);
    expect(isRevealed(fog, 0, 0)).toBe(false);
    expect(isRevealed(fog, -1, 2)).toBe(false);
  });
});

describe('celda de un punto del mundo', () => {
  it('worldToCell usa la celda que contiene el punto', () => {
    expect(worldToCell(0, 0, 32)).toEqual({ x: 0, y: 0 });
    expect(worldToCell(31.9, 63.9, 32)).toEqual({ x: 0, y: 1 });
    expect(worldToCell(32, 64, 32)).toEqual({ x: 1, y: 2 });
  });

  it('worldToCell redondea hacia abajo en negativos', () => {
    expect(worldToCell(-0.1, -32.1, 32)).toEqual({ x: -1, y: -2 });
    expect(worldToCell(-32, -64, 32)).toEqual({ x: -1, y: -2 });
  });
});

describe('worldToGridCell', () => {
  it('usa la celda cuadrada por defecto', () => {
    expect(worldToGridCell(33, 65, 32, 'square')).toEqual({ x: 1, y: 2 });
    expect(worldToGridCell(-0.1, -32.1, 32, 'square')).toEqual({ x: -1, y: -2 });
  });

  it('usa la celda hexagonal (odd-q) con grid hexagonal', () => {
    // Centro de la celda hexagonal (1, 0) con tamaño 32: (24, 8√3).
    expect(worldToGridCell(24, Math.sqrt(3) * 8, 32, 'hex')).toEqual({ x: 1, y: 0 });
    expect(worldToGridCell(0, 0, 32, 'hex')).toEqual({ x: 0, y: 0 });
  });
});
