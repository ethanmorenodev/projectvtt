import { describe, expect, it } from 'vitest';
import type { ActorEffectDto } from '../api/types';
import {
  applyEffects,
  MAX_EFFECT_NAME_LENGTH,
  toCreateEffectRequest,
  type EffectsByActor,
} from './effects';

const effectFixture = (overrides: Partial<ActorEffectDto> = {}): ActorEffectDto => ({
  id: 'effect-1',
  actorId: 'actor-1',
  name: 'Envenenado',
  createdAt: '2026-11-06T00:00:00Z',
  ...overrides,
});

describe('estado de efectos', () => {
  it('applyEffects sustituye solo la lista del actor indicado', () => {
    const current: EffectsByActor = {
      'actor-1': [effectFixture()],
      'actor-2': [effectFixture({ id: 'effect-2', actorId: 'actor-2' })],
    };
    const next = applyEffects(current, {
      actorId: 'actor-1',
      effects: [],
    });

    expect(next['actor-1']).toHaveLength(0);
    // La lista ajena no se toca.
    expect(next['actor-2']).toHaveLength(1);
    // Inmutable: el estado anterior sigue intacto.
    expect(current['actor-1']).toHaveLength(1);
  });
});

describe('peticiones de efectos', () => {
  it('toCreateEffectRequest recorta el nombre', () => {
    expect(toCreateEffectRequest('  Aturdido  ')).toEqual({ name: 'Aturdido' });
  });

  it('toCreateEffectRequest rechaza nombres vacíos o demasiado largos', () => {
    expect(toCreateEffectRequest('')).toBeNull();
    expect(toCreateEffectRequest('   ')).toBeNull();
    expect(toCreateEffectRequest('x'.repeat(MAX_EFFECT_NAME_LENGTH + 1))).toBeNull();
    expect(toCreateEffectRequest('x'.repeat(MAX_EFFECT_NAME_LENGTH))).toEqual({
      name: 'x'.repeat(MAX_EFFECT_NAME_LENGTH),
    });
  });
});
