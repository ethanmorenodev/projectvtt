import { describe, expect, it } from 'vitest';
import type { ActorItemDto } from '../api/types';
import {
  applyInventory,
  MAX_ITEM_QUANTITY,
  toCreateItemRequest,
  toItemPatch,
  type InventoryByActor,
} from './inventory';

const itemFixture = (overrides: Partial<ActorItemDto> = {}): ActorItemDto => ({
  id: 'item-1',
  actorId: 'actor-1',
  name: 'Antorcha',
  quantity: 1,
  version: 2,
  createdAt: '2026-11-06T00:00:00Z',
  updatedAt: '2026-11-06T00:00:00Z',
  slotId: null,
  contentItemId: null,
  weight: null,
  volume: null,
  ...overrides,
});

describe('estado de inventario', () => {
  it('applyInventory sustituye solo el inventario del actor indicado', () => {
    const current: InventoryByActor = {
      'actor-1': [itemFixture()],
      'actor-2': [itemFixture({ id: 'item-2', actorId: 'actor-2' })],
    };
    const next = applyInventory(current, {
      actorId: 'actor-1',
      items: [itemFixture({ id: 'item-3', name: 'Cuerda' })],
    });

    expect(next['actor-1']).toHaveLength(1);
    expect(next['actor-1'][0].name).toBe('Cuerda');
    // El inventario ajeno no se toca.
    expect(next['actor-2'][0].id).toBe('item-2');
    // Inmutable: el estado anterior sigue intacto.
    expect(current['actor-1'][0].name).toBe('Antorcha');
  });
});

describe('peticiones de inventario', () => {
  it('toCreateItemRequest recorta el nombre y convierte la cantidad', () => {
    expect(toCreateItemRequest('  Antorcha  ', '3')).toEqual({
      name: 'Antorcha',
      quantity: 3,
    });
  });

  it('toCreateItemRequest rechaza nombre vacío y cantidades inválidas', () => {
    expect(toCreateItemRequest('   ', '1')).toBeNull();
    expect(toCreateItemRequest('Flechas', '')).toBeNull();
    expect(toCreateItemRequest('Flechas', '0')).toBeNull();
    expect(toCreateItemRequest('Flechas', '2.5')).toBeNull();
    expect(toCreateItemRequest('Flechas', '-4')).toBeNull();
    expect(toCreateItemRequest('Flechas', 'abc')).toBeNull();
    expect(toCreateItemRequest('Flechas', String(MAX_ITEM_QUANTITY + 1))).toBeNull();
    expect(toCreateItemRequest('Flechas', String(MAX_ITEM_QUANTITY))).toEqual({
      name: 'Flechas',
      quantity: MAX_ITEM_QUANTITY,
    });
  });

  it('toItemPatch lleva la versión leída del ítem (409 si cambió)', () => {
    const item = itemFixture();
    expect(toItemPatch(item, 'Antorcha grande', '2')).toEqual({
      version: 2,
      name: 'Antorcha grande',
      quantity: 2,
    });
    // Formulario inválido: no se envía nada.
    expect(toItemPatch(item, '', '2')).toBeNull();
    expect(toItemPatch(item, 'Antorcha', '0')).toBeNull();
  });
});
