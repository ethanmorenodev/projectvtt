import { describe, expect, it } from 'vitest';
import { historyLabel, historyTime } from './history';

describe('historial de cambios', () => {
  it('historyLabel traduce los tipos conocidos y deja los desconocidos', () => {
    expect(historyLabel('actor-created')).toBe('Ficha');
    expect(historyLabel('roll-resolved')).toBe('Tirada');
    expect(historyLabel('combat-ended')).toBe('Combate');
    expect(historyLabel('tipo-futuro')).toBe('tipo-futuro');
  });

  it('historyTime muestra la hora UTC del ISO del servidor', () => {
    expect(historyTime('2026-11-09T12:34:56.789Z')).toBe('12:34');
  });
});
