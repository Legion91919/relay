# Decision: one-to-one chat screen

Reviewed at 390px (low-end phone) and 1440px (desktop browser, content capped at 720px). Scores 1 (poor) to 5 (great).

| Variant | Main job speed | Clarity (ticks, status) | Accessibility | Fits tone | Total |
|---|---|---|---|---|---|
| A: compact log (`screens/chat-a.html`) | 4 | 4 | 4 | 2 | 14 |
| B: bubbles (`screens/chat-b.html`) | 5 | 3 | 4 | 5 | 17 |
| C: delivery-first (`screens/chat-c.html`) | 2 | 5 | 5 | 3 | 15 |

- **A** is the lightest to render and fits the most messages on a small screen, and its text labels ("Delivered", "Read") are unambiguous. But it reads like a log file, not a conversation, and "who said what" depends on a narrow name column.
- **B** is the fastest to scan (left/right alignment answers "who" without reading) and feels warm. Weakness: tick icons alone are small and the "Connecting..." pill is easy to miss when the network really drops.
- **C** makes the network state and the outbox impossible to miss, which matters on patchy 2G, but cards and the big composer cost a lot of vertical space: only four messages fit at 390px.

## Pick: B, with A's text status and C's network banner
1. B's bubble layout and round send button as the base.
2. From A: the newest outgoing message also shows its status as text ("Read", "Delivered", "Sent", "Waiting") so status never depends on icon shape or colour alone. Each tick icon carries a screen-reader label.
3. From C: when the connection is *reconnecting* or *offline* the banner becomes a full-width strip with plain words ("You're offline. 1 message will send when you're back."). Unsent messages stay inline in the list with a clock icon, in order, instead of a separate tray (saves space).
4. System fonts, no images, inline SVG icons only: nothing extra to download on 2G.

Final: `screens/chat.html` (state switcher: loading, empty, error, success; connection: online, connecting, reconnecting, offline).
