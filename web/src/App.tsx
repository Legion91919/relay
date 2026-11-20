import { useEffect, useMemo, useState } from 'react';
import { AppBar } from './components/AppBar';
import { ChatHeader } from './components/ChatHeader';
import { Composer } from './components/Composer';
import { ConnectionBanner } from './components/ConnectionBanner';
import { ConversationList } from './components/ConversationList';
import { MessageList } from './components/MessageList';
import { api } from './lib/api';
import { outbox } from './lib/chatStore';
import { tabUser } from './lib/storage';
import type { UserSummary } from './lib/types';
import { useChat } from './lib/useChat';

export function App() {
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [usersError, setUsersError] = useState(false);
  // ?as=u_sipho&chat=dm.u_sipho.u_thandi opens a tab straight into a user and chat (handy for two-tab demos).
  const params = new URLSearchParams(window.location.search);
  const [userId, setUserId] = useState<string | null>(params.get('as') ?? tabUser.get());
  const [selected, setSelected] = useState<string | undefined>(params.get('chat') ?? undefined);
  const chat = useChat(userId);

  useEffect(() => {
    api.users().then(setUsers).catch(() => setUsersError(true));
  }, []);

  const switchUser = (id: string) => { tabUser.set(id); setSelected(undefined); setUserId(id); };
  const conversation = chat.conversations.find((c) => c.conversationId === selected);
  const list = useMemo(() => (selected ? chat.messages[selected] ?? [] : []), [chat.messages, selected]);
  const waiting = userId ? outbox(chat.messages, userId).length : 0;

  // Opening (and looking at) a chat tells the sender "read".
  useEffect(() => {
    if (!selected || chat.connection !== 'online') return;
    const mark = () => { if (document.visibilityState === 'visible') chat.markRead(selected); };
    mark();
    document.addEventListener('visibilitychange', mark);
    return () => document.removeEventListener('visibilitychange', mark);
  }, [selected, list, chat.connection, chat.markRead]);

  return (
    <div className="shell">
      <AppBar users={users} currentUserId={userId} onSwitchUser={switchUser} />
      {usersError && <p className="note" role="alert">Can't reach Relay. Is the API running on port 5103?</p>}
      {!userId ? (
        <p className="note">Pick a demo user above. Open a second tab as someone else to chat between them.</p>
      ) : (
        <div className={`body ${conversation ? 'has-chat' : ''}`}>
          <ConversationList conversations={chat.conversations} selectedId={selected} presence={chat.presence} status={chat.load} onSelect={setSelected} onRetry={chat.retry} />
          <section className="chat" aria-label="Chat">
            {conversation ? (
              <>
                <ChatHeader partner={conversation.partner} presence={chat.presence[conversation.partner.userId]} onBack={() => setSelected(undefined)} />
                <ConnectionBanner state={chat.connection} waitingCount={waiting} />
                <MessageList messages={list} myUserId={userId} status={chat.load} partnerName={conversation.partner.displayName.split(' ')[0]} onRetry={chat.retry} />
                <Composer disabled={chat.load !== 'ready'} onSend={(body) => void chat.send(conversation.conversationId, body)} />
              </>
            ) : (
              <div className="centre"><div><ConnectionBanner state={chat.connection} waitingCount={waiting} /><h2>Pick a chat</h2></div></div>
            )}
          </section>
        </div>
      )}
    </div>
  );
}
