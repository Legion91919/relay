import type { MessageMap } from './chatStore';

// Messages live on the phone (the server deletes bodies once delivered). One key per user so two tabs can be two users.
const key = (userId: string) => `relay:v1:${userId}:messages`;

export function loadMessages(userId: string): MessageMap {
  try {
    return JSON.parse(localStorage.getItem(key(userId)) ?? '{}') as MessageMap;
  } catch {
    return {};
  }
}

export function saveMessages(userId: string, map: MessageMap): void {
  try { localStorage.setItem(key(userId), JSON.stringify(map)); } catch { /* storage full: keep in memory */ }
}

// The signed-in demo user is per tab.
export const tabUser = {
  get: () => sessionStorage.getItem('relay:user'),
  set: (userId: string) => sessionStorage.setItem('relay:user', userId),
};
