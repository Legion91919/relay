import { useEffect, useRef } from 'react';
import type { ChatMessage } from '../lib/types';
import { MessageBubble } from './MessageBubble';
import { tickLabel } from './Ticks';

interface Props { messages: ChatMessage[]; myUserId: string; status: 'loading' | 'ready' | 'error'; partnerName: string; onRetry: () => void }

export function MessageList({ messages, myUserId, status, partnerName, onRetry }: Props) {
  const end = useRef<HTMLDivElement>(null);
  useEffect(() => {
    end.current?.scrollIntoView({ block: 'end' });
  }, [messages.length]);

  if (status === 'loading') {
    return (
      <main className="list" aria-busy="true">
        <span className="sr">Loading messages</span>
        {[60, 40, 70, 50].map((w, i) => <div key={i} className="skel" style={{ width: `${w}%`, alignSelf: i % 2 ? 'flex-end' : 'flex-start' }} />)}
      </main>
    );
  }
  if (status === 'error') {
    return (
      <main className="centre error">
        <div>
          <h2>Couldn't open this chat</h2>
          <p>Check your signal, then try again. Your messages are safe on this phone.</p>
          <button className="retry" type="button" onClick={onRetry}>Try again</button>
        </div>
      </main>
    );
  }
  if (messages.length === 0) {
    return (
      <main className="centre">
        <div>
          <h2>Say hi to {partnerName}</h2>
          <p>Messages live on your phone. Relay deletes them from its servers once they are delivered.</p>
        </div>
      </main>
    );
  }
  const lastMine = [...messages].reverse().find((m) => m.senderId === myUserId);
  const newestIsMine = messages[messages.length - 1]?.senderId === myUserId;
  return (
    <main className="list" aria-label={`Messages with ${partnerName}`} aria-live="polite">
      {messages.map((m) => <MessageBubble key={m.messageId} message={m} mine={m.senderId === myUserId} />)}
      {newestIsMine && lastMine && <div className="status-text" data-testid="last-status">{tickLabel[lastMine.status]}</div>}
      <div ref={end} />
    </main>
  );
}
