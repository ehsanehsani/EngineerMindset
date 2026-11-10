# 07: Cheatsheet (read the night before)

> Status: **growing**. One page, updated after each phase.

## Push basics
- Polling (simple, wasteful) → long polling (held request) → SSE (one-way, auto-reconnect) → **WebSockets** (two-way, lowest overhead, **stateful**).
- Persistent connections mean memory per connection, sticky routing, dropped connections on deploy, and fan-out across servers.

## Long polling in 4 lines
- The server **holds** the request until there's news or a **timeout** (SignalR 90 s) → the client re-polls **immediately**.
- Close the **gap** between polls with a per-client buffer or a `since` cursor.
- Async I/O → a held request ≠ a blocked thread. Client→server goes over separate POSTs.
- Needs **sticky sessions** when scaled. Degrades to rapid polling under high message rates.

## WebSockets in 5 lines
- Starts as HTTP: `Upgrade: websocket` → **`101 Switching Protocols`**. The same TCP connection then carries frames.
- Frames: text / binary / ping / pong / close. 2–14 bytes of overhead. **No meaning, no ids, no acks.**
- Connections die silently → **heartbeats**. Proxies kill idle connections after ~60 s.
- Browsers **can't set headers** → token in the **query string**. Use `wss://` and check `Origin`.
- Stateful → sticky to one server, which makes deploys and scaling hard.

## On the wire
`negotiate` → WebSockets / SSE / long polling (fallback on failure) → `{"protocol":"json","version":1}` → messages `{"type":1,"target":"X","arguments":[...],"invocationId":"0"}␞`. `type 3` = completion, `6` = ping, `7` = close.

## SignalR in 10 lines
1. **Hub = transient**, one instance per call → no state in the hub.
2. Push from outside a hub → **`IHubContext<THub>`**.
3. **Connection** (per tab, new id on reconnect) ≠ **User** (many connections) ≠ **Group** (set of connections, not persisted, re-join after reconnect).
4. **Caller** = sender · **Group** = table/match · **User** = all of my devices · **All** = global.
5. Transports: **WebSockets → SSE → long polling**, chosen during **negotiate**.
6. `SendAsync` completing ≠ delivered. **No acks**: design for loss.
7. `HubException` → rejects the client's `invoke()`. Business rejections → push a message.
8. Heartbeats: keep-alive 15 s, timeout 30 s (timeout ≥ 2 × keep-alive).
9. `OnDisconnectedAsync` doesn't run if the process crashes → use TTLs for critical cleanup.
10. JS handlers must not return a value unless the server asked for a result.

## Scaling out (the 3 problems)
1. **Messages don't reach players on other servers** → **backplane** (Redis pub/sub). It moves messages, **not** state.
2. **Every server runs its own game** → **one leader** (a Redis lock that expires; another server takes over if the leader dies).
3. **The same money can be spent twice** → **shared state** in Redis/DB + **atomic** check-and-subtract (Lua script / conditional SQL) + **idempotent** bet ids.
- Plus **sticky sessions** on the load balancer. Or **Azure SignalR Service**, which handles connections for you, but not your state.
- Redis pub/sub **forgets** messages; Kafka **stores** them (use it for bets and payouts).

## Diagnostics under load
- **Mitigate first** (rollback, scale out, throttle), **then** diagnose, fix, prevent (alert + load test).
- **RED** for services (Rate, Errors, Duration); **USE** for resources (Utilization, **Saturation**, Errors).
- **p95/p99, never averages.**
- **CPU low + slow = waiting** (blocked threads, DB/pool, external API, locks). **CPU high = computing** (hot code, GC).
- Thread pool starvation: the queue grows, CPU is idle, threads stuck in `.Result`/`Sleep` → async all the way.
- Tools: **counters** (live numbers), **trace** (where time goes), **stack** (what threads do), **dump/gcdump** (memory, hangs).
- Common culprits: N+1 queries, pool exhaustion, `new HttpClient()` per request, memory leaks (static/unbounded caches), lock contention, slow dependencies (timeouts + circuit breaker).

## Casino patterns
- **Server-authoritative**: the server owns the clock, state, result and balance. The client is a view.
- Send **`endsAtUtc` + `serverNowUtc`**, never "seconds left". The client computes the offset.
- Accept a bet only if **phase is open AND now < deadline**, checked atomically with the balance.
- **Never trust the client**: amount > 0, valid target, balance, state, identity from the token.
- Close betting **before** the result. Use a **crypto RNG** on the server. **Audit-log** every money event.
- Message criticality: payouts/balance must be recoverable. Jackpot can be throttled or dropped.
