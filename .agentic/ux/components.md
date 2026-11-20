# Components (chat screen)

Built in this order in `web/src/components/`. All colours, sizes and spacing come from `design-tokens.json` as CSS variables (`web/src/tokens.css`).

| Component | Props | Loading | Empty | Error | Success |
|---|---|---|---|---|---|
| `AppBar` | `users: UserSummary[]`, `currentUserId?`, `onSwitchUser(id)` | switcher disabled, "Loading people..." | "No demo users" text | "Can't reach Relay" + retry | logo mark + user switcher |
| `ConversationList` | `conversations`, `selectedId?`, `presence`, `onSelect(id)` | 3 grey rows | "No chats yet" | error line + retry | rows: partner name, online dot + "online"/"last seen" text |
| `ChatHeader` | `partner: UserSummary`, `presence?: PresenceStatus` | name only | n/a | n/a | avatar initials, name, "online" or "last seen hh:mm" |
| `ConnectionBanner` | `state: 'online'\|'connecting'\|'reconnecting'\|'offline'`, `waitingCount: number` | pill "Connecting..." | hidden when online (still announced to screen readers) | full-width strip "You're offline. N messages will send when you're back." | hidden |
| `MessageList` | `messages: ChatMessage[]`, `myUserId`, `status: 'loading'\|'ready'\|'error'`, `partnerName`, `onRetry()` | skeleton bubbles + sr text "Loading messages" | "Say hi to {partner}" + note that messages live on the phone | "Couldn't open this chat" + Try again | bubbles in `seq` order, day label, text status under newest outgoing message |
| `MessageBubble` | `message: ChatMessage`, `mine: boolean` | n/a | n/a | n/a | body, time, `Ticks` for mine |
| `Ticks` | `status: 'waiting'\|'sent'\|'delivered'\|'read'` | clock icon "Waiting to send" | n/a | n/a | one tick (Sent), two ticks (Delivered), two amber ticks (Read); every icon has an sr-only label |
| `Composer` | `disabled: boolean`, `onSend(body)`, `maxLength=2000` | disabled | send disabled while input is blank | keeps text if send throws | clears after queuing; Enter sends, Shift+Enter new line |

## Data shapes (web)
- `ChatMessage { messageId, conversationId, seq?, senderId, body, sentAt, status }`: `seq` is assigned by the server; a queued message has no `seq` yet and sorts last.
- Messages are stored on the device (`localStorage`, per user); the server only keeps receipts metadata once a message is delivered.
