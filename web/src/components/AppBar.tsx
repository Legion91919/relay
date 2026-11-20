import type { UserSummary } from '../lib/types';

export function Logo() {
  return (
    <svg width="28" height="28" viewBox="0 0 32 32" aria-hidden="true">
      <path d="M4 6a4 4 0 0 1 4-4h16a4 4 0 0 1 4 4v13a4 4 0 0 1-4 4H13l-6 6v-6H8a4 4 0 0 1-4-4z" fill="var(--color-accent)" />
      <path d="M12 18v-8h2.4v1.4c.7-1.1 1.8-1.6 3.4-1.6v2.5c-2 0-3.2.8-3.2 2.6V18z" fill="var(--color-on-accent)" />
    </svg>
  );
}

export function AppBar({ users, currentUserId, onSwitchUser }: { users: UserSummary[]; currentUserId: string | null; onSwitchUser: (id: string) => void }) {
  return (
    <div className="appbar">
      <span className="brand"><Logo />Relay</span>
      <label htmlFor="who">Demo user</label>
      <select id="who" value={currentUserId ?? ''} disabled={users.length === 0} onChange={(e) => onSwitchUser(e.target.value)}>
        {users.length === 0 && <option value="">Loading people...</option>}
        {currentUserId === null && users.length > 0 && <option value="">Pick a person</option>}
        {users.map((u) => <option key={u.userId} value={u.userId}>{u.displayName}</option>)}
      </select>
    </div>
  );
}
