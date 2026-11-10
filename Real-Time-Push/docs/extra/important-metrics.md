# The most important metrics (for any app, any dashboard)

The Aspire Metrics page shows a long list (`http.server.request.duration`, `dotnet.gc.collections`, `kestrel.active_connections`…). This page explains **the ones that matter**, what they tell you, and when to look at them. The names are **OpenTelemetry standard names**, so you'll find the same metrics in Grafana, Application Insights, Datadog and others. Learn them once, use them everywhere.

> Names can differ slightly between versions or tools (e.g. older versions use `process.runtime.dotnet.gc.collections.count` instead of `dotnet.gc.collections`). Look for the keywords: **gc**, **thread_pool**, **request.duration**, **active_connections**.

---

## 1. First: the three kinds of metrics

| Kind | What it is | How you read it | Example |
|---|---|---|---|
| **Counter** | a number that only goes **up** | look at its **rate**: "how many per second / minute" | requests handled, exceptions thrown, GC collections |
| **Histogram** | records **every value** into buckets | **percentiles** (p50/p95/p99), plus a **count** (= how many) | request duration |
| **Gauge / up-down counter** | a **current value** that goes up and down | "how many right now" | active requests, open connections, memory used, queue length |

> 💡 A **duration histogram** gives you two things at once: how **long** requests take (percentiles), and how **many** there were (its count = traffic). That's why `http.server.request.duration` is the most useful metric of all.

New to percentiles? Read [percentiles.md](percentiles.md) first.

---

## 2. The "four golden signals"

Google's SRE book says: if you can only watch four things, watch these. Almost every metric below belongs to one of them.

| Signal | Question | Main metric |
|---|---|---|
| **Latency** | how long do requests take? | `http.server.request.duration` (p50/p95/p99) |
| **Traffic** | how much demand is there? | count/rate of `http.server.request.duration` |
| **Errors** | how many requests fail? | `http.server.request.duration` filtered by status code 5xx |
| **Saturation** | how "full" is the system? | thread pool queue, CPU, memory, connection pools, `active_requests` |

(These are the same ideas as **RED** and **USE** in [05: Diagnostics](../05-diagnostics-under-load.md).)

---

## 3. HTTP server metrics: "how is my API doing?"

### ⭐ `http.server.request.duration` (histogram)
**The single most important metric.** One measurement per incoming request.

It has **tags** you can filter and split by:
- `http.route`: which endpoint (`/orders`, `/brew`)
- `http.request.method`: GET, POST…
- `http.response.status_code`: 200, 404, 500…

From this one metric you get **three of the four golden signals**:

| You want | Look at |
|---|---|
| **Latency** | its p50 / p95 / p99 |
| **Traffic** | its count per second (requests/s) |
| **Errors** | its count where `status_code` is 5xx, divided by the total = **error rate** |

**When you use it:**
- "Is the API slow?" → p95/p99, split by `http.route` to find **which endpoint**.
- "Did the deploy break something?" → compare error rate and p99 before and after.
- "How busy are we?" → requests/s.
- **Alerting:** "error rate > 2% for 5 min" or "p99 of `/orders` > 1 s".

> 💡 4xx errors (400, 404) are usually the **client's** mistake. Alert on **5xx** (your fault). A sudden jump in 4xx is still worth a look, though (a broken client release, an attack).

### `http.server.active_requests` (up-down counter)
How many requests are **being processed right now**.
- Steadily growing = requests come in faster than they finish → **saturation** (blocked threads, a slow DB…).
- Use it together with latency: active requests ↑ and latency ↑ = you're hitting a limit.

---

## 4. HTTP client metrics: "how are the services I call doing?"

### ⭐ `http.client.request.duration` (histogram)
Same as the server metric, but for **outgoing** calls (to other services and external APIs). Tags include the server address and status code.

**When you use it:**
- "Is it us or the service we call?" If your own server's p99 is high **and** `http.client.request.duration` to the payment API is high, **the payment API is the cause**.
- Spotting retries and timeouts toward a dependency.

In the coffee shop: coffeeshop's client duration to `barista` explains almost all of coffeeshop's own latency.

### `http.client.open_connections` / `http.client.active_requests`
How many connections and calls are open to other services. Useful for spotting **connection pool exhaustion** (stuck at a maximum, with requests waiting).

---

## 5. Kestrel and SignalR metrics: connections

Kestrel is ASP.NET Core's web server. These matter most for **real-time apps** (WebSockets, SignalR), where connections stay open for a long time.

