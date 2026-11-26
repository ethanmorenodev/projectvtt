/**
 * Estado autoritativo de los tokens de una escena. Funciones puras: cada
 * actualización se aplica solo si trae una versión más nueva, de modo que
 * los eventos se reordenan o duplican sin corromper el estado (el cliente
 * nunca restaura una posición obsoleta tras reconectar: `applySync` manda).
 */

import type { MoveTokenResult, TokenCreatedEvent, TokenDto, TokenMovedEvent } from '../api/types';

export interface TokenView {
  id: string;
  name: string;
  x: number;
  y: number;
  version: number;
  actorId: string | null;
}

export type TokenState = Record<string, TokenView>;

export const emptyTokens = (): TokenState => ({});

function toView(token: TokenDto): TokenView {
  return {
    id: token.id,
    name: token.name,
    x: token.x,
    y: token.y,
    version: token.version,
    actorId: token.actorId,
  };
}

/** Sustituye todo el estado por el servidor (carga inicial y reconexión). */
export function applySync(tokens: TokenDto[]): TokenState {
  const state: TokenState = {};
  for (const token of tokens) {
    state[token.id] = toView(token);
  }
  return state;
}

/** Aplica `TokenMoved` solo si `aggregateVersion` es estrictamente mayor. */
export function applyTokenMoved(state: TokenState, event: TokenMovedEvent): TokenState {
  const current = state[event.tokenId];
  if (current && current.version >= event.aggregateVersion) {
    return state;
  }
  return {
    ...state,
    [event.tokenId]: {
      id: event.tokenId,
      name: current?.name ?? event.tokenId,
      x: event.x,
      y: event.y,
      version: event.aggregateVersion,
      actorId: current?.actorId ?? null,
    },
  };
}

/** Añade o actualiza un token solo si trae una versión igual o más nueva. */
export function upsertToken(state: TokenState, token: TokenDto): TokenState {
  const current = state[token.id];
  if (current && current.version > token.version) {
    return state;
  }
  return {
    ...state,
    [token.id]: toView(token),
  };
}

/** Añade un token creado; si ya existe, conserva la versión más nueva. */
export function applyTokenCreated(state: TokenState, event: TokenCreatedEvent): TokenState {
  return upsertToken(state, event.token);
}

/**
 * Movimiento optimista local: registra la posición prevista con la versión
 * que se enviará al servidor; la confirmación real llega con `MoveTokenResult`.
 */
export function applyLocalMove(
  state: TokenState,
  tokenId: string,
  x: number,
  y: number,
  expectedVersion: number,
): TokenState {
  const current = state[tokenId];
  if (!current) {
    return state;
  }
  return {
    ...state,
    [tokenId]: { ...current, x, y, version: expectedVersion },
  };
}

/** Confirma el resultado del servidor (versión y coordenadas reales). */
export function applyMoveResult(state: TokenState, result: MoveTokenResult): TokenState {
  const current = state[result.tokenId];
  if (!current) {
    return state;
  }
  return {
    ...state,
    [result.tokenId]: { ...current, x: result.x, y: result.y, version: result.version },
  };
}
