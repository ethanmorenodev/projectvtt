/**
 * Combate e iniciativa de la escena (Épica F, ADR-018): estado completo
 * del encuentro (el servidor lo manda ya ordenado por turno) y sanidad
 * del formulario de alta de combatientes. Leer es de miembro; modificar
 * es de GM+ y lo valida el servidor.
 */

import type { CombatStateDto, CombatantDto } from '../api/types';

/** Límites de sanidad; el servidor valida lo mismo (ADR-018). */
export const MIN_INITIATIVE = -100;
export const MAX_INITIATIVE = 100;
export const MAX_COMBATANT_NAME_LENGTH = 100;

/** Estado inicial sin encuentro en curso (antes de la primera carga). */
export function emptyCombatState(sceneId: string): CombatStateDto {
  return { sceneId, active: false, round: 0, currentCombatantId: null, combatants: [] };
}

/** ¿Tiene este combatiente el turno actual? */
export function isCurrentTurn(combat: CombatStateDto, combatant: CombatantDto): boolean {
  return combat.currentCombatantId === combatant.id;
}

/**
 * Cuerpo de `POST /api/v1/scenes/{id}/combat/combatants`. Devuelve
 * `null` si la iniciativa no es un entero en rango o si falta nombre y
 * no hay token del que tomarlo.
 */
export function toCreateCombatantRequest(
  name: string,
  initiative: string,
  tokenId: string | null,
): { name?: string; initiative: number; tokenId?: string } | null {
  const trimmed = name.trim();
  if (!tokenId && (trimmed.length < 1 || trimmed.length > MAX_COMBATANT_NAME_LENGTH)) {
    return null;
  }

  const value = initiative.trim() === '' ? Number.NaN : Number(initiative);
  if (!Number.isInteger(value) || value < MIN_INITIATIVE || value > MAX_INITIATIVE) {
    return null;
  }

  const request: { name?: string; initiative: number; tokenId?: string } = {
    initiative: value,
  };
  if (trimmed) {
    request.name = trimmed;
  }
  if (tokenId) {
    request.tokenId = tokenId;
  }
  return request;
}
