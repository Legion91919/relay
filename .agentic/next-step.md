# Next step

- **Doing:** nothing in progress; slice 1 is complete.
- **Done so far:** sketch leg (`ux/`), slice 1 (API + four modules + web chat UI), 91 .NET tests, 11 Vitest tests, SignalR smoke script, design dossier (`docs/DESIGN.md`).
- **Next action:** start slice 2 (Postgres via EF Core): write a failing integration test for `IDeliveryStore` against Postgres (Testcontainers), then add `Relay.Delivery` EF store with its own schema; same for `IConversationRepository`. Wrap Accept + Enqueue in one transaction.
- **Blocked by:** NuGet access (EF Core, Npgsql, Testcontainers, OpenTelemetry).
