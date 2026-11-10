# 15 — Interview Questions & System Design Scenarios

> **Level 4 — Review.** Practice answering out loud. Short answers first; links point to the deep dive.

---

## Part A — Rapid-fire questions (with model answers)

### Basics

**1. Why use a message broker instead of HTTP calls?**
To remove temporal and location coupling, absorb load spikes, scale consumers independently, and fan out events to many services. The cost is eventual consistency, duplicates and harder debugging. → [01](01-Why-Messaging.md)

**2. Queue vs topic?**
Queue: each message to one consumer (commands/work). Topic: each subscription gets a copy (events/fan-out). → [04](04-Communication-Models.md)

**3. Command vs event?**
Command = "do this", one handler, can be rejected, imperative name. Event = "this happened", any number of subscribers, immutable fact, past tense. → [03](03-Message-Types.md)

**4. What is a competing consumer?**
Multiple instances reading the same queue so each message is processed by one of them — horizontal scaling, but ordering across instances is lost.

**5. Kafka vs RabbitMQ in one sentence?**
Kafka is a retained, partitioned log with replay and huge throughput; RabbitMQ is a smart broker with rich routing that deletes messages on ack. → [14](14-Choosing-a-Broker.md)

### Reliability

**6. Explain at-most-once, at-least-once, exactly-once.**
Ack before processing → may lose. Ack after → may duplicate. Exactly-once end-to-end is impractical; achieve exactly-once effect via at-least-once + idempotency. → [05](05-Delivery-Guarantees-Idempotency.md)

**7. How do you make a consumer idempotent?**
Dedup on a stable MessageId stored in the same DB transaction as the business change (unique constraint), use natural keys, conditional updates, and idempotency keys for external APIs.

**8. What is a poison message? What's a DLQ?**
A message that always fails. After a retry limit it's moved to a dead-letter queue so the main queue keeps flowing; DLQs are monitored and messages replayed after a fix. → [06](06-Failures-Retries-DLQ.md)

**9. Retry strategy?**
Classify errors; retry transient ones with exponential backoff + jitter and a cap; don't retry poison/business errors; use delayed redelivery rather than sleeping in the handler; circuit breaker for downstream outages.

**10. How do you guarantee ordering?**
Only per key: Kafka partition key, Service Bus sessions, SQS FIFO group id. Defend in the consumer with version numbers. → [07](07-Ordering-Partitioning-Concurrency.md)

**11. What happens when a consumer's processing time exceeds the lock duration?**
The lock expires, the message becomes visible, another consumer processes it too → duplicate. Fix: renew the lock (SDK auto-renew), increase lock duration, speed up processing, or use an inbox to ack early.

### Distributed design

**12. Save to DB and publish an event — what can go wrong? How do you fix it?**
Dual write: either lost or ghost events. Use the transactional outbox (same local transaction) + relay via polling or CDC; consumers stay idempotent. → [08](08-Consistency-Outbox-Inbox.md)

**13. Why not use 2PC/distributed transactions?**
Most brokers don't support it, it's slow, holds locks, and reduces availability.

**14. What is a saga? Choreography vs orchestration?**
Sequence of local transactions with compensations. Choreography = services react to events (simple, implicit flow). Orchestration = central state machine (explicit, better for complex flows). → [09](09-Sagas-Workflows.md)

**15. How do you version messages?**
Additive optional fields, tolerant readers, versioned type names for breaking changes with parallel publishing, schema registry for enforcement. → [10](10-Message-Contracts-Evolution.md)

**16. What is backpressure?**
Mechanisms for when consumers can't keep up: buffering, scaling out, throttling producers, bounded queues, load shedding. → [11](11-Scaling-Backpressure-Performance.md)

**17. How do you trace a request across async services?**
CorrelationId + W3C traceparent in message headers, OpenTelemetry instrumentation, structured logs with those ids. → [12](12-Observability-Security-Operations.md)

**18. Event sourcing vs event-driven?**
Event-driven = communication by events. Event sourcing = storing state as a sequence of events. Independent ideas. → [13](13-Event-Driven-Architecture.md)

### Tricky / senior

**19. "We use Kafka exactly-once, so we don't need idempotency." Agree?**
No. Kafka EOS covers read-process-write inside Kafka. Side effects in DBs, emails or APIs are still at-least-once.

**20. Increasing Kafka partitions from 6 to 12 — any risk?**
Key→partition mapping changes, so new events for a key can go to a different partition than older ones → temporary ordering violations; also more consumers/rebalance.

**21. A DLQ has 50,000 messages after a deploy. What do you do?**
Stop the bleeding (rollback/fix), sample and classify the DLQ messages, fix the root cause, then resubmit in controlled batches to idempotent consumers; add an alert on DLQ > 0 for the future.

**22. How would you handle a message that must be processed in order, but one message is poison?**
Ordered queues block on poison messages. Options: dead-letter it and **park** subsequent messages for that key (not the whole queue), alert, fix, then replay in order; with sessions only that session is blocked.

