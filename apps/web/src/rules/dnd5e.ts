/**
 * Presentación del primer Ruleset (D&D 5e / 5.5e). La validación y el
 * dado viven en el servidor; aquí solo hay etiquetas, modificador para
 * mostrar, una ficha por defecto al crear un actor y el borrador de edición
 * (el rango 3..30 de un atributo lo valida el servidor, no se duplica aquí).
 */

import type { ActorDto, ResourceValue } from '../api/types';

export const DND5E_KEY = 'dnd5e';

export const DND5E_ABILITIES = [
  'strength',
  'dexterity',
  'constitution',
  'intelligence',
  'wisdom',
  'charisma',
] as const;

export type Dnd5eAbility = (typeof DND5E_ABILITIES)[number];

export const DND5E_ABILITY_LABELS: Record<Dnd5eAbility, string> = {
  strength: 'Fuerza',
  dexterity: 'Destreza',
  constitution: 'Constitución',
  intelligence: 'Inteligencia',
  wisdom: 'Sabiduría',
  charisma: 'Carisma',
};

/** Igual que `Dnd5eRuleset.Modifier` en el backend: piso((valor − 10) / 2). */
export function abilityModifier(value: number): number {
  return Math.floor((value - 10) / 2);
}

export function formatModifier(value: number): string {
  const modifier = abilityModifier(value);
  return modifier >= 0 ? `+${modifier}` : String(modifier);
}

export function defaultDnd5eSheet(name: string) {
  return {
    name,
    rulesetKey: DND5E_KEY,
    attributes: {
      strength: 10,
      dexterity: 10,
      constitution: 10,
      intelligence: 10,
      wisdom: 10,
      charisma: 10,
    },
    resources: {
      hitPoints: { current: 8, max: 8 },
    },
  };
}

/** Borrador de edición de una ficha: todo en texto hasta pulsar Guardar. */
export interface ActorSheetDraft {
  /** Versión leída al abrir el editor: viaja en el PATCH (409 si cambió). */
  version: number;
  name: string;
  attributes: Record<Dnd5eAbility, string>;
  hitPoints: { current: string; max: string } | null;
}

/** Cuerpo de `PATCH /api/v1/actors/{id}`. */
export interface ActorPatch {
  version: number;
  name: string;
  attributes: Record<string, number>;
  resources: Record<string, ResourceValue>;
}

/** Rellena el borrador de edición a partir del actor seleccionado. */
export function sheetDraftOf(actor: ActorDto): ActorSheetDraft {
  const attributes = {} as Record<Dnd5eAbility, string>;
  for (const ability of DND5E_ABILITIES) {
    attributes[ability] = String(actor.attributes[ability] ?? 10);
  }
  const hitPoints = actor.resources.hitPoints;
  return {
    version: actor.version,
    name: actor.name,
    attributes,
    hitPoints: hitPoints
      ? { current: String(hitPoints.current), max: String(hitPoints.max) }
      : null,
  };
}

/**
 * Convierte el borrador en el cuerpo del PATCH. Conserva atributos y
 * recursos que el formulario no muestra (el PATCH sustituye el diccionario
 * completo). Devuelve `null` si el nombre está vacío o algún campo no es un
 * entero: solo sanidad del formulario, los rangos los valida el servidor.
 */
export function patchFromDraft(actor: ActorDto, draft: ActorSheetDraft): ActorPatch | null {
  const name = draft.name.trim();
  if (name === '') {
    return null;
  }

  const attributes: Record<string, number> = { ...actor.attributes };
  for (const ability of DND5E_ABILITIES) {
    const value = toInteger(draft.attributes[ability]);
    if (value === null) {
      return null;
    }
    attributes[ability] = value;
  }

  const resources: Record<string, ResourceValue> = { ...actor.resources };
  if (draft.hitPoints !== null) {
    const current = toInteger(draft.hitPoints.current);
    const max = toInteger(draft.hitPoints.max);
    if (current === null || max === null) {
      return null;
    }
    resources.hitPoints = { current, max };
  }

  return { version: draft.version, name, attributes, resources };
}

function toInteger(text: string): number | null {
  const trimmed = text.trim();
  return /^-?\d+$/.test(trimmed) ? Number(trimmed) : null;
}
