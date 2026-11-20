import type { ChatMessage } from '../lib/types';
import { Ticks } from './Ticks';

const time = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' });

export function MessageBubble({ message, mine }: { message: ChatMessage; mine: boolean }) {
  return (
    <div className={`msg ${mine ? 'out' : 'in'}`}>
      {message.body}
      <div className="meta">
        <time dateTime={message.sentAt}>{time.format(new Date(message.sentAt))}</time>
        {mine && <Ticks status={message.status} />}
      </div>
    </div>
  );
}
