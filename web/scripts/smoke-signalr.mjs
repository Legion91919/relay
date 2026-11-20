// End-to-end smoke test over the real hub: two demo users, offline queue drain, live send, ticks, idempotent retry.
// Usage: node scripts/smoke-signalr.mjs [http://localhost:5103]
import { randomUUID } from 'node:crypto';
import * as signalR from '@microsoft/signalr';

const base = process.argv[2] ?? 'http://localhost:5103';
const conv = 'dm.u_sipho.u_thandi';
let failures = 0;
const check = (ok, label) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${label}`); if (!ok) failures++; };

async function session(userId) {
  const res = await fetch(`${base}/api/identity/demo-sessions`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userId }) });
  if (!res.ok) throw new Error(`session for ${userId}: ${res.status}`);
  return res.json();
}

function connect(s) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${base}/hubs/chat`, { accessTokenFactory: () => s.token })
    .configureLogging(signalR.LogLevel.Warning)
    .build();
  const events = { messages: [], ticks: [], presence: [] };
  conn.on('MessageReceived', (e) => { events.messages.push(e); });
  conn.on('TickUpdated', (t) => { events.ticks.push(t); });
  conn.on('PresenceChanged', (p) => { events.presence.push(p); });
  return { conn, events };
}

const waitFor = async (pred, ms = 3000) => {
  const end = Date.now() + ms;
  while (Date.now() < end) { if (pred()) return true; await new Promise((r) => setTimeout(r, 25)); }
  return false;
};

const thandi = await session('u_thandi');
const sipho = await session('u_sipho');
const a = connect(thandi);
await a.conn.start();
check(a.conn.state === 'Connected', 'Thandi connected to /hubs/chat');

// 1. Sipho offline: Thandi sends two messages, they wait in Sipho's device queue.
const offlineIds = [randomUUID(), randomUUID()];
for (const [i, id] of offlineIds.entries()) {
  const r = await a.conn.invoke('Send', { conversationId: conv, clientMessageId: id, body: `while you were offline ${i + 1}` });
  check(r.ok && r.status === 'sent', `offline send ${i + 1} accepted (seq ${r.seq}, status ${r.status})`);
}

// 2. Sipho reconnects: drains the queue in order, acks, Thandi sees "delivered".
const b = connect(sipho);
await b.conn.start();
check(await waitFor(() => a.events.presence.some((p) => p.userId === 'u_sipho' && p.online)), 'Thandi sees Sipho come online');
const pending = await b.conn.invoke('SyncPending');
const ours = pending.filter((e) => offlineIds.includes(e.messageId)).map((e) => e.messageId);
const increasing = pending.every((e, i) => i === 0 || e.seq > pending[i - 1].seq);
check(ours.join() === offlineIds.join() && increasing, `Sipho drained ${pending.length} missed message(s) in order (seq ${pending.map((e) => e.seq).join(',')})`);
await b.conn.invoke('AckDelivered', pending.map((e) => e.messageId));
check(await waitFor(() => offlineIds.every((id) => a.events.ticks.some((t) => t.messageId === id && t.status === 'delivered'))), 'Thandi got delivered ticks for the missed messages');

// 3. Both online: live delivery, then delivered and read ticks.
const liveId = randomUUID();
const t0 = Date.now();
const sent = await a.conn.invoke('Send', { conversationId: conv, clientMessageId: liveId, body: 'Taxi rank at 5?' });
check(await waitFor(() => b.events.messages.some((m) => m.messageId === liveId)), `Sipho received the live message in ${Date.now() - t0} ms`);
await b.conn.invoke('AckDelivered', [liveId]);
check(await waitFor(() => a.events.ticks.some((t) => t.messageId === liveId && t.status === 'delivered')), 'Thandi sees delivered');
await b.conn.invoke('MarkRead', conv, sent.seq);
check(await waitFor(() => a.events.ticks.some((t) => t.messageId === liveId && t.status === 'read')), 'Thandi sees read after Sipho opens the chat');

// 4. Retry with the same client message ID: one message only, no second push.
const retry = await a.conn.invoke('Send', { conversationId: conv, clientMessageId: liveId, body: 'Taxi rank at 5?' });
await new Promise((r) => setTimeout(r, 300));
check(retry.ok && retry.duplicate && retry.seq === sent.seq, `retry is a duplicate (seq ${retry.seq}, status ${retry.status})`);
check(b.events.messages.filter((m) => m.messageId === liveId).length === 1, 'Sipho received the message exactly once');
const receipts = await (await fetch(`${base}/api/conversations/${conv}/receipts`, { headers: { Authorization: `Bearer ${thandi.token}` } })).json();
check(receipts.filter((r) => r.messageId === liveId).length === 1 && !('body' in receipts[0]), `receipts list has the message once and no body (${receipts.length} receipts)`);

// 5. Server forgot the bodies once delivered.
const stats = await (await fetch(`${base}/api/delivery/stats`, { headers: { Authorization: `Bearer ${thandi.token}` } })).json();
check(stats.messagesHeld === 0, `server holds ${stats.messagesHeld} message bodies after delivery`);

await b.conn.stop();
check(await waitFor(() => a.events.presence.some((p) => p.userId === 'u_sipho' && !p.online)), 'Thandi sees Sipho go offline (last seen)');
await a.conn.stop();
console.log(failures === 0 ? 'SMOKE OK' : `SMOKE FAILED (${failures})`);
process.exit(failures === 0 ? 0 : 1);