| Metric | Kind | Tells you | Watch for |
|---|---|---|---|
| `kestrel.active_connections` | gauge | open TCP connections right now | sudden drops (a deploy or crash → a reconnect storm coming) or endless growth |
| `kestrel.queued_connections` | gauge | connections waiting to be accepted | anything above ~0 for long = the server can't keep up |
| `kestrel.rejected_connections` | counter | connections refused (over the limit) | any value = you're over capacity |
| `kestrel.connection.duration` | histogram | how long connections stay open | short for normal HTTP, long for WebSockets |
| `signalr.server.active_connections` | gauge | connected SignalR clients | your "players/users online" number; capacity planning |
| `signalr.server.connection.duration` | histogram | how long SignalR clients stay connected | many very short connections = clients disconnecting and reconnecting in a loop |

---

## 6. .NET runtime metrics: "how is the process itself doing?"

These explain **why** the HTTP metrics look bad. They're the "USE" side: is a resource saturated?

### CPU and memory

| Metric | Tells you | Bad sign |
|---|---|---|
| `dotnet.process.cpu.time` (often shown as CPU %) | how much CPU the app uses | constantly near 100% → computing too much (hot code, GC) |
| `dotnet.process.memory.working_set` | memory the process uses | **only goes up, never down** over hours → memory leak |

### Garbage collector (GC)

| Metric | Tells you | Bad sign |
|---|---|---|
| `dotnet.gc.collections` (split by generation gen0/gen1/gen2) | how often the GC runs | many **gen2** collections (expensive, full cleanups) |
| `dotnet.gc.heap.total_allocated` | how fast the app allocates memory | very high = lots of short-lived objects = more GC work |
| `dotnet.gc.pause.time` | time the app was **paused** by the GC | pauses lining up with p99 spikes |
| `dotnet.gc.last_collection.heap.size` | the size of the managed heap | growing after every collection → a leak |

> 🧠 **GC in one sentence:** .NET cleans up unused memory automatically, but cleaning costs CPU and sometimes briefly pauses the app. Allocate less → less GC → smoother p99.

### Thread pool

| Metric | Tells you | Bad sign |
|---|---|---|
| ⭐ `dotnet.thread_pool.queue.length` | work **waiting** for a thread | growing while CPU is low = **thread pool starvation** (blocking calls like `.Result`) |
| `dotnet.thread_pool.thread.count` | how many pool threads exist | climbing steadily = the pool is adding threads because existing ones are blocked |
| `dotnet.thread_pool.work_item.count` | how many work items were processed | (throughput of the pool) |

### Others worth knowing

| Metric | Tells you | Bad sign |
|---|---|---|
| `dotnet.monitor.lock_contentions` | how often threads had to **wait for a lock** | high = threads fighting over a `lock` → lower throughput |
| `dotnet.exceptions` (.NET 9+) | exceptions thrown, by type | a sudden jump, even if users don't see errors (exceptions are expensive and hide bugs) |
| `dotnet.jit.*`, `dotnet.assembly.count` | code compilation/loading | mostly interesting at startup; ignore day to day |

---

## 7. Database and other dependencies

The exact names depend on the library (EF Core, SqlClient, Npgsql, Redis client…). Look for these **ideas**:

| Idea | Typical name | Tells you |
|---|---|---|
| Query duration | `db.client.operation.duration` | slow queries (look at p99, split by operation) |
| Connections in use / waiting | `db.client.connection.count` (with state `used`/`idle`), pending requests | **pool exhaustion**: all in use and others waiting |
| Cache hit rate | a custom counter: hits vs misses | a low hit rate = the cache isn't helping |
| Queue length / consumer lag (RabbitMQ, Kafka…) | `…queue.depth`, consumer lag | messages arriving faster than they're processed |

---

## 8. Infrastructure metrics (outside your app)

