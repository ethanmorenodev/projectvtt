/**
 * Mensajes privados 1:1 dentro de la campaña (Épica F, ADR-017): estado
 * `peerId → mensajes` (peer = el otro participante) y sanidad del
 * formulario. El servidor valida permisos, destinatario y cuerpo; aquí
 * solo se evita duplicar (la respuesta del POST y el evento traen el
 * mismo mensaje) y se recorta la lista a los más recientes.
 */

import type { DirectMessageDto } from '../api/types';

/** Tope de sanidad en el cliente; el servidor valida lo mismo (ADR-017). */
export const MAX_MESSAGE_BODY_LENGTH = 2000;

/** Mensajes que conserva el cliente (el servidor manda 100 como máximo). */
export const MAX_VISIBLE_MESSAGES = 100;

export type DirectMessagesByPeer = Record<string, DirectMessageDto[]>;

/** El otro participante de la conversación respecto a `currentUserId`. */
export function peerOf(message: DirectMessageDto, currentUserId: string): string {
  return message.fromUserId === currentUserId ? message.toUserId : message.fromUserId;
}

/** Sustituye la conversación de UN peer (respuesta de la carga inicial). */
export function applyDirectMessages(
  current: DirectMessagesByPeer,
  peerId: string,
  messages: DirectMessageDto[],
): DirectMessagesByPeer {
  return { ...current, [peerId]: messages };
}

/**
 * Añade un mensaje a su conversación (evento o respuesta de envío),
 * ignorando duplicados por id y recortando a los más recientes. La lista
 * de cada conversación se guarda de más reciente a más antiguo.
 */
export function appendDirectMessage(
  current: DirectMessagesByPeer,
  message: DirectMessageDto,
  currentUserId: string,
  limit = MAX_VISIBLE_MESSAGES,
): DirectMessagesByPeer {
  const peerId = peerOf(message, currentUserId);
  const list = current[peerId] ?? [];
  if (list.some((item) => item.id === message.id)) {
    return current;
  }
  return { ...current, [peerId]: [message, ...list].slice(0, limit) };
}

/**
 * Cuerpo de `POST /api/v1/campaigns/{id}/direct-messages`. Devuelve
 * `null` si falta el destinatario o el texto queda vacío o demasiado
 * largo.
 */
export function toCreateDirectMessageRequest(
  toUserId: string,
  body: string,
): { toUserId: string; body: string } | null {
  const trimmed = body.trim();
  if (!toUserId || trimmed.length < 1 || trimmed.length > MAX_MESSAGE_BODY_LENGTH) {
    return null;
  }
  return { toUserId, body: trimmed };
}
