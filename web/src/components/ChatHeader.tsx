import type { Presence, UserSummary } from '../lib/types';

const time = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' });

export function presenceText(p?: Presence): string {
  if (!p) return '';
  if (p.online) return 'online';
  return p.lastSeenAt ? `last seen ${time.format(new Date(p.lastSeenAt))}` : 'offline';
}

export function ChatHeader({ partner, presence, onBack }: { partner: UserSummary; presence?: Presence; onBack: () => void }) {
  return (
    <header className="chat-header">
      <button className="back" type="button" onClick={onBack} aria-label="Back to chats">&larr;</button>
      <div className="avatar" aria-hidden="true">{partner.initials}</div>
      <div>
        <div className="name">{partner.displayName}</div>
        <div className="presence">{presenceText(presence)}</div>
      </div>
    </header>
  );
}
