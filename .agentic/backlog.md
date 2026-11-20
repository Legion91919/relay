# Backlog

Vertical slices, smallest valuable first.

## Now
- [x] Sketch leg: tokens + three variants of the slice-1 screen (`sketch-ui`): `ux/decision.md`, final `ux/screens/chat.html`
- [x] Slice 1: Two users exchange messages in real time with sent, delivered and read ticks, and a phone that reconnects receives what it missed
  - [x] Given both users are online, when A sends, then B receives it live and A sees 'delivered'
  - [x] Given B is offline, when B reconnects, then B receives missed messages in order
  - [x] Given B opens the chat, then A sees 'read'
  - [x] Given a client retries a send with the same message ID, then only one message exists
  - Test notes: one test per criterion; seeded invented data. Tests: `tests/Relay.Tests/Chat/Slice1AcceptanceTests.cs`, smoke `web/scripts/smoke-signalr.mjs`.

## Next
- [ ] Slice 2: persistence (Postgres via EF Core) behind the same repository interfaces
- [ ] CI green with all gates

## Later
- [ ] Real sign-in: SMS one-time PIN via the network's SMS gateway (replaces demo sessions)
- [ ] OpenTelemetry exporter (traces + `Relay.Chat` metrics) once packages can be restored
- [ ] Retention for receipts metadata (proposal: 90 days)
- [ ] Rate limit sends per device
- [ ] Playwright journey test for flow 1 with axe
- [ ] Group chats (tick model already takes the lowest status across recipients)
