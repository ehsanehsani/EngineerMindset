# 07 — Ordering, Partitioning & Concurrency

> **Level 2 — Reliability.** "Will messages arrive in order?" The honest answer: *only under specific conditions, and only per key.*

---

## 1. Why ordering breaks

Even if the broker stores messages in order, they can be **processed** out of order:

| Cause | Example |
|-------|---------|
| **Competing consumers** | Worker A gets `OrderCreated`, worker B gets `OrderCancelled`; B finishes first. |
| **Retries / redelivery** | Message 1 fails and is retried after message 2 succeeded. |
| **Parallelism inside a consumer** | `MaxConcurrentCalls = 10` processes 10 messages at once. |
| **Multiple producers** | Two API instances publish — no global clock. |
| **Different partitions / queues** | Order is only guaranteed *inside* one partition/queue. |
| **Delayed messages** | Retry queues and scheduled messages jump in time. |

> **Global ordering** across a whole system = a single consumer, single partition → **no scalability**. Nobody does this at scale.

---

## 2. The real goal: ordering **per key**

You rarely need *all* messages in order. You need all messages **for the same entity** in order: all events of **order 1234** in sequence; order 1234 and order 5678 can be processed in parallel.

| Broker | Mechanism for per-key ordering |
|--------|-------------------------------|
| **Kafka / Event Hubs** | **Partition key**. Same key → same partition → ordered. One consumer per partition in a group. |
| **Azure Service Bus** | **Sessions** (`SessionId = orderId`). One consumer holds the session lock; messages in a session are processed in FIFO order. |
| **AWS SQS FIFO** | **MessageGroupId**. |
| **RabbitMQ** | A single queue with a single consumer (or prefetch 1) is ordered. For per-key parallelism: **consistent-hash exchange** or **super streams**/streams with single active consumer. |

---

## 3. Partitions (Kafka mental model)

```
Topic "orders" (3 partitions)
  P0: [o1-created][o4-created][o1-paid][o1-shipped]
  P1: [o2-created][o2-paid]
  P2: [o3-created][o5-created][o3-cancelled]

key = orderId → hash(key) % 3 → partition
```

- Order is guaranteed **within a partition** only.
- **Consumer group:** each partition is assigned to **exactly one** consumer in the group.
- Max useful consumers in a group = **number of partitions**. Extra consumers sit idle.
- Choosing partition count is a capacity decision (hard to reduce later; increasing it **changes key→partition mapping** and breaks ordering for existing keys during the transition).

### Hot partitions
A bad key (e.g. `country` where 80% of traffic is one country) makes one partition overloaded while others idle. Choose high-cardinality, evenly distributed keys (`orderId`, `customerId`).

### Rebalancing
When a consumer joins/leaves/crashes, partitions are reassigned. During a rebalance:
- Consumption pauses (shorter with cooperative/incremental rebalancing and the newer consumer-group protocol in Kafka 4).
- Uncommitted offsets → messages re-read → **duplicates**. Commit carefully and stay idempotent.

---

## 4. Handling out-of-order messages in the consumer

When you can't guarantee order (or as defense in depth):

| Technique | How |
|-----------|-----|
| **Version / sequence number** | Event carries `version: 7`. Consumer stores last version per entity and ignores older ones. |
| **Timestamps (careful)** | "Last write wins" by event time. Clock skew can bite. |
| **State machine checks** | `OrderShipped` arrives before `OrderPaid`? Reject/park/retry later because the transition isn't valid yet. |
| **Commutative operations** | Design updates so order doesn't matter (sets, max(), counters with CRDT-like logic). |
| **Snapshot events** | Send full state + version; the newest version wins regardless of arrival order. |
| **Re-sequencer** | Buffer messages and release them in order (complex — last resort). |

---

## 5. Concurrency inside a consumer

| Setting | Effect |
|---------|--------|
| **Prefetch / PrefetchCount** | How many messages the broker sends ahead to a consumer before acks. High = throughput, but messages sit "locked" in one consumer and lock expiry risk grows. Low (1) = fair distribution, ordered, slower. |
| **MaxConcurrentCalls** (Service Bus) / parallel handlers | Process N messages in parallel in one instance. Breaks ordering. |
| **MaxConcurrentSessions** | Process N *sessions* in parallel, each session still ordered — best of both. |
| **Single Active Consumer** (RabbitMQ) | Many consumers attached, only one active → ordering + failover. |

### Concurrency on the data side
Two messages for the same entity on two workers can race on the DB. Protect with:
- **Optimistic concurrency** (rowversion / ETag) → conflict → retry.
- Per-key routing (partition/session) so the same entity is never processed concurrently.

---

## Interview one-liners

- "Kafka guarantees order per partition; I choose `orderId` as the key so all events of one order are ordered while different orders scale out."
- "In Service Bus I use sessions with `SessionId = orderId` and `MaxConcurrentSessions` for parallelism across orders."
- "Consumers should still defend against out-of-order delivery with version numbers or state-machine checks."

## Check yourself

1. Topic with 6 partitions, consumer group of 8 instances. How many are busy?
2. Why is increasing partitions on an existing topic risky for ordering?
3. `OrderShipped` arrives before `OrderPaid`. List three possible strategies.
4. What's the trade-off of a large prefetch count?

<div dir="rtl" lang="fa">

**خلاصه:** ترتیب سراسری در سیستم مقیاس‌پذیر عملاً ممکن نیست؛ هدف واقعی ترتیب «به ازای هر کلید» است (مثلاً همه پیام‌های یک سفارش). در Kafka با Partition Key، در Service Bus با Session، در SQS FIFO با MessageGroupId. تعداد مصرف‌کننده مفید در یک Consumer Group حداکثر برابر تعداد پارتیشن‌هاست. در مصرف‌کننده هم با شماره نسخه (Version) یا بررسی State Machine از پیام‌های خارج از ترتیب دفاع کن.

</div>
