import { useState, type FormEvent, type KeyboardEvent } from 'react';

export function Composer({ disabled, onSend, maxLength = 2000 }: { disabled: boolean; onSend: (body: string) => void; maxLength?: number }) {
  const [text, setText] = useState('');
  const submit = (e?: FormEvent) => {
    e?.preventDefault();
    if (disabled || text.trim() === '') return;
    onSend(text);
    setText('');
  };
  const onKey = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) submit();
    if (e.key === 'Enter' && !e.shiftKey) e.preventDefault();
  };
  return (
    <form className="composer" onSubmit={submit}>
      <label htmlFor="composer" className="sr">Message</label>
      <textarea id="composer" rows={1} placeholder="Message" value={text} maxLength={maxLength} disabled={disabled}
        onChange={(e) => setText(e.target.value)} onKeyDown={onKey} />
      <button className="send" type="submit" aria-label="Send" disabled={disabled || text.trim() === ''}>
        <svg width="22" height="22" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 20l18-8L3 4v6l12 2-12 2z" fill="currentColor" /></svg>
      </button>
    </form>
  );
}
