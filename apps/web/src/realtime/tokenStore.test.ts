import { describe, expect, it } from 'vitest';
import {
  applyLocalMove,
  applyMoveResult,
  applySync,
  applyTokenCreated,
  applyTokenMoved,
  emptyTokens,
  upsertToken,
} from './tokenStore';
import type { TokenDto, TokenMovedEvent } from '../api/types';

const token = (id: string, x: number, y: number, version: number): TokenDto => ({
  id,
  sceneId: 'scene-1',
  name: `Token ${id}`,
  x,
  y,
  version,
  updatedByUserId: null,
  actorId: null,
  updatedAt: '2026-11-03T00:00:00Z',
});

const moved = (tokenId: string, x: number, y: number, version: number): TokenMovedEvent => ({
  eventId: `evt-${version}`,
  campaignId: 'camp-1',
  sceneId: 'scene-1',
  tokenId,
  actorUserId: 'user-1',
  occurredAt: '2026-11-03T00:00:00Z',
  schemaVersion: 1,
  aggregateVersion: version,
  x,
  y,
});

describe('tokenStore', () => {
  it('applySync construye el estado a partir del servidor', () => {
    const state = applySync([token('a', 10, 20, 1), token('b', 30, 40, 2)]);
    expect(Object.keys(state).sort()).toEqual(['a', 'b']);
    expect(state.a).toEqual({
      id: 'a',
      name: 'Token a',
      x: 10,
      y: 20,
      version: 1,
      actorId: null,
    });
  });

  it('applyTokenMoved aplica versiones nuevas y descarta las obsoletas', () => {
    let state = applySync([token('a', 10, 20, 3)]);

    state = applyTokenMoved(state, moved('a', 99, 99, 2)); // obsoleta
    expect(state.a.x).toBe(10);

    state = applyTokenMoved(state, moved('a', 50, 60, 4));
    expect(state.a).toMatchObject({ x: 50, y: 60, version: 4 });

    state = applyTokenMoved(state, moved('a', 70, 70, 4)); // misma versión: no cambia
    expect(state.a.x).toBe(50);
  });

  it('applyTokenMoved tolera un token desconocido (evento previo a SyncScene)', () => {
    const state = applyTokenMoved(emptyTokens(), moved('nuevo', 5, 6, 1));
    expect(state.nuevo).toMatchObject({ x: 5, y: 6, version: 1 });
  });

  it('applyTokenCreated añade el token y no degrada una versión más nueva', () => {
    let state = applySync([token('a', 10, 20, 5)]);
    const created = {
      eventId: 'evt-1',
      campaignId: 'camp-1',
      sceneId: 'scene-1',
      actorUserId: 'user-1',
      occurredAt: '2026-11-03T00:00:00Z',
      schemaVersion: 1,
      token: token('a', 99, 99, 3),
    };

    state = applyTokenCreated(state, created);
    expect(state.a.x).toBe(10);

    state = applyTokenCreated(state, { ...created, token: token('b', 1, 2, 1) });
    expect(state.b).toBeDefined();
  });

  it('applyLocalMove predice la posición con la versión esperada', () => {
    const state = applyLocalMove(applySync([token('a', 10, 20, 3)]), 'a', 40, 50, 3);
    expect(state.a).toMatchObject({ x: 40, y: 50, version: 3 });
  });

  it('applyMoveResult confirma versión y coordenadas del servidor', () => {
    let state = applySync([token('a', 10, 20, 3)]);
    state = applyLocalMove(state, 'a', 40, 50, 3);
    state = applyMoveResult(state, {
      commandId: 'cmd-1',
      tokenId: 'a',
      version: 4,
      x: 41,
      y: 51,
      duplicate: false,
    });
    expect(state.a).toMatchObject({ x: 41, y: 51, version: 4 });
  });

  it('upsertToken ignora tokens con versión más antigua', () => {
    const state = upsertToken(applySync([token('a', 10, 20, 5)]), token('a', 1, 1, 4));
    expect(state.a.version).toBe(5);
  });
});
