import { describe, expect, it } from 'vitest';
import { prependRoll, upsertActor } from './actorStore';
import type { ActorDto, RollDto } from '../api/types';

const actor = (id: string, version: number, name: string): ActorDto => ({
  id,
  campaignId: 'camp-1',
  name,
  rulesetKey: 'dnd5e',
  attributes: { strength: 10 },
  resources: {},
  version,
  createdByUserId: null,
  createdAt: '2026-11-03T00:00:00Z',
  updatedAt: '2026-11-03T00:00:00Z',
  kind: 'pc',
  portraitId: null,
  biography: null,
  personality: null,
  level: 1,
  systemData: null,
  build: null,
});

const roll = (rollId: string): RollDto => ({
  rollId,
  campaignId: 'camp-1',
  actorId: 'actor-1',
  attribute: 'strength',
  breakdown: {
    rulesetKey: 'dnd5e',
    kind: 'ability-check',
    attribute: 'strength',
    attributeValue: 10,
    die: 12,
    modifier: 0,
    extra: 0,
    effectModifier: 0,
    dice: [12],
    rollMode: 'normal',
    effects: [],
    total: 12,
    dc: null,
    success: null,
    formula: '1d20(12) + 0 + 0 + 0 = 12',
  },
  rolledByUserId: 'user-1',
  occurredAt: '2026-11-03T00:00:00Z',
});

describe('actorStore', () => {
  it('upsertActor añade o sustituye si la versión no es más antigua', () => {
    let list = upsertActor([], actor('a', 0, 'Uno'));
    list = upsertActor(list, actor('a', 1, 'Dos'));
    expect(list).toHaveLength(1);
    expect(list[0].name).toBe('Dos');

    list = upsertActor(list, actor('a', 0, 'Viejo'));
    expect(list[0].name).toBe('Dos');

    list = upsertActor(list, actor('b', 0, 'Otro'));
    expect(list.map((item) => item.id)).toEqual(['a', 'b']);
  });

  it('prependRoll ignora duplicados y recorta el historial', () => {
    const first = prependRoll([], roll('r1'));
    const same = prependRoll(first, roll('r1'));
    expect(same).toHaveLength(1);

    const grown = prependRoll(first, roll('r2'), 2);
    expect(grown.map((item) => item.rollId)).toEqual(['r2', 'r1']);
    expect(prependRoll(grown, roll('r3'), 2).map((item) => item.rollId)).toEqual(['r3', 'r2']);
  });
});
