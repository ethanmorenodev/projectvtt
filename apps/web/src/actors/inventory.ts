/**
 * Inventario del actor (Épica E): estado `actorId → ítems` y peticiones
 * del formulario. El servidor valida permisos y rangos; aquí solo hay
 * sanidad de formulario (nombre no vacío, cantidad entera 1..9999), igual
 * que en `rules/dnd5e.ts` para la ficha.
 */

import type { ActorInventoryDto, ActorItemDto } from '../api/types';

/** Tope de sanidad en el cliente; el servidor valida lo mismo (ADR-013). */
export const MAX_ITEM_QUANTITY = 9999;

export type InventoryByActor = Record<string, ActorItemDto[]>;

/** Sustituye el inventario de UN actor (respuesta de mutación o evento). */
export function applyInventory(
  current: InventoryByActor,
  inventory: ActorInventoryDto,
): InventoryByActor {
  return { ...current, [inventory.actorId]: inventory.items };
}

/**
 * Cuerpo de `POST /api/v1/actors/{id}/items`. Devuelve `null` si el
 * nombre está vacío o la cantidad no es un entero en 1..9999.
 */
export function toCreateItemRequest(
  name: string,
  quantity: string,
): { name: string; quantity: number } | null {
  const trimmedName = name.trim();
  const trimmedQuantity = quantity.trim();
  if (trimmedName === '' || !/^\d+$/.test(trimmedQuantity)) {
    return null;
  }
  const value = Number(trimmedQuantity);
  if (value < 1 || value > MAX_ITEM_QUANTITY) {
    return null;
  }
  return { name: trimmedName, quantity: value };
}

/**
 * Cuerpo de `PATCH /api/v1/actors/{actorId}/items/{itemId}`: la versión
 * viaja desde el ítem leído (409 si otro cliente lo cambió antes).
 */
export function toItemPatch(
  item: ActorItemDto,
  name: string,
  quantity: string,
): { version: number; name: string; quantity: number } | null {
  const request = toCreateItemRequest(name, quantity);
  return request === null ? null : { version: item.version, ...request };
}
