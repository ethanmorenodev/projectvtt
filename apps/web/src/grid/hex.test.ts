import { describe, expect, it } from 'vitest';
import { hexCellCenter, snapToHexCenter, worldToHexCell } from './hex';

const SIZE = 32; // R = 16

describe('hexCellCenter', () => {
  it('coloca las celdas en el retículo flat-top odd-q', () => {
    expect(hexCellCenter(0, 0, SIZE)).toEqual({ x: 0, y: 0 });

    // R = size/2 = 16: separación horizontal 1.5·R = 24.
    const col1 = hexCellCenter(1, 0, SIZE);
    expect(col1.x).toBeCloseTo(24);
    expect(col1.y).toBeCloseTo(Math.sqrt(3) * 8); // media celda abajo (columna impar)

    const col2 = hexCellCenter(2, 0, SIZE);
    expect(col2.x).toBeCloseTo(48);
    expect(col2.y).toBeCloseTo(0);
  });

  it('las columnas negativas impares también se desplazan hacia abajo', () => {
    const colMinus1 = hexCellCenter(-1, 0, SIZE);
    expect(colMinus1.x).toBeCloseTo(-24);
    expect(colMinus1.y).toBeCloseTo(Math.sqrt(3) * 8);
  });
});

describe('worldToHexCell', () => {
  it('un centro cae en su propia celda', () => {
    for (const [col, row] of [[0, 0], [1, 0], [2, 3], [-1, -2], [-3, 4]] as const) {
      const center = hexCellCenter(col, row, SIZE);
      expect(worldToHexCell(center.x, center.y, SIZE)).toEqual({ x: col, y: row });
    }
  });

  it('un punto cercano al centro cae en la misma celda', () => {
    expect(worldToHexCell(3, -2, SIZE)).toEqual({ x: 0, y: 0 });
  });
});

describe('snapToHexCenter', () => {
  it('devuelve el centro del hexágono más cercano', () => {
    const center = hexCellCenter(1, 0, SIZE);
    const snapped = snapToHexCenter(center.x + 4, center.y - 3, SIZE);
    expect(snapped.x).toBeCloseTo(center.x);
    expect(snapped.y).toBeCloseTo(center.y);
  });
});
