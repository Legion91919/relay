# Quality

## Definition of done
- Acceptance criteria met and demonstrated (one test per criterion, plus the SignalR smoke script).
- Unit tests for rules, integration tests for each adapter, one end-to-end test per key flow.
- CI green: build (warnings as errors), tests, format, web (Vitest + build), dependency and secret scan, privacy guard.
- Docs in `.agentic/` and `docs/DESIGN.md` updated in the same change.

## Service levels
| SLI | SLO |
|---|---|
| Delivery latency, send to first device ack, both online (`relay.delivery.latency`) | p95 < 1 s |
| Send success rate (hub + REST, excluding 4xx) | 99.9% over 30 days |
| Hub connection success rate | 99.5% over 30 days |
| Oldest held body for a connected device | < 60 s |
| p95 REST latency | < 300 ms |
| Messages lost after acceptance (within retention) | 0 |

## Privacy checks
- No message content in logs: `MessageContentNeverReachesTheLogs` test.
- Receipts carry no content: `ReceiptsCarryNoMessageContent` test.
- Bodies encrypted at rest and deleted on last ack: `BodiesAreEncryptedAtRest`, `BodyIsDeletedOnlyWhenEveryRecipientDeviceHasAcked`.

## Current numbers
- .NET: 91 tests passing (offline runner). Web: 11 Vitest tests. Smoke: 14/14 checks over the real hub.
