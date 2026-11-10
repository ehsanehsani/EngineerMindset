# 06: Interview questions

> Status: **growing**. Phase 1 questions are below. The final version has 30+ questions and 3 full system design walkthroughs.

## SignalR basics

**1. What is a Hub and what is its lifetime?**
A class whose public methods clients can call, and through which the server pushes to clients. It is **transient**: one instance per invocation, then disposed. So it must not hold state. Keep state in singletons, databases or Redis.
*Follow-up:* "Can I start a `Task.Run` in a hub method that uses `Clients.Caller` later?" No. The hub and its context are disposed. Use `IHubContext` and the connection id.

**2. How do you push to clients from a background service?**
Inject `IHubContext<THub>`. It's a singleton with `Clients` and `Groups`. You can't use a hub instance because SignalR only creates one per incoming call, and there is no caller.

**3. Connection vs user vs group?**
Connection = one transport from one tab, and its id changes on reconnect. User = authenticated identity, which can own many connections (`Clients.User`). Group = a named set of connections that you manage yourself. It isn't persisted, can't be enumerated, and the client must re-join after reconnecting.
*Pitfall:* saying groups contain users. They contain **connections**.

**4. When would you use `Clients.Caller`, `Group`, `User`, `All`?**
Caller: response-like messages for the sender (validation errors, the join snapshot). Group: shared context (a table, a match). User: private data that must reach all of one person's devices (balance). All: truly global data (jackpot, maintenance notice). Choose by **privacy** first, then **fan-out cost**.

**5. Which transports does SignalR use, and how does it choose?**
WebSockets, then Server-Sent Events, then long polling. A `negotiate` POST returns the available transports and a connection token. The client tries them in order. Forcing WebSockets with `skipNegotiation` removes the negotiate round trip and the need for sticky sessions.

**6. Does `await Clients.All.SendAsync(...)` mean clients received the message?**
No. It means the message was handed to the server's outgoing pipeline. There is no delivery acknowledgement. Design for loss: sequence numbers, snapshots, a source of truth behind an API.

**7. `HubException` vs other exceptions?**
A `HubException` message is always sent to the client, and it rejects the `invoke()` promise. Other exceptions show a generic error unless `EnableDetailedErrors` is on, which should be dev only because it leaks internals.

**7a. Walk me through what happens on the wire when a SignalR client connects.**
POST `/negotiate` → the server returns a connection token and its transports → the client tries WebSockets (HTTP `Upgrade` → `101`) and falls back to SSE, then long polling, if a transport fails → handshake `{"protocol":"json","version":1}` / `{}` → messages are JSON objects (`type`, `target`, `arguments`, `invocationId`) ending with `0x1E`. See [02a](02a-signalr-how-it-works.md).

**7b. `invoke` vs `send` on the client?**
`invoke` includes an `invocationId`, so the server replies with a Completion (result or error) and the promise waits for it. `send` has no id: it's fire-and-forget, and it resolves once the message is sent.

**7c. Your hub needs shared state. How do you provide it, and what must you be careful about?**
Register a service as a **singleton** in DI and inject it into the hub. Hubs are created per call, so the singleton is the shared part. Make it **thread-safe** (calls run in parallel). Never inject a **scoped** service into it (captive dependency). Remember it's **one per process**, so a second server has its own copy, and you need Redis or a database to scale out.

**7d. What does WebSocket give you and what doesn't it?**
It gives you a persistent, full-duplex, low-overhead channel that starts with an HTTP Upgrade. It does **not** give you message meaning, request/response matching, reconnect, groups, fallback, auth headers from browsers, or delivery guarantees. See [01a](01a-websockets-explained.md).

## Real-time design

**8. How do you implement a fair countdown when clients' clocks differ?**
The server sends an **absolute end time** plus its **current time**. The client computes the clock offset and renders `endsAt - serverNow`. The server enforces the real deadline on every bet. The client countdown is just a display.
*Follow-up:* "What about latency?" The display may be off by one-way latency (about 50 ms). Acceptable, because the server decides. You can also show "closing…" in the last 500 ms.

**9. Why check both the phase and the timestamp when accepting a bet?**
The timer that flips the phase can fire late (thread pool pressure, GC). Between the deadline and the actual phase change, a phase-only check would accept late bets.

**9a. Explain long polling. What are its pitfalls?**
The client sends a request, and the server holds it until there's news or a timeout (SignalR: 90 s). The client re-polls immediately after every response. Pitfalls: the **gap** between a response and the next request (fix with a per-client buffer or a `since` cursor); the **timeout** must be shorter than proxy idle limits; held requests need **async I/O**, not threads; client→server needs separate requests; **sticky sessions** when scaled out; duplicates on retry; the HTTP/1.1 limit of ~6 connections per domain. See [01b](01b-short-and-long-polling.md).
*Follow-up:* "What happens to long polling under a high message rate?" Every message costs a response plus a new request, so it degrades into rapid short polling. That's when you want WebSockets.

**10. Polling vs SSE vs WebSockets, when to use each?**
See [doc 01](01-realtime-push-fundamentals.md). In short: polling for rare updates, SSE for one-way feeds, WebSockets for two-way and high frequency. SignalR abstracts the choice.

## Reliability