---

## Part B — System design scenarios

Use this structure for every scenario: **requirements → message types → topology → reliability → ordering → failures → scaling → observability**.

### Scenario 1 — E-commerce order placement

> "Design order placement: the API should respond fast; payment, inventory, email and analytics must happen reliably."

Points to cover:
- API validates, saves `Order(Pending)` + `OrderPlaced` in **Outbox**, returns **202** with order id.
- Topic `orders` → subscriptions: Inventory, Payment, Notifications, Analytics.
- Saga (orchestrated) for Inventory → Payment → Confirm, with compensations (`ReleaseStock`, `RefundPayment`) and a payment **timeout**.
- Ordering per `OrderId` (sessions/partition key).
- Idempotent consumers (Inbox), retries + DLQ, alerts.
- Client gets status via polling `GET /orders/{id}` or SignalR push.

### Scenario 2 — Notification service (email/SMS/push) at scale

- Command queue `SendNotification`; priority via separate queues (`otp` vs `marketing`).
- Rate limits per provider, circuit breaker, provider failover.
- Dedup on `NotificationId` (users hate duplicate SMS).
- TTL for OTPs (no point sending a 10-minute-old code).
- Batching for marketing, low latency for OTP.

### Scenario 3 — Real-time analytics on clicks / IoT telemetry

- Kafka/Event Hubs, partition by `deviceId`/`userId`, compression and batching.
- At-most-once or at-least-once acceptable; aggregates tolerate small duplicates or use dedup windows.
- Stream processing with windows (Flink / Kafka Streams / Stream Analytics), event time + watermarks.
- Retention for replay; cold storage (data lake) via connectors.

### Scenario 4 — Video upload & processing pipeline

- Upload to blob storage → blob-created event (Event Grid) → `TranscodeVideo` command (claim check).
- Long processing → lock renewal or ack-early + status table; competing consumers on GPU workers; autoscale on queue length (KEDA).
- Progress events for the UI; DLQ for corrupt files.

### Scenario 5 — Migrating a monolith to services using events

- **Strangler fig:** monolith publishes integration events via outbox/CDC; new services consume them.
- Anti-corruption layer to translate legacy models into clean events.
- Dual-run and compare, then switch writes.

---

## Part C — Glossary (quick lookup)

| Term | One-line definition |
|------|---------------------|
| Ack / Nack | Confirm success / report failure of processing |
| AMQP | Open wire protocol used by RabbitMQ (0-9-1) and Service Bus (1.0) |
| Backpressure | Coping when consumers are slower than producers |
| Broker | Server that stores and routes messages |
| CDC | Change Data Capture — stream DB changes from the transaction log |
| Claim check | Store large payload elsewhere, send a reference |
| Compensating transaction | Business action that semantically undoes a previous step |
| Competing consumers | Many instances share one queue |
| Consumer group | Kafka consumers sharing partitions of a topic |
| Consumer lag | How far behind a consumer is (latest − committed offset) |
| Correlation id | Id linking all messages of one business flow |
| Dead-letter queue | Holding area for messages that can't be processed |
| Delivery count | How many times a message was delivered |
| Dual write | Writing to two systems without a shared transaction |
| Durable / persistent | Survives broker restart |
| Event sourcing | Persisting state as an append-only list of events |
| Exchange | RabbitMQ routing component |
| Fan-out | One message delivered to many receivers |
| Idempotency | Processing many times = same effect as once |
| Inbox | Consumer-side dedup/persistence of incoming messages |
| Lock / visibility timeout | Time a consumer exclusively holds a message |
| Log compaction | Keep only the latest record per key |
| Offset | Position in a Kafka partition |
| Outbox | Store outgoing messages in the same DB transaction as business data |
| Partition | Ordered shard of a topic |
| Poison message | Message that always fails |
| Prefetch | Messages delivered ahead before ack |
| Pub/Sub | Publish once, all subscribers receive |
| Rebalance | Reassigning partitions among consumers |
| Retention | How long messages are kept |
| Saga | Distributed workflow of local transactions + compensations |
| Session (Service Bus) | Group of messages processed in order by one consumer |
| Topic | Pub/sub channel |
| TTL | Time-to-live — message expiry |

<div dir="rtl" lang="fa">

**خلاصه:** این فایل برای مرور قبل از مصاحبه است: بخش A پرسش‌های کوتاه با پاسخ نمونه (پایه، قابلیت اطمینان، طراحی توزیع‌شده، سوالات سخت)، بخش B سناریوهای System Design (ثبت سفارش، سرویس اعلان، آنالیتیکس بلادرنگ، پردازش ویدیو، مهاجرت از Monolith) و بخش C واژه‌نامه. پاسخ‌ها را با صدای بلند تمرین کن و برای هر سناریو این ترتیب را رعایت کن: نیازمندی‌ها، نوع پیام، توپولوژی، قابلیت اطمینان، ترتیب، خطاها، مقیاس و مانیتورینگ.

</div>