Usually from the hosting platform (Kubernetes, Azure, AWS…):
- **CPU and memory per machine/container**, and the **limits** (is the container close to its memory limit? It'll be killed: "OOMKilled").
- **Container restarts**: a restarting app often means crashes or failed health checks.
- **Disk** usage and I/O, **network** traffic.
- **Instance count** (autoscaling working?).

---

## 9. Business metrics: often the most important of all

Technical metrics say "the system is healthy". **Business metrics say "the business is working".** You create them yourself as custom counters (like `coffeeshop.orders` in the training).

| Domain | Examples |
|---|---|
| Shop | orders per minute, checkout failures, payment declines, cart abandonment |
| Casino / betting | bets placed per second, rejected bets by reason, payouts, logins |
| SaaS | sign-ups, logins, failed logins, active users |
| Real-time app | messages delivered per second, connected users, reconnects per minute |

> 💡 A drop in "orders per minute" to zero can be the **first** sign of a problem, even when CPU, memory and error rates all look normal (e.g. a broken button in the front end). Always have at least one business metric with an alert.
>
> ⚠️ Tags: use a few values (`coffee=latte`, `result=failed`), **never ids** (user id, order id). Those belong in logs and traces. See "high cardinality" in the training.

---

## 10. A starter dashboard (8 panels)

If you had to build one dashboard for any web API, start with:

| # | Panel | Metric |
|---|---|---|
| 1 | Requests per second | count of `http.server.request.duration` |
| 2 | Error rate (5xx %) | `http.server.request.duration` by status code |
| 3 | Latency p50 / p95 / p99 | `http.server.request.duration` |
| 4 | Slowest endpoints | p99 of the same metric, split by `http.route` |
| 5 | Dependency latency | `http.client.request.duration` (+ DB duration) |
| 6 | CPU and memory | `dotnet.process.cpu.time`, `dotnet.process.memory.working_set` |
| 7 | Thread pool queue and GC | `dotnet.thread_pool.queue.length`, `dotnet.gc.collections` / pause time |
| 8 | A business number | e.g. orders per minute |

For real-time apps, add **active connections** (`signalr.server.active_connections` / `kestrel.active_connections`).

### What to alert on
Alert on **symptoms users feel** (high error rate, high p99, business metric dropping), not on every cause (CPU at 80% might be totally fine). Use the cause metrics (CPU, GC, thread pool) to **investigate** once an alert fires.

---

## 11. Quick lookup: "I see X, which metric do I check?"

| Situation | Check |
|---|---|
| "The app is slow" | `http.server.request.duration` p95/p99 by route → `http.client.request.duration` → thread pool queue, CPU, GC |
| "Errors are increasing" | `http.server.request.duration` by status code and route → logs for those requests |
| "Slow but CPU is low" | `dotnet.thread_pool.queue.length` (starvation), DB/HTTP client durations, lock contentions |
| "Memory keeps growing" | `working_set` + `gc.last_collection.heap.size` over hours → take a heap snapshot |
| "Random latency spikes" | `dotnet.gc.pause.time` and gen2 collections at the same moments |
| "Users disconnecting" (real-time) | `signalr.server.active_connections`, `kestrel.active_connections`, connection duration, restarts |
| "Everything looks fine but sales dropped" | business metrics → front-end errors / a broken flow |

---

## 12. Interview questions

**1. Which metrics would you put on a dashboard for a new web API?**
The four golden signals: request rate, error rate (5xx), latency p50/p95/p99 (all from the request duration histogram, split by endpoint), and saturation (CPU, memory, thread pool queue, connection pools). Plus dependency latency and at least one business metric.

**2. Counter vs gauge vs histogram?**
Counter only goes up (read it as a rate). Gauge is a current value that goes up and down. Histogram records a distribution, giving percentiles and a count.

**3. Why is `http.server.request.duration` so useful?**
It's a histogram per request with route, method and status tags, so it gives latency (percentiles), traffic (count) and errors (by status code), per endpoint, from one metric.

**4. What is the thread pool queue length and when does it matter?**
Work waiting for a thread-pool thread. If it grows while CPU is low, threads are blocked (sync-over-async, `Thread.Sleep`, sync I/O): thread pool starvation.

**5. Which metrics show a memory leak?**
Working set and GC heap size that keep growing over hours, even after gen2 collections. Confirm with a heap snapshot (dotnet-gcdump/dotnet-dump).

**6. What's the difference between the four golden signals and RED/USE?**
Same ideas, grouped differently. Golden signals: latency, traffic, errors, saturation. RED is the service view (rate, errors, duration). USE is the resource view (utilization, saturation, errors).

**7. What should you alert on?**
User-facing symptoms: error rate, high p95/p99, availability, business metrics dropping. Cause metrics (CPU, GC) are for investigating, not paging people at 3 a.m. for CPU at 80%.

**8. Why not put the user id as a tag on a metric?**
High cardinality: every unique value creates a new time series, which explodes cost and slows queries. Per-user detail belongs in logs and traces.

**9. Which metrics matter specifically for a real-time (WebSocket/SignalR) app?**
Active connections, connection duration (reconnect loops), messages sent per second, rejected/queued connections, plus the usual latency, CPU, memory and GC. Also the backplane's (e.g. Redis) latency.

**10. Why have business metrics if you already monitor CPU, errors and latency?**
Some failures are invisible to technical metrics (a broken button, a bad config that rejects all payments with 200 OK). A drop in orders, bets or sign-ups catches them.
