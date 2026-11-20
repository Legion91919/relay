export type TickStatus = 'waiting' | 'sent' | 'delivered' | 'read';
export type ConnectionState = 'online' | 'connecting' | 'reconnecting' | 'offline';

export interface UserSummary { userId: string; displayName: string; initials: string }
export interface DemoSession { token: string; userId: string; deviceId: string; displayName: string }
export interface Conversation { conversationId: string; partner: UserSummary; lastSeq: number; lastMessageAt: string | null }
export interface Presence { userId: string; online: boolean; lastSeenAt: string | null }

/** A message as the phone stores it. `seq` is assigned by the server; a queued message has none yet. */
export interface ChatMessage {
  messageId: string;
  conversationId: string;
  seq?: number;
  senderId: string;
  body: string;
  sentAt: string;
  status: TickStatus;
}

export interface Envelope { messageId: string; conversationId: string; seq: number; senderId: string; sentAt: string; body: string }
export interface TickUpdate { conversationId: string; messageId: string; status: 'sent' | 'delivered' | 'read'; at: string }
export interface SendResult { ok: boolean; error: string | null; messageId: string | null; seq: number | null; sentAt: string | null; status: 'sent' | 'delivered' | 'read' | null; duplicate: boolean }
