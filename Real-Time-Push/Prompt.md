You are my senior engineer mentor. I am preparing for a technical interview at Gaming1 (an online casino and sports betting company). I want to learn real-time push design, ASP.NET Core SignalR, and scaling with a Redis backplane by building a tiny project. The game itself is unimportant; every feature exists to teach a concept. This project will also be my long-term study resource, so documentation matters as much as code.

## General rules
- Work PHASE BY PHASE. After each phase, STOP and give me:
  (1) what you built,
  (2) exact commands to run it,
  (3) what to try in the browser to observe the concept,
  (4) 3 short questions to check my understanding.
  Wait for me to say "next" before continuing.
- Keep code simple and readable. Prefer clarity over cleverness. No unnecessary abstractions.
- Add short comments in code marked `// CONCEPT:` that explain WHY something is done, linking to the relevant doc file.
- Use .NET 8 or later (use the installed SDK), ASP.NET Core SignalR, and a plain HTML + vanilla JS client served from wwwroot, using the official @microsoft/signalr JS client from a CDN. No frontend framework.
- Each phase must be committed-ready and runnable on its own.

## The game: "Mini Roulette"
- Players open the web page, enter a nickname, and join one of 2 tables ("table-1", "table-2").
- Each player starts with a balance of 1000 credits.
- A round lifecycle runs on the server, per table, on a loop:
  BettingOpen (10s) -> BettingClosed (2s) -> Result (spin a number 0-36) -> Payout -> next round.
- During BettingOpen, a player can bet an amount on "Red", "Black", or a single number.
- The server decides the result and pays out (Red/Black pays 2x, single number pays 36x; 0 is green and loses colour bets).
- Everyone at the table sees: round state + countdown, the list of players and their bets, the result.
- Each player privately sees: their own balance and win/loss.
- A global "jackpot" counter grows with every bet (1% of each bet) and is shown to ALL players across all tables.

## Phases

### Phase 1 – Basic SignalR (single server, in-memory)
- Create a solution `RealtimeRoulette` with an ASP.NET Core project `Roulette.Server`.
- One hub `RouletteHub` with methods: JoinTable(tableId, nickname), LeaveTable, PlaceBet(...).
- Use SignalR Groups for tables. Use Clients.Caller / Clients.User / Clients.Group / Clients.All deliberately and explain each choice.
- A BackgroundService `GameEngine` runs the round loop and pushes events via IHubContext (explain why a background service cannot use the Hub instance directly).
- Server-authoritative timing: the server rejects bets after BettingClosed, even if the client still shows the button. Send the server's round end time so the client countdown is calculated from it, not from a local timer.
- Validate bets on the server (balance, amount > 0, correct round state). Never trust the client.
- Handle OnConnectedAsync / OnDisconnectedAsync.
- Simple UI: nickname, table picker, bet controls, live event log panel showing every message received (so I can SEE the push traffic).

### Phase 2 – Reliability patterns used in real casino systems
- Strongly-typed hub (`Hub<IRouletteClient>`) so message names are not magic strings.
- Sequence numbers: every table event carries an increasing `seq`. The client tracks the last seq it received.
- Reconnect and resync: enable automatic reconnect on the client. On reconnect, the client calls `Resync(tableId, lastSeq)`, and the server returns a full SNAPSHOT of current table state (round state, end time, bets, balance). Explain "snapshot + deltas".
- Idempotent bets: the client generates a `betId` (GUID). If the same betId arrives twice, the server does not place it twice. Add a "Send bet twice" debug button to prove it.
- Duplicate/out-of-order protection on the client: ignore events with seq <= lastSeq.
- Balance as source of truth: add a normal REST endpoint `GET /api/players/{id}/balance`. The push is a notification; the client can always re-fetch from the API. Explain why money must not depend on push delivery.
- Jackpot throttling: the jackpot changes on every bet but is broadcast at most once per second, with coalescing. Explain why this message type tolerates loss while payouts do not.
- Simple authentication: a fake login endpoint that issues a token (JWT or a simple signed token). The client passes it via `accessTokenFactory`. Use a custom IUserIdProvider so `Clients.User(...)` works across a player's multiple tabs. Explain why the token goes in the query string for WebSockets.
- Add a "Simulate disconnect" button on the client to test reconnection.

