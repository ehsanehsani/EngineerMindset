# 11 — Scaling, Backpressure & Performance

> **Level 3 — Distributed design.** How fast can the system go, what happens when producers are faster than consumers, and which knobs matter.

---

## 1. Key metrics

| Metric | Meaning |
|--------|---------|
| **Throughput** | Messages (or MB) per second. |
| **Latency (end-to-end)** | Time from publish to processing complete. Watch **p95/p99**, not just average. |
| **Queue depth / backlog** | Messages waiting. Growing steadily = consumers too slow. |
| **Consumer lag** (Kafka) | Latest offset − committed offset, per partition. The streaming version of queue depth. |
| **Age of oldest message** | Often more meaningful than depth: "we are 12 minutes behind". |
| **Processing time per message** | Drives how many consumers you need. |
| **Error / retry / DLQ rate** | Health. |

### Capacity math (Little's Law)

```
consumers needed ≈ arrival rate × processing time
e.g. 500 msg/s × 0.2 s = 100 concurrent handlers
```

If arrival rate > total processing capacity for long enough, backlog grows without limit.

---

## 2. Backpressure

**Backpressure** = a way for a slow consumer to say "slow down" to a fast producer, or for the system to cope when it can't.

| Strategy | How | Trade-off |
|----------|-----|-----------|
| **Buffer** (just queue it) | The broker absorbs the spike | Fine for spikes; dangerous if sustained (disk/memory full) |
| **Scale out consumers** | Add instances | Bounded by partitions/DB capacity |
| **Throttle producers** | Rate limit API, return 429/503 | Pushes the problem to clients |
| **Broker flow control** | RabbitMQ blocks publishers on memory/disk alarms; Service Bus throttles (`ServerBusy`) | Producers must handle it |
| **Bounded queues** | `max-length`, overflow = reject-publish or drop-head | Explicit, but data loss or rejections |
| **Load shedding** | Drop low-priority messages | Only for non-critical data |
| **Prefetch / credit limits** | Consumer only receives what it can handle | Fair distribution |

> Queues make **spikes** cheap, not **sustained overload**. If you're always behind, you need more capacity, not a bigger queue.

---

## 3. Scaling consumers

- **Horizontal:** more instances (competing consumers). Limits: partitions (Kafka), sessions (ordered), the **database** behind the consumer (often the real bottleneck).
- **Vertical/concurrency:** more parallel handlers per instance (`MaxConcurrentCalls`, multiple channels/threads).
- **Autoscaling on queue metrics:** **KEDA** (Kubernetes Event-Driven Autoscaling) scales pods on queue length or Kafka lag; Azure Functions / Container Apps scale on Service Bus / Event Hubs triggers.
- **Scale to zero:** event-driven compute (Functions, KEDA) can run no instances when queues are empty.

---

## 4. Performance knobs

| Knob | Effect |
|------|--------|
| **Batching** (send & receive) | Fewer network round trips → much higher throughput, slightly higher latency. Kafka `linger.ms` + `batch.size`; Service Bus `SendMessagesAsync(batch)`. |
| **Compression** | Less bandwidth (Kafka `compression.type` = lz4/zstd). |
| **Prefetch** | Fewer round trips; risk of lock expiry and unfairness. |
| **Persistence / replication level** | `acks=all` / quorum queues / persistent messages are slower but safe. Choose per use case. |
| **Message size** | Small messages = higher msg/s. Use claim-check for big payloads. |
| **Connection reuse** | Connections are expensive. One long-lived connection (`ServiceBusClient`, RabbitMQ `IConnection`) per app; channels/senders per use. Never connect per message. |
| **Serialization** | Binary formats are faster than JSON for very high volume. |
| **Partition count** | More partitions = more parallelism (and more overhead). |

### Latency vs throughput
Batching, linger and compression **increase throughput** but **add latency**. A payment-authorization flow and a click-stream pipeline need different settings.

---

## 5. Priority, fairness, and noisy neighbors

- **Priority queues** (RabbitMQ `x-max-priority`) — or simpler: **separate queues** (`orders.vip`, `orders.standard`) with more consumers on the important one.
- **Multi-tenant fairness:** one big tenant can flood a shared queue. Options: per-tenant queues/partitions, rate limits per tenant, weighted consumption.

---

## Interview one-liners

- "Queues absorb spikes, but sustained overload needs more consumer capacity or producer throttling."
- "I monitor backlog **age** and consumer lag, and autoscale consumers with KEDA or Functions based on them."
- "The real bottleneck is often the database behind the consumer, not the broker."

## Check yourself

1. 2000 msg/s, 50 ms per message. How many concurrent handlers do you need?
2. Kafka topic with 4 partitions, lag keeps growing, consumers are at 100% CPU. What are your options?
3. Why might increasing prefetch make things *worse*?
4. What is the difference between queue depth and age of the oldest message, and why care?

<div dir="rtl" lang="fa">

**خلاصه:** متریک‌های مهم: Throughput، Latency (p99)، عمق صف، Consumer Lag و سن قدیمی‌ترین پیام. Backpressure یعنی وقتی مصرف‌کننده کندتر از تولیدکننده است چه کنیم: بافر کردن، افزایش مصرف‌کننده‌ها، محدود کردن تولیدکننده، صف محدود یا حذف پیام‌های کم‌اهمیت. صف برای جذب پیک خوب است، نه برای بار دائمی بیش از ظرفیت. Batching و فشرده‌سازی Throughput را بالا می‌برند ولی Latency را کمی زیاد می‌کنند. با KEDA یا Azure Functions می‌توان بر اساس طول صف Autoscale کرد.

</div>
