import type { TickStatus } from '../lib/types';

export const tickLabel: Record<TickStatus, string> = { waiting: 'Waiting to send', sent: 'Sent', delivered: 'Delivered', read: 'Read' };

export function Ticks({ status }: { status: TickStatus }) {
  const path =
    status === 'waiting' ? <><circle cx="9" cy="6" r="4.5" fill="none" stroke="currentColor" strokeWidth="1.5" /><path d="M9 3.5V6l1.8 1.2" fill="none" stroke="currentColor" strokeWidth="1.5" /></>
    : status === 'sent' ? <path d="M2 6.5l3.5 3.5L12 2" fill="none" stroke="currentColor" strokeWidth="2" />
    : <path d="M1 6.5l3.5 3.5L11 2M7 9l1 1L16 2" fill="none" stroke="currentColor" strokeWidth="2" />;
  return (
    <span className={`tick ${status === 'read' ? 'read' : ''}`} data-status={status}>
      <svg viewBox="0 0 18 12" aria-hidden="true">{path}</svg>
      <span className="sr">{tickLabel[status]}</span>
    </span>
  );
}
