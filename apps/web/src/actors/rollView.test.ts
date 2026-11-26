import { describe, expect, it } from 'vitest';
import { rollModeLabel } from './rollView';

describe('rollView', () => {
  it('rollModeLabel traduce la modalidad del contrato', () => {
    expect(rollModeLabel('normal')).toBeNull();
    expect(rollModeLabel('advantage')).toBe('Ventaja');
    expect(rollModeLabel('disadvantage')).toBe('Desventaja');
  });
});
