# Runbook

## Run locally
Prerequisites: .NET 8 SDK, Node 22.

```bash
# API on http://localhost:5103 (Development: seeds 4 invented users and their chats, ephemeral content key)
dotnet run --project src/Relay.Api            # uses Properties/launchSettings.json
# or, without the launch profile:
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5103 dotnet run --project src/Relay.Api --no-launch-profile

# Web on http://localhost:5173 (Vite proxies /api, /hubs (WebSockets) and /health to 5103)
cd web && npm install && npm run dev
```
Open two tabs and pick a different demo user in each (or use `http://localhost:5173/?as=u_thandi&chat=dm.u_sipho.u_thandi` and `?as=u_sipho&chat=dm.u_sipho.u_thandi`).

## Run tests
```bash
dotnet test                                    # CI (needs NuGet for xunit)
# Offline workspace (no NuGet): build each src project, then the xunit stand-in runner
dotnet build src/Relay.Api/Relay.Api.csproj --configfile /home/claude/tools/offline-nuget.config
/home/claude/tools/run-xunit-offline.sh tests/Relay.Tests src/Relay.Api/Relay.Api.csproj src/Modules/*/Relay.*.csproj
cd web && npx vitest run && npm run build
# End-to-end over the real hub (API must be running):
cd web && node scripts/smoke-signalr.mjs http://localhost:5103
```

## Configuration
| Key (env var) | Default | Notes |
|---|---|---|
| `Delivery__ContentKey` | none (ephemeral in Development) | base64 AES key, 16/24/32 bytes; the app refuses to start without it elsewhere |
| `Delivery__Retention` | `30.00:00:00` | undelivered bodies deleted after this |
| `Delivery__MaxBodyLength` | `2000` | characters |

## Deploy
Not deployed yet. Target: one container per region behind a load balancer with WebSockets enabled; add the SignalR Redis backplane before running more than one instance. Content key from the secret store.

## Monitor and alerts
- `GET /health/live` (process up), `GET /health/ready` (modules answer; database check arrives in slice 2).
- Metrics meter `Relay.Chat`: `relay.delivery.latency` p95 (SLO < 1 s), `relay.messages.accepted`, `relay.messages.duplicate_sends`.
- `GET /api/delivery/stats` (authenticated): bodies held and devices with pending messages (counts only).

## Common incidents
- **Ticks stuck on one tick:** recipient device offline or not acking; check `GET /api/delivery/queue` as that device and `/api/delivery/stats`.
- **Spike in duplicate sends:** clients retrying because replies are slow; check latency and connection churn.
- **Messages lost after restart:** expected in slice 1 (in-memory queues); phones keep their copies. Fixed by slice 2.
- **Hub connects but no pushes:** more than one instance without a backplane.
