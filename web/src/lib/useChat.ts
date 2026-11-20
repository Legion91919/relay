import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { api } from './api';
import { applyTick, fromEnvelope, lastPartnerSeq, newMessageId, outbox, upsert, type MessageMap } from './chatStore';
import { loadMessages, saveMessages } from './storage';
import type { ConnectionState, Conversation, DemoSession, Envelope, Presence, SendResult, TickUpdate } from './types';

type LoadState = 'loading' | 'ready' | 'error';

/** Session, live connection, local message store and outbox for one demo user. */
export function useChat(userId: string | null) {
  const [session, setSession] = useState<DemoSession | null>(null);
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [load, setLoad] = useState<LoadState>('loading');
  const [connection, setConnection] = useState<ConnectionState>('connecting');
  const [messages, setMessages] = useState<MessageMap>({});
  const [presence, setPresence] = useState<Record<string, Presence>>({});
  const [attempt, setAttempt] = useState(0);
  const hub = useRef<HubConnection | null>(null);
  const store = useRef<MessageMap>({});

  const commit = useCallback((next: MessageMap) => {
    store.current = next;
    setMessages(next);
    if (userId) saveMessages(userId, next);
  }, [userId]);

  const receive = useCallback(async (envelopes: Envelope[]) => {
    if (envelopes.length === 0) return;
    let next = store.current;
    for (const e of envelopes) next = upsert(next, fromEnvelope(e));
    commit(next);
    await hub.current?.invoke('AckDelivered', envelopes.map((e) => e.messageId)).catch(() => undefined);
  }, [commit]);

  const flushOutbox = useCallback(async (me: string) => {
    for (const m of outbox(store.current, me)) {
      if (hub.current?.state !== HubConnectionState.Connected) return;
      try {
        const r = await hub.current.invoke<SendResult>('Send', { conversationId: m.conversationId, clientMessageId: m.messageId, body: m.body });
        if (r.ok && r.seq !== null && r.sentAt && r.status) {
          commit(upsert(store.current, { ...m, seq: r.seq, sentAt: r.sentAt, status: r.status }));
        }
      } catch {
        return; // Still offline: keep it waiting, retry with the same ID on reconnect.
      }
    }
  }, [commit]);

  const sync = useCallback(async (me: string) => {
    setConnection('online');
    const pending = await hub.current?.invoke<Envelope[]>('SyncPending').catch(() => []);
    await receive(pending ?? []);
    await flushOutbox(me);
  }, [receive, flushOutbox]);

  useEffect(() => {
    if (!userId) return;
    let cancelled = false;
    let retryTimer: number | undefined;
    setLoad('loading');
    setConnection('connecting');
    store.current = loadMessages(userId);
    setMessages(store.current);

    (async () => {
      try {
        const s = await api.startSession(userId);
        const convs = await api.conversations(s.token);
        if (cancelled) return;
        setSession(s);
        setConversations(convs);
        setLoad('ready');
        const pres = await Promise.all(convs.map((c) => api.presence(s.token, c.partner.userId).catch(() => null)));
        if (!cancelled) setPresence(Object.fromEntries(pres.filter((p): p is Presence => p !== null).map((p) => [p.userId, p])));

        const conn = new HubConnectionBuilder()
          .withUrl('/hubs/chat', { accessTokenFactory: () => s.token })
          .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (c) => Math.min(30_000, 1000 * 2 ** c.previousRetryCount) })
          .configureLogging(LogLevel.Warning)
          .build();
        hub.current = conn;
        conn.on('MessageReceived', (e: Envelope) => void receive([e]));
        conn.on('TickUpdated', (t: TickUpdate) => commit(applyTick(store.current, t.conversationId, t.messageId, t.status)));
        conn.on('PresenceChanged', (p: Presence) => setPresence((prev) => ({ ...prev, [p.userId]: p })));
        conn.onreconnecting(() => setConnection('reconnecting'));
        conn.onreconnected(() => void sync(s.userId));
        const start = async () => {
          if (cancelled) return;
          try {
            setConnection((c) => (c === 'offline' ? 'offline' : 'connecting'));
            await conn.start();
            await sync(s.userId);
          } catch {
            setConnection('offline');
            retryTimer = window.setTimeout(start, 5000);
          }
        };
        conn.onclose(() => { if (!cancelled) { setConnection('offline'); retryTimer = window.setTimeout(start, 3000); } });
        await start();
      } catch {
        if (!cancelled) { setLoad('error'); setConnection('offline'); }
      }
    })();

    return () => {
      cancelled = true;
      window.clearTimeout(retryTimer);
      void hub.current?.stop();
      hub.current = null;
      setSession(null);
      setConversations([]);
    };
  }, [userId, attempt, commit, receive, sync]);

  const send = useCallback(async (conversationId: string, body: string) => {
    if (!session) return;
    const m = { messageId: newMessageId(), conversationId, senderId: session.userId, body, sentAt: new Date().toISOString(), status: 'waiting' as const };
    commit(upsert(store.current, m));
    await flushOutbox(session.userId);
  }, [session, commit, flushOutbox]);

  const markRead = useCallback((conversationId: string) => {
    if (!session || hub.current?.state !== HubConnectionState.Connected) return;
    const upTo = lastPartnerSeq(store.current[conversationId], session.userId);
    if (upTo > 0) void hub.current.invoke('MarkRead', conversationId, upTo).catch(() => undefined);
  }, [session]);

  return { session, conversations, load, connection, messages, presence, send, markRead, retry: () => setAttempt((a) => a + 1) };
}
