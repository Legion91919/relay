# User flows

## Flow 1: Two users chat in real time with ticks; a phone that reconnects gets what it missed (slice 1)
Screen: `screens/chat.html` (React: `web/src/App.tsx`). Live connection: SignalR hub `/hubs/chat` (WebSockets, falls back to long polling on poor networks).

1. **Pick a demo user** (AppBar switcher; two tabs can be two users). `GET /api/identity/users`, then `POST /api/identity/demo-sessions` `{ userId, deviceId }` returns a demo bearer token.
2. **Open the chat list**: `GET /api/conversations` (+ `GET /api/presence/{userId}` for each partner). State: loading → empty or success.
3. **Connect**: hub `/hubs/chat?access_token=...`. ConnectionBanner shows connecting → online. On every (re)connect the client calls hub `SyncPending()`, which returns this device's queued messages in order; the client stores them and calls `AckDelivered(ids)`.
4. **Send**: Composer adds the message locally as *waiting* with a client-generated `messageId` (UUID), then calls hub `Send({ conversationId, clientMessageId, body })`. Reply `{ messageId, seq, status: "sent" }` turns the clock into one tick. If the network drops, the message stays in the local outbox and is re-sent with the **same** ID on reconnect (server answers `duplicate: true`, no second message). REST fallback: `POST /api/conversations/{id}/messages`.
5. **Receive**: the partner gets hub event `MessageReceived(envelope)` live (or via `SyncPending` after reconnect), stores it and calls `AckDelivered([messageId])`. The sender gets `TickUpdated { status: "delivered" }` → two ticks. The server deletes the message body once every recipient device has acknowledged it.
6. **Read**: when the partner has the chat open and visible, the client calls `MarkRead(conversationId, upToSeq)`. The sender gets `TickUpdated { status: "read" }` → amber ticks + "Read".
7. **Reconcile** (optional): `GET /api/conversations/{id}/receipts?afterSeq=n` returns receipts metadata (no message bodies) to repair ticks after a long time offline.
8. **Presence**: hub event `PresenceChanged { userId, online, lastSeenAt }` updates ChatHeader.

Error and edge states: ConnectionBanner (reconnecting/offline), MessageList error with retry, Composer disabled until a session exists.
