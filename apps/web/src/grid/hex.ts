/**
 * Matemática del grid hexagonal (Épica D, ADR-023): hexágonos flat-top
 * con coordenadas de celda "odd-q" (las columnas impares se desplazan
 * media celda hacia abajo). `size` es el ancho del hexágono (esquina a
 * esquina), igual que `GridCellSize` es el lado de la celda cuadrada.
 */

const SQRT3 = Math.sqrt(3);

export interface GridCell {
  x: number;
  y: number;
}

/** Centro del hexágono de la celda (col, row). */
export function hexCellCenter(col: number, row: number, size: number): { x: number; y: number } {
  const r = size / 2;
  return {
    x: 1.5 * r * col,
    y: SQRT3 * r * (row + ((col & 1) === 1 ? 0.5 : 0)),
  };
}

/** Celda (odd-q) que contiene un punto del mundo, al hexágono más cercano. */
export function worldToHexCell(x: number, y: number, size: number): GridCell {
  const r = size / 2;
  // Fraccional axial (flat-top) y redondeo cúbico al hexágono más cercano.
  const qf = ((2 / 3) * x) / r;
  const rf = ((-1 / 3) * x + (SQRT3 / 3) * y) / r;
  const sf = -qf - rf;
  let q = Math.round(qf);
  let axialR = Math.round(rf);
  const s = Math.round(sf);
  const qDiff = Math.abs(q - qf);
  const rDiff = Math.abs(axialR - rf);
  const sDiff = Math.abs(s - sf);
  if (qDiff > rDiff && qDiff > sDiff) {
    q = -axialR - s;
  } else if (rDiff > sDiff) {
    axialR = -q - s;
  }

  // Axial → offset odd-q (columnas impares desplazadas hacia abajo).
  return { x: q, y: axialR + (q - (q & 1)) / 2 };
}

/** Centro del hexágono más cercano a un punto (ajuste al grid hexagonal). */
export function snapToHexCenter(x: number, y: number, size: number): { x: number; y: number } {
  const cell = worldToHexCell(x, y, size);
  return hexCellCenter(cell.x, cell.y, size);
}
