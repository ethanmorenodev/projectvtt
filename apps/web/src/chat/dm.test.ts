import { describe, expect, it } from 'vitest';
import type { DirectMessageDto } from '../api/types';
import {
  appendDirectMessage,
  applyDirectMessages,
  MAX_MESSAGE_BODY_LENGTH,
  MAX_VISIBLE_MESSAGES,
  peerOf,
  toCreateDirectMessageRequest,
  type DirectMessagesByPeer,
} from './dm';

const ME = 'user-me';
const PEER = 'user-peer';

const messageFixture = (overrides: Partial<DirectMessageDto> = {}): DirectMessageDto => ({
  id: 'dm-1',
  campaignId: 'campaign-1',
  fromUserId: ME,
  fromUserName: 'demo_gm',
  toUserId: PEER,
  toUserName: 'demo_player',
  body: 'Hola',
  createdAt: '2026-11-09T00:00:00Z',
  ...overrides,
});

describe('estado de mensajes privados', () => {
  it('peerOf devuelve el otro participante en ambos sentidos', () => {
    expect(peerOf(messageFixture(), ME)).toBe(PEER);
    expect(peerOf(messageFixture({ fromUserId: PEER, toUserId: ME }), ME)).toBe(PEER);
  });

  it('applyDirectMessages sustituye solo la conversación del peer indicado', () => {
    const current: DirectMessagesByPeer = {
      [PEER]: [messageFixture()],
      'user-other': [messageFixture({ id: 'dm-2' })],
    };
    const next = applyDirectMessages(current, PEER, []);

    expect(next[PEER]).toHaveLength(0);
    expect(next['user-other']).toHaveLength(1);
    // Inmutable.
    expect(current[PEER]).toHaveLength(1);
  });

  it('appendDirectMessage coloca el mensaje nuevo primero y deduplica por id', () => {
    const first = appendDirectMessage({}, messageFixture(), ME);
    const same = appendDirectMessage(first, messageFixture(), ME);
    expect(same).toBe(first);

    const second = appendDirectMessage(
      first,
      messageFixture({ id: 'dm-2', fromUserId: PEER, toUserId: ME }),
      ME,
    );
    expect(second[PEER].map((item) => item.id)).toEqual(['dm-2', 'dm-1']);
  });

  it('appendDirectMessage recorta a los más recientes', () => {
    let state: DirectMessagesByPeer = {};
    for (let i = 0; i < MAX_VISIBLE_MESSAGES + 5; i++) {
      state = appendDirectMessage(state, messageFixture({ id: `dm-${i}` }), ME);
    }

    expect(state[PEER]).toHaveLength(MAX_VISIBLE_MESSAGES);
    expect(state[PEER][0]?.id).toBe(`dm-${MAX_VISIBLE_MESSAGES + 4}`);
  });
});

describe('peticiones de mensajes privados', () => {
  it('toCreateDirectMessageRequest recorta el texto', () => {
    expect(toCreateDirectMessageRequest(PEER, '  Susurro  ')).toEqual({
      toUserId: PEER,
      body: 'Susurro',
    });
  });

  it('toCreateDirectMessageRequest rechaza destinatario o texto inválidos', () => {
    expect(toCreateDirectMessageRequest('', 'Hola')).toBeNull();
    expect(toCreateDirectMessageRequest(PEER, '')).toBeNull();
    expect(toCreateDirectMessageRequest(PEER, '   ')).toBeNull();
    expect(
      toCreateDirectMessageRequest(PEER, 'x'.repeat(MAX_MESSAGE_BODY_LENGTH + 1)),
    ).toBeNull();
    expect(toCreateDirectMessageRequest(PEER, 'x'.repeat(MAX_MESSAGE_BODY_LENGTH))).toEqual({
      toUserId: PEER,
      body: 'x'.repeat(MAX_MESSAGE_BODY_LENGTH),
    });
  });
});
