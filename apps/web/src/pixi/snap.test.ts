import { describe, expect, it } from 'vitest';
import { snapPoint, snapToGrid } from './snap';

describe('snapToGrid', () => {
  it('ajusta al múltiplo más cercano de la celda', () => {
    expect(snapToGrid(17, 31, 32)).toEqual({ x: 32, y: 32 });
    expect(snapToGrid(16, 15, 32)).toEqual({ x: 32, y: 0 });
    expect(snapToGrid(0, 0, 32)).toEqual({ x: 0, y: 0 });
  });

  it('tolera celdas no positivas sin NaN', () => {
    expect(snapToGrid(10, 10, 0)).toEqual({ x: 10, y: 10 });
    expect(snapToGrid(10, 10, -4)).toEqual({ x: 10, y: 10 });
  });
});

describe('snapPoint', () => {
  it('usa el grid cuadrado por defecto', () => {
    expect(snapPoint(17, 31, 32, 'square')).toEqual({ x: 32, y: 32 });
  });

  it('ajusta al centro del hexágono con grid hexagonal', () => {
    // Centro de la celda hexagonal (1, 0) con tamaño 32: (24, 8√3).
    const center = { x: 24, y: Math.sqrt(3) * 8 };
    expect(snapPoint(center.x + 4, center.y - 3, 32, 'hex')).toEqual(center);
    expect(snapPoint(3, -2, 32, 'hex')).toEqual({ x: 0, y: 0 });
  });
});
