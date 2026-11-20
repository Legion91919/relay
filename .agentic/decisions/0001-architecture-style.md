# 0001. Architecture style

- Status: Accepted
- Date: 2026-11-19

## Context

The network's customers rely on foreign messaging apps that don't work on zero-rated data, so customers with no airtime can't message at all. Launch traits: MillionsOfLiveConnections, PersonalData, SmallTeam. At global scale the advisor recommends Microservices.

## Decision

Pilot as a modular monolith (Identity, Conversations, Delivery, Presence) on ASP.NET Core SignalR, with a per-device delivery queue and client-generated message IDs for safe retries. Evolution trigger: Split Delivery and Presence into their own scaled services when one region passes ~200k concurrent connections, the point where the advisor's microservices answer (reference #14) pays off.

## Alternatives considered

- Microservices immediately (reference #14 at global scale): right for millions of users, wrong for a 20k-user pilot run by four engineers.
- Polling instead of live connections: simpler, but drains batteries and data on exactly the phones this is for.

## Consequences

- Module boundaries match the future services, so extraction is a deployment change, not a rewrite.
- One deployable and one database for the launch team to run.