### Phase 3 – Scale out with Redis backplane (the most important phase)
- First, DEMONSTRATE THE PROBLEM: run two server instances on different ports (e.g. 5001 and 5002) WITHOUT Redis. Player A on 5001 and player B on 5002 join the same table and do not see each other. Explain why.
- Add docker-compose.yml with Redis.
- Add the SignalR Redis backplane (Microsoft.AspNetCore.SignalR.StackExchangeRedis). Show that the problem is fixed for messages.
- Then show the SECOND problem: game state is still in-memory per instance, and both instances run their own round timer. Fix it:
  - Move shared state (round state, bets, balances, jackpot, processed betIds) to Redis.
  - Make bet placement atomic (Redis transaction or Lua script) to prevent double spending when two instances handle bets at the same time.
  - Only ONE instance should run the GameEngine. Implement a simple leader election using a Redis lock with expiry (SET NX PX + renewal). Kill the leader and show that the other instance takes over.
- Also add a separate small demo of raw Redis pub/sub (a console app `Roulette.PubSubDemo` with a publisher and subscriber) so I understand what the backplane is doing underneath. Explain: pub/sub is fire-and-forget, no persistence, subscribers that are offline miss messages. Compare briefly with Redis Streams, RabbitMQ and Kafka.
- Explain sticky sessions: when they are needed (non-WebSocket transports, negotiate step) and how Azure SignalR Service removes the need to manage this yourself.
- Add a simple nginx (or YARP) load balancer in docker-compose in front of both instances, with sticky sessions, so I can see a realistic setup.

### Phase 4 – Load and diagnostics
- Create a console app `Roulette.LoadTest` that opens N bot connections (configurable, e.g. 500), joins tables, and places random bets. Report connection count, messages/sec received, and latency (server timestamp vs receive time).
- Add OpenTelemetry metrics or simple custom counters: connected clients, bets/sec, messages pushed/sec, round processing duration.
- Deliberately add a "bad mode" flag that introduces a classic bug (e.g. sync-over-async `.Result` call in the bet path, or a global lock). Run the load test, then show me how to diagnose it with `dotnet-counters` (thread pool queue length, GC) and `dotnet-trace`. Then fix it and compare numbers.
- Add a slow-client scenario: one bot that reads messages very slowly. Explain backpressure and SignalR settings like MaximumReceiveMessageSize, client timeout, keep-alive interval, and per-connection buffers.

## Documentation (in a /docs folder, written for interview study)
Write these Markdown files, clear and concise, with diagrams in Mermaid where helpful:
- README.md (root): project overview, architecture diagram, how to run each phase, learning path.
- docs/01-realtime-push-fundamentals.md: polling vs long polling vs SSE vs WebSockets, persistent connections, trade-offs.
- docs/02-signalr-concepts.md: hubs, connections, users, groups, transports and negotiation, IHubContext, strongly-typed hubs, connection lifecycle, authentication, configuration settings.
- docs/03-casino-realtime-patterns.md: server-authoritative state and timing, push as notification vs source of truth, snapshot + deltas, sequence numbers, idempotency, message criticality (jackpot vs payout), fairness and audit logging.
- docs/04-scaling-and-redis.md: why scaling real-time is hard, backplane, Redis pub/sub internals, shared state, atomic operations, leader election, sticky sessions, Azure SignalR Service, limits per server, comparison with Kafka/RabbitMQ/Redis Streams.
- docs/05-diagnostics-under-load.md: RED and USE methods, common .NET bottlenecks (thread pool starvation, GC, lock contention, DB pool exhaustion), dotnet-counters / dotnet-trace / dotnet-dump, what I observed in Phase 4, mitigate-first approach.
- docs/06-interview-questions.md: at least 30 interview questions with strong model answers, grouped by: SignalR basics, real-time design, scaling and Redis, reliability, diagnostics, and 3 full system design walkthroughs ("Design live roulette for 50k players", "Design a global jackpot ticker", "Design real-time balance updates across devices"). Include follow-up questions an interviewer might ask and pitfalls to avoid.
- docs/07-cheatsheet.md: one-page summary of the most important points to review the night before the interview.

Update the docs at the end of each phase with anything learned in that phase.

Start with Phase 1. Before writing code, show me the folder structure you plan to create and a short architecture explanation, then proceed.