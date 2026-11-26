import { describe, expect, it } from 'vitest';
import type { CombatStateDto } from '../api/types';
import {
  emptyCombatState,
  isCurrentTurn,
  MAX_COMBATANT_NAME_LENGTH,
  MAX_INITIATIVE,
  MIN_INITIATIVE,
  toCreateCombatantRequest,
} from './combat';

const combatFixture = (overrides: Partial<CombatStateDto> = {}): CombatStateDto => ({
  sceneId: 'scene-1',
  active: true,
  round: 1,
  currentCombatantId: null,
  combatants: [
    {
      id: 'c-1',
      sceneId: 'scene-1',
      name: 'Héroe',
      initiative: 18,
      tokenId: null,
      createdAt: '2026-11-09T00:00:00Z',
    },
    {
      id: 'c-2',
      sceneId: 'scene-1',
      name: 'Goblin',
      initiative: 12,
      tokenId: 'token-1',
      createdAt: '2026-11-09T00:00:01Z',
    },
  ],
  ...overrides,
});

describe('estado del combate', () => {
  it('emptyCombatState describe una escena sin encuentro', () => {
    expect(emptyCombatState('scene-1')).toEqual({
      sceneId: 'scene-1',
      active: false,
      round: 0,
      currentCombatantId: null,
      combatants: [],
    });
  });

  it('isCurrentTurn solo marca al combatiente del turno', () => {
    const combat = combatFixture({ currentCombatantId: 'c-1' });
    expect(isCurrentTurn(combat, combat.combatants[0])).toBe(true);
    expect(isCurrentTurn(combat, combat.combatants[1])).toBe(false);
  });
});

describe('peticiones de combatientes', () => {
  it('toCreateCombatantRequest recorta el nombre y convierte la iniciativa', () => {
    expect(toCreateCombatantRequest('  Goblin  ', '12', null)).toEqual({
      name: 'Goblin',
      initiative: 12,
    });
  });

  it('sin nombre solo se permite con token (el servidor toma su nombre)', () => {
    expect(toCreateCombatantRequest('', '12', 'token-1')).toEqual({
      initiative: 12,
      tokenId: 'token-1',
    });
    expect(toCreateCombatantRequest('   ', '12', null)).toBeNull();
  });

  it('toCreateCombatantRequest rechaza iniciativas no enteras o fuera de rango', () => {
    expect(toCreateCombatantRequest('Goblin', '', null)).toBeNull();
    expect(toCreateCombatantRequest('Goblin', 'doce', null)).toBeNull();
    expect(toCreateCombatantRequest('Goblin', '12.5', null)).toBeNull();
    expect(toCreateCombatantRequest('Goblin', String(MAX_INITIATIVE + 1), null)).toBeNull();
    expect(toCreateCombatantRequest('Goblin', String(MIN_INITIATIVE - 1), null)).toBeNull();
    expect(toCreateCombatantRequest('Goblin', String(MAX_INITIATIVE), null)).toEqual({
      name: 'Goblin',
      initiative: MAX_INITIATIVE,
    });
    expect(toCreateCombatantRequest('Goblin', String(MIN_INITIATIVE), null)).toEqual({
      name: 'Goblin',
      initiative: MIN_INITIATIVE,
    });
  });

  it('toCreateCombatantRequest rechaza nombres demasiado largos', () => {
    expect(
      toCreateCombatantRequest('x'.repeat(MAX_COMBATANT_NAME_LENGTH + 1), '12', null),
    ).toBeNull();
    expect(
      toCreateCombatantRequest('x'.repeat(MAX_COMBATANT_NAME_LENGTH), '12', null),
    ).toEqual({ name: 'x'.repeat(MAX_COMBATANT_NAME_LENGTH), initiative: 12 });
  });
});
