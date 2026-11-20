import type { ConnectionState } from '../lib/types';

export function bannerText(state: ConnectionState, waiting: number): string {
  const tail = waiting > 0 ? ` ${waiting} message${waiting === 1 ? '' : 's'} will send when you're back.` : '';
  switch (state) {
    case 'connecting': return 'Connecting...';
    case 'reconnecting': return `Reconnecting...${tail}`;
    case 'offline': return `You're offline.${tail || ' Relay keeps trying.'}`;
    default: return 'Connected';
  }
}

export function ConnectionBanner({ state, waitingCount }: { state: ConnectionState; waitingCount: number }) {
  const cls = state === 'online' ? 'sr' : state === 'connecting' ? 'banner pill' : 'banner strip';
  return <div className={cls} role="status" aria-live="polite">{bannerText(state, waitingCount)}</div>;
}
