# Handover

What the client needs to own this without us.

- [ ] Source, infrastructure-as-code and pipelines in the client's own accounts
- [ ] Every secret rotated and owned by the client
- [ ] Architecture and decisions explained (`architecture.md`, `decisions/`)
- [ ] Runbook rehearsed by a client engineer
- [ ] Known issues and next steps listed below
- [ ] AI agent setup documented so their team can keep using it

## Known issues
- Slice 1 keeps queues and receipts in memory: a restart loses undelivered messages (phones keep theirs). Slice 2 fixes this.
- Sign-in is a Development-only demo session; real SMS one-time PIN sign-in is not built.
- More than one API instance needs a SignalR backplane.
- OpenTelemetry export not wired (packages unavailable in the build environment); metrics are emitted on meter `Relay.Chat`.

## Recommended next steps
- Slice 2 persistence (see `next-step.md`), then real sign-in, then a receipts retention policy.