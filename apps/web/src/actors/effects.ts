/**
 * Efectos y condiciones del actor (Épica E): estado `actorId → efectos` y
 * petición del formulario. Sin duración (ADR-014). El servidor valida
 * permisos y unicidad; aquí solo hay sanidad de formulario (nombre no
 * vacío, hasta 100 caracteres), igual que en `actors/inventory.ts`.
 */

import type { ActorEffectDto, ActorEffectsDto } from '../api/types';

/** Tope de sanidad en el cliente; el servidor valida lo mismo (ADR-014). */
export const MAX_EFFECT_NAME_LENGTH = 100;

export type EffectsByActor = Record<string, ActorEffectDto[]>;

/** Sustituye la lista de UN actor (respuesta de mutación o evento). */
export function applyEffects(
  current: EffectsByActor,
  effects: ActorEffectsDto,
): EffectsByActor {
  return { ...current, [effects.actorId]: effects.effects };
}

/**
 * Cuerpo de `POST /api/v1/actors/{id}/effects`. Devuelve `null` si el
 * nombre queda vacío o supera los 100 caracteres.
 */
export function toCreateEffectRequest(
  name: string,
): { name: string } | null {
  const trimmed = name.trim();
  if (trimmed.length < 1 || trimmed.length > MAX_EFFECT_NAME_LENGTH) {
    return null;
  }
  return { name: trimmed };
}
