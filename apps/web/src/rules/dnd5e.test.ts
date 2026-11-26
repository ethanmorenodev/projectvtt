import { describe, expect, it } from 'vitest';
import type { ActorDto } from '../api/types';
import {
  abilityModifier,
  defaultDnd5eSheet,
  DND5E_ABILITIES,
  formatModifier,
  patchFromDraft,
  sheetDraftOf,
} from './dnd5e';

const actorFixture = (overrides: Partial<ActorDto> = {}): ActorDto => ({
  id: 'actor-1',
  campaignId: 'camp-1',
  name: 'Gandalf',
  rulesetKey: 'dnd5e',
  attributes: {
    strength: 16,
    dexterity: 14,
    constitution: 13,
    intelligence: 12,
    wisdom: 11,
    charisma: 10,
  },
  resources: { hitPoints: { current: 12, max: 12 } },
  version: 3,
  createdByUserId: 'user-1',
  createdAt: '2026-11-04T00:00:00Z',
  updatedAt: '2026-11-04T00:00:00Z',
  kind: 'pc',
  portraitId: null,
  biography: null,
  personality: null,
  level: 3,
  systemData: null,
  build: null,
  ...overrides,
});

describe('dnd5e presentación', () => {
  it('el modificador coincide con la tabla 5e', () => {
    expect(abilityModifier(1)).toBe(-5);
    expect(abilityModifier(10)).toBe(0);
    expect(abilityModifier(11)).toBe(0);
    expect(abilityModifier(16)).toBe(3);
    expect(abilityModifier(17)).toBe(3);
    expect(formatModifier(8)).toBe('-1');
    expect(formatModifier(14)).toBe('+2');
  });

  it('la ficha por defecto incluye las seis puntuaciones', () => {
    const sheet = defaultDnd5eSheet('Alric');
    expect(sheet.rulesetKey).toBe('dnd5e');
    expect(sheet.name).toBe('Alric');
    for (const key of DND5E_ABILITIES) {
      expect(sheet.attributes[key]).toBe(10);
    }
    expect(sheet.resources.hitPoints).toEqual({ current: 8, max: 8 });
  });
});

describe('borrador de edición de ficha', () => {
  it('sheetDraftOf rellena desde el actor con su versión', () => {
    const draft = sheetDraftOf(actorFixture());
    expect(draft.version).toBe(3);
    expect(draft.name).toBe('Gandalf');
    expect(draft.attributes.strength).toBe('16');
    expect(draft.attributes.charisma).toBe('10');
    expect(draft.hitPoints).toEqual({ current: '12', max: '12' });
  });

  it('patchFromDraft sin cambios devuelve el estado actual con la versión leída', () => {
    const actor = actorFixture();
    const patch = patchFromDraft(actor, sheetDraftOf(actor));
    expect(patch).toEqual({
      version: 3,
      name: 'Gandalf',
      attributes: actor.attributes,
      resources: actor.resources,
    });
  });

  it('patchFromDraft conserva atributos y recursos que el formulario no muestra', () => {
    const actor = actorFixture({
      attributes: { ...actorFixture().attributes, initiative: 2 },
      resources: { ...actorFixture().resources, mana: { current: 5, max: 10 } },
    });
    const draft = sheetDraftOf(actor);
    draft.attributes.strength = '18';
    const patch = patchFromDraft(actor, draft);
    expect(patch?.attributes.initiative).toBe(2);
    expect(patch?.attributes.strength).toBe(18);
    expect(patch?.resources.mana).toEqual({ current: 5, max: 10 });
    expect(patch?.resources.hitPoints).toEqual({ current: 12, max: 12 });
  });

  it('patchFromDraft rechaza nombre vacío y números no enteros', () => {
    const actor = actorFixture();
    const base = sheetDraftOf(actor);
    expect(patchFromDraft(actor, { ...base, name: '   ' })).toBeNull();
    expect(
      patchFromDraft(actor, {
        ...base,
        attributes: { ...base.attributes, strength: '16.5' },
      }),
    ).toBeNull();
    expect(
      patchFromDraft(actor, {
        ...base,
        attributes: { ...base.attributes, dexterity: '' },
      }),
    ).toBeNull();
    expect(
      patchFromDraft(actor, {
        ...base,
        hitPoints: { current: '12', max: '' },
      }),
    ).toBeNull();
  });

  it('patchFromDraft usa la versión del borrador, no la del actor ya refrescado', () => {
    const actor = actorFixture();
    const draft = sheetDraftOf(actor); // lee versión 3
    const yaEditado = actorFixture({ version: 4, name: 'Otro' }); // llegó un evento
    const patch = patchFromDraft(yaEditado, draft);
    // Con la versión 3 el servidor responde 409 y no se pisa el cambio ajeno.
    expect(patch?.version).toBe(3);
    expect(patch?.name).toBe('Gandalf');
  });
});
