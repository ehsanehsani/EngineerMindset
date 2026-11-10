# Extra: background topics

Docs about concepts that came up while building this project and are worth knowing for interviews, but aren't specific to roulette or SignalR. Each ends with interview questions and answers.

| Doc | Topic | Size |
|---|---|---|
| [glossary.md](glossary.md) | New terms explained simply: **backplane**, **sticky sessions** (more added over time) | grows |
| [percentiles.md](percentiles.md) | p50 / p90 / p95 / p99 in simple words: why averages lie, why the 1% matters, reading the p50–p99 gap, real scenarios | full |
| [important-metrics.md](important-metrics.md) | The metrics that matter on any dashboard: counters/gauges/histograms, golden signals, HTTP, Kestrel/SignalR, .NET runtime (GC, thread pool), DB, business metrics | full |
| [http-protocol.md](http-protocol.md) | HTTP essentials: message anatomy, methods, status codes, headers, caching, CORS, HTTP/1.1 vs 2 vs 3, TLS | full |
| [scaling-deep-dive.md](scaling-deep-dive.md) | The detailed version of doc 04: Redis channels, Lua scripts, leader-election timeline, Streams/RabbitMQ/Kafka, Azure SignalR, all with measured results | full (optional) |
| [c10k-problem.md](c10k-problem.md) | The C10k problem: why "one thread per connection" doesn't scale, and how event loops / epoll / IOCP / async-await solved it | full |
| [thread-sleep-vs-task-delay.md](thread-sleep-vs-task-delay.md) | Why `Thread.Sleep` holds a thread and `await Task.Delay` releases it, with measured numbers | short |
