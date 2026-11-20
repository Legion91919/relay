import type { Conversation, DemoSession, Presence, UserSummary } from './types';

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let detail = `${res.status}`;
    try { detail = ((await res.json()) as { title?: string }).title ?? detail; } catch { /* not JSON */ }
    throw new Error(detail);
  }
  return (await res.json()) as T;
}

const auth = (token: string) => ({ Authorization: `Bearer ${token}` });

export const api = {
  users: () => fetch('/api/identity/users').then((r) => json<UserSummary[]>(r)),
  startSession: (userId: string) =>
    fetch('/api/identity/demo-sessions', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userId }) }).then((r) => json<DemoSession>(r)),
  conversations: (token: string) => fetch('/api/conversations', { headers: auth(token) }).then((r) => json<Conversation[]>(r)),
  presence: (token: string, userId: string) => fetch(`/api/presence/${encodeURIComponent(userId)}`, { headers: auth(token) }).then((r) => json<Presence>(r)),
};
