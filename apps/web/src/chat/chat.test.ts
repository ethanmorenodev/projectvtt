import { describe, expect, it } from 'vitest';
import type { ChatMessageDto } from '../api/types';
import {
  appendMessage,
  MAX_MESSAGE_BODY_LENGTH,
  MAX_VISIBLE_MESSAGES,
  toCreateMessageRequest,
} from './chat';

const messageFixture = (overrides: Partial<ChatMessageDto> = {}): ChatMessageDto => ({
  id: 'message-1',
  campaignId: 'campaign-1',
  userId: 'user-1',
  userName: 'demo_gm',
  body: 'Hola',
  createdAt: '2026-11-08T00:00:00Z',
  ...overrides,
});

describe('estado del chat', () => {
  it('appendMessage coloca el mensaje nuevo el primero (más reciente arriba)', () => {
    const first = appendMessage([], messageFixture());
    const second = appendMessage(
      first,
      messageFixture({ id: 'message-2', body: 'Segundo' }),
    );

    expect(second.map((item) => item.id)).toEqual(['message-2', 'message-1']);
    // Inmutable: el estado anterior sigue intacto.
    expect(first).toHaveLength(1);
  });

  it('appendMessage ignora duplicados por id (POST y evento MessagePosted)', () => {
    const first = appendMessage([], messageFixture());
    const same = appendMessage(first, messageFixture());

    expect(same).toBe(first);
  });

  it('appendMessage recorta la lista a los más recientes', () => {
    let list: ChatMessageDto[] = [];
    for (let i = 0; i < MAX_VISIBLE_MESSAGES + 5; i++) {
      list = appendMessage(list, messageFixture({ id: `message-${i}` }));
    }

    expect(list).toHaveLength(MAX_VISIBLE_MESSAGES);
    // El más reciente sobrevive al recorte.
    expect(list[0]?.id).toBe(`message-${MAX_VISIBLE_MESSAGES + 4}`);
  });
});

describe('peticiones de mensajes', () => {
  it('toCreateMessageRequest recorta el texto', () => {
    expect(toCreateMessageRequest('  Hola mesa  ')).toEqual({ body: 'Hola mesa' });
  });

  it('toCreateMessageRequest rechaza textos vacíos o demasiado largos', () => {
    expect(toCreateMessageRequest('')).toBeNull();
    expect(toCreateMessageRequest('   ')).toBeNull();
    expect(toCreateMessageRequest('x'.repeat(MAX_MESSAGE_BODY_LENGTH + 1))).toBeNull();
    expect(toCreateMessageRequest('x'.repeat(MAX_MESSAGE_BODY_LENGTH))).toEqual({
      body: 'x'.repeat(MAX_MESSAGE_BODY_LENGTH),
    });
  });
});
