import { describe, expect, it } from 'vitest';
import { bannerText } from './ConnectionBanner';
import { presenceText } from './ChatHeader';

describe('connection banner', () => {
  it('tells the user unsent messages are safe when the network drops', () => {
    expect(bannerText('offline', 2)).toBe("You're offline. 2 messages will send when you're back.");
    expect(bannerText('reconnecting', 1)).toBe("Reconnecting... 1 message will send when you're back.");
    expect(bannerText('offline', 0)).toBe("You're offline. Relay keeps trying.");
    expect(bannerText('connecting', 0)).toBe('Connecting...');
  });
});

describe('presence text', () => {
  it('says online, last seen or offline', () => {
    expect(presenceText({ userId: 'u', online: true, lastSeenAt: null })).toBe('online');
    expect(presenceText({ userId: 'u', online: false, lastSeenAt: '2026-11-19T08:41:00Z' })).toMatch(/^last seen /);
    expect(presenceText({ userId: 'u', online: false, lastSeenAt: null })).toBe('offline');
    expect(presenceText(undefined)).toBe('');
  });
});
