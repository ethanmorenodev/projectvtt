import { useEffect, useRef, useState, type FormEvent } from 'react';
import { apiFetch } from '../api/client';
import './panels.css';
import type {
  CampaignMember,
  ChatMessageDto,
  DirectMessageDto,
} from '../api/types';
import { MAX_MESSAGE_BODY_LENGTH, toCreateMessageRequest } from '../chat/chat';
import { toCreateDirectMessageRequest } from '../chat/dm';

interface Props {
  campaignId: string;
  currentUserId: string;
  /** Mensajes de campaña, de más reciente a más antiguo. */
  messages: ChatMessageDto[];
  /** Miembros de la campaña (para elegir destinatario privado). */
  members: CampaignMember[];
  /** Conversación seleccionada: `null` = canal de campaña. */
  dmTarget: string | null;
  /** Conversación con `dmTarget`, de más reciente a más antiguo. */
  conversation: DirectMessageDto[];
  onSelectTarget: (userId: string | null) => void;
  onMessagePosted: (message: ChatMessageDto) => void;
  onDirectMessagePosted: (message: DirectMessageDto) => void;
  onError: (message: string) => void;
}

/**
 * Chat de campaña (Épica F): segundo aside con el historial en orden
 * cronológico (lo más reciente abajo) y el formulario de envío. El
 * selector cambia entre el canal de campaña y las conversaciones privadas
 * 1:1 con otro miembro (ADR-017). Los mensajes llegan por HTTP al cargar
 * y en vivo por `MessagePosted` / `DirectMessagePosted`; el remitente
 * también recibe su propio evento, y el deduplicado por id evita el doble
 * insert.
 */
export function ChatPanel({
  campaignId,
  currentUserId,
  messages,
  members,
  dmTarget,
  conversation,
  onSelectTarget,
  onMessagePosted,
  onDirectMessagePosted,
  onError,
}: Props) {
  const [draft, setDraft] = useState('');
  const sheetRef = useRef<HTMLElement>(null);

  // La vista sigue al final: los mensajes nuevos aparecen abajo.
  useEffect(() => {
    const sheet = sheetRef.current;
    if (sheet) {
      sheet.scrollTop = sheet.scrollHeight;
    }
  }, [messages, conversation]);

  const send = async (event: FormEvent) => {
    event.preventDefault();
    const request = dmTarget
      ? toCreateDirectMessageRequest(dmTarget, draft)
      : toCreateMessageRequest(draft);
    if (!request) {
      return;
    }

    try {
      if (dmTarget) {
        const created = await apiFetch<DirectMessageDto>(
          `/api/v1/campaigns/${campaignId}/direct-messages`,
          { method: 'POST', body: request },
        );
        onDirectMessagePosted(created);
      } else {
        const created = await apiFetch<ChatMessageDto>(
          `/api/v1/campaigns/${campaignId}/messages`,
          { method: 'POST', body: request },
        );
        onMessagePosted(created);
      }
      setDraft('');
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : 'No se pudo enviar el mensaje.');
    }
  };

  const others = members.filter((member) => member.userId !== currentUserId);

  // Ambas listas están de más reciente a más antiguo; se invierten para
  // mostrarlas en orden cronológico.
  const chronological = [...(dmTarget ? conversation : messages)].reverse();
  const targetName =
    dmTarget === null
      ? 'campaña'
      : (others.find((member) => member.userId === dmTarget)?.userName ?? 'conversación');

  return (
    <aside className="scene-sheet" ref={sheetRef}>
      <h3>Chat</h3>
      <label className="inline-form">
        Conversación
        <select
          value={dmTarget ?? ''}
          onChange={(event) => onSelectTarget(event.target.value || null)}
          aria-label="Conversación"
        >
          <option value="">Campaña (todos)</option>
          {others.map((member) => (
            <option key={member.userId} value={member.userId}>
              {member.userName} (privado)
            </option>
          ))}
        </select>
      </label>
      {chronological.length === 0 ? (
        <p className="muted">
          {dmTarget ? `Aún no hay mensajes con ${targetName}.` : 'Nadie ha escrito todavía.'}
        </p>
      ) : (
        <ul className="roll-list">
          {chronological.map((message) => {
            const isDirect = 'fromUserId' in message;
            const author = isDirect
              ? (message as DirectMessageDto).fromUserName
              : (message as ChatMessageDto).userName;
            return (
              <li key={message.id}>
                <strong>{author}</strong>
                <span>{message.body}</span>
                <span className="muted">
                  {new Date(message.createdAt).toLocaleTimeString([], {
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </span>
              </li>
            );
          })}
        </ul>
      )}
      <form onSubmit={(event) => void send(event)} className="sheet-form">
        <input
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          maxLength={MAX_MESSAGE_BODY_LENGTH}
          aria-label={dmTarget ? `Mensaje privado a ${targetName}` : 'Mensaje'}
        />
        <button type="submit" disabled={!draft.trim()}>
          Enviar
        </button>
      </form>
    </aside>
  );
}
