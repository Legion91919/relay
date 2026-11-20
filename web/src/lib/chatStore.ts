import type { ChatMessage, Envelope, TickStatus } from './types';

/** All messages on this phone, per conversation, always sorted. Pure functions only: easy to test. */
export type MessageMap = Record<string, ChatMessage[]>;

const rank: Record<TickStatus, number> = { waiting: 0, sent: 1, delivered: 2, read: 3 };

/** Ticks only ever move forward, even if updates arrive out of order or twice. */
export function laterStatus(a: TickStatus, b: TickStatus): TickStatus {
  return rank[b] > rank[a] ? b : a;
}

/** Server order (seq) first; messages still waiting to send go last, in the order they were written. */
export function sortMessages(list: ChatMessage[]): ChatMessage[] {
  return [...list].sort((a, b) => {
    if (a.seq !== undefined && b.seq !== undefined) return a.seq - b.seq;
    if (a.seq !== undefined) return -1;
    if (b.seq !== undefined) return 1;
    return a.sentAt.localeCompare(b.sentAt);
  });
}

/** Adds or merges a message. The same ID never appears twice (live push + reconnect sync can both deliver it). */
export function upsert(map: MessageMap, msg: ChatMessage): MessageMap {
  const list = map[msg.conversationId] ?? [];
  const existing = list.find((m) => m.messageId === msg.messageId);
  const merged: ChatMessage = existing
    ? { ...existing, seq: existing.seq ?? msg.seq, sentAt: msg.seq !== undefined ? msg.sentAt : existing.sentAt, status: laterStatus(existing.status, msg.status) }
    : msg;
  const others = list.filter((m) => m.messageId !== msg.messageId);
  return { ...map, [msg.conversationId]: sortMessages([...others, merged]) };
}

export function fromEnvelope(e: Envelope): ChatMessage {
  return { messageId: e.messageId, conversationId: e.conversationId, seq: e.seq, senderId: e.senderId, body: e.body, sentAt: e.sentAt, status: 'delivered' };
}

export function applyTick(map: MessageMap, conversationId: string, messageId: string, status: TickStatus): MessageMap {
  const list = map[conversationId];
  if (!list?.some((m) => m.messageId === messageId)) return map;
  return { ...map, [conversationId]: list.map((m) => (m.messageId === messageId ? { ...m, status: laterStatus(m.status, status) } : m)) };
}

/** My messages the server has not confirmed yet, oldest first: re-sent with the same ID after reconnect. */
export function outbox(map: MessageMap, me: string): ChatMessage[] {
  return Object.values(map)
    .flat()
    .filter((m) => m.senderId === me && m.status === 'waiting')
    .sort((a, b) => a.sentAt.localeCompare(b.sentAt));
}

/** Highest seq of the partner's messages, to tell the server "read up to here". */
export function lastPartnerSeq(list: ChatMessage[] | undefined, me: string): number {
  return (list ?? []).reduce((max, m) => (m.senderId !== me && m.seq !== undefined && m.seq > max ? m.seq : max), 0);
}

/** UUID v4 for client message IDs; works on plain-HTTP origins where crypto.randomUUID is missing. */
export function newMessageId(rand: (bytes: Uint8Array) => Uint8Array = (b) => crypto.getRandomValues(b)): string {
  const b = rand(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = Array.from(b, (x) => x.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}
