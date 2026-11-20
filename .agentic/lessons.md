# Lessons

Generic, reusable lessons only. No client names, data or secrets.
- In a modular monolith, keep a thin application coordinator in the composition root as the only place that combines modules; modules stay reference-free and an architecture test can prove it with reflection plus a csproj scan.
- Client-generated UUIDs as message IDs give idempotent retries for free; pair them with at-least-once delivery and client-side de-duplication rather than chasing exactly-once.
- SignalR client handlers must not return values (an arrow function returning `push()` makes the client report "Result given ... server is not expecting a result").
