/**
 * Chat de campaña (Épica F): utilidad de estado para la lista en vivo y
 * sanidad del formulario. El servidor valida permisos y cuerpo (ADR-016);
 * aquí solo se evita duplicar mensajes (el propio envío también llega por
 * el hub) y se recorta la lista a los más recientes.
 */

import type { ChatMessageDto } from '../api/types';

/** Tope de sanidad en el cliente; el servidor valida lo mismo (ADR-016). */
export const MAX_MESSAGE_BODY_LENGTH = 2000;

/** Mensajes que conserva el cliente (el servidor manda 100 como máximo). */
export const MAX_VISIBLE_MESSAGES = 100;

/**
 * Añade un mensaje a la lista, que se guarda de más reciente a más
 * antiguo (como la respuesta del servidor). Ignora duplicados por id
 * (la respuesta del POST y el evento `MessagePosted` traen el mismo
 * mensaje) y recorta a los más recientes.
 */
export function appendMessage(
  current: ChatMessageDto[],
  message: ChatMessageDto,
  limit = MAX_VISIBLE_MESSAGES,
): ChatMessageDto[] {
  if (current.some((item) => item.id === message.id)) {
    return current;
  }

  return [message, ...current].slice(0, limit);
}

/**
 * Cuerpo de `POST /api/v1/campaigns/{id}/messages`. Devuelve `null` si
 * el texto queda vacío o supera los 2000 caracteres.
 */
export function toCreateMessageRequest(body: string): { body: string } | null {
  const trimmed = body.trim();
  if (trimmed.length < 1 || trimmed.length > MAX_MESSAGE_BODY_LENGTH) {
    return null;
  }
  return { body: trimmed };
}
