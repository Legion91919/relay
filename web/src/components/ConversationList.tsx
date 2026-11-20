import type { Conversation, Presence } from '../lib/types';
import { presenceText } from './ChatHeader';

interface Props { conversations: Conversation[]; selectedId?: string; presence: Record<string, Presence>; status: 'loading' | 'ready' | 'error'; onSelect: (id: string) => void; onRetry: () => void }

export function ConversationList({ conversations, selectedId, presence, status, onSelect, onRetry }: Props) {
  if (status === 'loading') return <div className="convs" aria-busy="true"><span className="sr">Loading chats</span><div className="skel-row" /><div className="skel-row" /><div className="skel-row" /></div>;
  if (status === 'error') return <div className="convs"><p className="note">Can't reach Relay. <button className="retry" type="button" onClick={onRetry}>Try again</button></p></div>;
  if (conversations.length === 0) return <div className="convs"><p className="note">No chats yet.</p></div>;
  return (
    <ul className="convs" aria-label="Chats">
      {conversations.map((c) => (
        <li key={c.conversationId}>
          <button type="button" aria-current={c.conversationId === selectedId} onClick={() => onSelect(c.conversationId)}>
            <span className="avatar" aria-hidden="true">{c.partner.initials}</span>
            <span><span className="who">{c.partner.displayName}</span><br /><span className="sub">{presenceText(presence[c.partner.userId]) || 'offline'}</span></span>
          </button>
        </li>
      ))}
    </ul>
  );
}