**11. A player places a bet and their balance check passes, but they place a second bet at the same time. How do you prevent double spending?**
Make the check and the debit **one atomic operation**: a lock in memory (Phase 1), a Redis Lua script or transaction (Phase 3), or a conditional UPDATE in SQL (`UPDATE ... SET balance = balance - @x WHERE balance >= @x`).

**12. Why generate the roulette number with `RandomNumberGenerator` and not `Random`?**
`Random` is a predictable PRNG: with a few outputs you can predict the next ones. Gambling needs a cryptographically secure (and in practice certified) RNG, on the server only.

## Scaling and Redis

**13. You add a second SignalR server behind a load balancer and players start missing messages. Why?**
Groups and connections live in each server's memory, so `Clients.Group("table-1")` only reaches connections on the server that sends. Fix: a backplane (`AddStackExchangeRedis`) so every server publishes and receives via Redis, or Azure SignalR Service. We measured it: without a backplane, Alice (5001) and Bob (5002) at "table-1" never saw each other's bets.

**14. What exactly does the Redis backplane do?**
Each server subscribes to Redis channels such as `<Hub>:all`, `<Hub>:group:<name>`, `<Hub>:connection:<id>`, `<Hub>:user:<id>`, plus internal channels for cross-server group membership and acks. Sending to a group publishes once to Redis, and every subscribed server delivers to its local connections. It stores nothing (`DBSIZE` stayed 0).
*Follow-up:* "What if Redis goes down?" Connections stay open, but messages published meanwhile are lost (no buffering), so clients need resync. Make Redis highly available and monitor it.

**15. With the backplane in place, is the app correctly scaled?**
No. The backplane moves **messages**, not **state**. Each server still has its own memory state and its own timers. We measured two different results for "round 1" (19 and 30) and a bet rejected because the player saw the other server's timeline. You need shared state, atomic updates and a single leader for the game loop.

**16. How do you prevent double spending when two servers process bets for the same player at once?**
Make check-and-debit a single atomic operation: a Redis Lua script (or `WATCH`/`MULTI` with retry), or a conditional SQL update. Measured: 50 concurrent bets of 100 against a balance of 1000 → naive GET/SET accepted all 50, Lua accepted exactly 10.

**17. Why Lua instead of MULTI/EXEC?**
`MULTI/EXEC` queues commands but can't make decisions based on values read inside the transaction. You need `WATCH` + retry (optimistic locking), which degrades under contention. A Lua script reads, decides and writes atomically on the server in one round trip. Caveats: keep it short (it blocks Redis), and pass all keys in `KEYS[]` (cluster routing, same hash slot).

**18. How would you make sure only one server runs the round timer?**
Leader election: `SET lock myId NX PX 3000` to acquire, a compare-and-`PEXPIRE` Lua script to renew (~every TTL/3), compare-and-`DEL` to release. Followers retry. Round state lives in Redis, so the new leader continues the round. Measured failover after a leader crash: ~2.6 s.
*Follow-up:* "What about a GC pause longer than the TTL?" Two leaders could briefly coexist. Use **fencing tokens** (an epoch from `INCR`, checked by every write), or a consensus system (etcd, ZooKeeper, a Kubernetes Lease).

**19. What are the properties of Redis pub/sub, and when is it the wrong tool?**
Fire-and-forget, at-most-once, no persistence. An offline subscriber misses messages, and slow subscribers get disconnected past the output-buffer limit. Great for ephemeral push fan-out. Wrong for anything that must not be lost (bets, payouts, audit). For those use Streams, RabbitMQ or Kafka, or a database outbox.

**20. Compare Redis pub/sub, Redis Streams, RabbitMQ and Kafka.**
Pub/sub: broadcast, no storage. Streams: a persistent log in Redis with consumer groups and acks, which can replay. RabbitMQ: a broker with rich routing and work queues, where messages are removed once consumed. Kafka: a durable, partitioned, replicated log with long retention and replay, high throughput, built for event pipelines and audit. A casino might use all four: pub/sub for push, Streams for recent events, RabbitMQ for commands, Kafka for the money event log.

**21. When do you need sticky sessions with SignalR, and when not?**
Negotiate returns a connection token held in one server's memory. Measured: the token from 5001 used on 5002 → `404 No Connection with that ID`. So you need affinity for negotiate → connect, and for SSE and long polling (many requests per connection). With WebSockets only and `skipNegotiation: true`, there's a single request, so no affinity is needed. Azure SignalR Service removes the problem.

**22. What does Azure SignalR Service give you, and what doesn't it?**
It holds the client connections, groups and fan-out, scales by units, and removes the need for a backplane and sticky sessions. Your app servers only keep a few connections to it. It does **not** handle your game or money state, atomicity, idempotency or timers.

**23. What limits how many connections a single SignalR server can handle?**
Memory per connection, file descriptors, CPU for serialization and fan-out (message rate × recipients), bandwidth, and the load balancer. Threads aren't the limit unless code blocks. Measure with a load test and keep headroom.

## Diagnostics
See the 15 questions at the end of [05: Diagnostics under load](05-diagnostics-under-load.md#interview-questions).

## System design walkthroughs
*(Phase 4: "Design live roulette for 50k players", "Design a global jackpot ticker", "Design real-time balance updates across devices")*
