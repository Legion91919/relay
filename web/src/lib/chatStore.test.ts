import { describe, expect, it } from 'vitest';
import { applyTick, fromEnvelope, lastPartnerSeq, laterStatus, newMessageId, outbox, upsert, type MessageMap } from './chatStore';
import type { ChatMessage } from './types';

const conv = 'dm.u_sipho.u_thandi';
const msg = (over: Partial<ChatMessage>): ChatMessage => ({
  messageId: 'id-1', conversationId: conv, senderId: 'u_thandi', body: 'hi', sentAt: '2026-11-19T08:00:00Z', status: 'waiting', ...over,
});

describe('ticks', () => {
  it('only move forward', () => {
    expect(laterStatus('sent', 'delivered')).toBe('delivered');
    expect(laterStatus('read', 'delivered')).toBe('read');
    expect(laterStatus('waiting', 'read')).toBe('read');
  });

  it('a late delivered update cannot undo read', () => {
    let map: MessageMap = upsert({}, msg({ seq: 1, status: 'sent' }));
    map = applyTick(map, conv, 'id-1', 'read');
    map = applyTick(map, conv, 'id-1', 'delivered');
    expect(map[conv][0].status).toBe('read');
  });

  it('ignores ticks for messages this phone does not have', () => {
    const map = upsert({}, msg({ seq: 1 }));
    expect(applyTick(map, conv, 'other', 'read')).toBe(map);
  });
});

describe('message list', () => {
  it('orders by server sequence and keeps waiting messages last', () => {
    let map: MessageMap = {};
    map = upsert(map, msg({ messageId: 'w', sentAt: '2026-11-19T07:00:00Z' }));
    map = upsert(map, msg({ messageId: 'b', seq: 2, status: 'sent' }));
    map = upsert(map, msg({ messageId: 'a', seq: 1, status: 'sent' }));
    expect(map[conv].map((m) => m.messageId)).toEqual(['a', 'b', 'w']);
  });

  it('never shows the same message twice (live push then reconnect sync)', () => {
    const env = { messageId: 'x', conversationId: conv, seq: 3, senderId: 'u_sipho', sentAt: '2026-11-19T08:01:00Z', body: 'yo' };
    const map = upsert(upsert({}, fromEnvelope(env)), fromEnvelope(env));
    expect(map[conv]).toHaveLength(1);
  });

  it('a send confirmation fills in seq without losing a newer tick', () => {
    let map = upsert({}, msg({ status: 'waiting' }));
    map = applyTick(upsert(map, msg({ status: 'delivered' })), conv, 'id-1', 'delivered');
    map = upsert(map, msg({ seq: 7, status: 'sent' }));
    expect(map[conv][0]).toMatchObject({ seq: 7, status: 'delivered' });
  });
});

describe('outbox and read marker', () => {
  it('lists only my unsent messages, oldest first', () => {
    let map: MessageMap = {};
    map = upsert(map, msg({ messageId: '2', sentAt: '2026-11-19T08:02:00Z' }));
    map = upsert(map, msg({ messageId: '1', sentAt: '2026-11-19T08:01:00Z' }));
    map = upsert(map, msg({ messageId: '3', seq: 1, status: 'sent' }));
    map = upsert(map, msg({ messageId: '4', senderId: 'u_sipho', seq: 2, status: 'delivered' }));
    expect(outbox(map, 'u_thandi').map((m) => m.messageId)).toEqual(['1', '2']);
  });

  it('reads up to the highest partner sequence', () => {
    const list = [msg({ seq: 1, senderId: 'u_sipho' }), msg({ seq: 2 }), msg({ seq: 3, senderId: 'u_sipho' }), msg({ seq: 4 })];
    expect(lastPartnerSeq(list, 'u_thandi')).toBe(3);
    expect(lastPartnerSeq(undefined, 'u_thandi')).toBe(0);
  });
});

describe('client message IDs', () => {
  it('are RFC 4122 v4 UUIDs the server accepts', () => {
    const id = newMessageId();
    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(newMessageId()).not.toBe(id);
  });
});
