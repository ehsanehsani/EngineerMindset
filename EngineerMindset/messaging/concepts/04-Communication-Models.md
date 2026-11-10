# 04 — Communication Models

> **Level 1 — Foundations.** The shapes in which messages flow. Most systems combine several of them.

---

## 1. Point-to-Point (Queue)

```
Producer ──► [ Queue ] ──► Consumer
```

- Each message is processed by **exactly one** consumer.
- Used for **commands / work items** ("resize this image").
- Message is removed after ack.

## 2. Competing Consumers

```
                      ┌──► Worker 1
Producer ──► [ Queue ]├──► Worker 2      each message → only one worker
                      └──► Worker 3
```

- Several instances read from the **same queue**; the broker spreads messages among them.
- Gives **horizontal scaling** and **high availability** of processing.
- Cost: **ordering is lost** across workers (message 2 can finish before message 1).
- Control fairness with **prefetch** (how many unacked messages one worker may hold).

## 3. Publish / Subscribe

```
                         ┌──► [ Sub: Email ]     ──► Email service
Publisher ──► ( Topic ) ─┼──► [ Sub: Inventory ] ──► Inventory service
                         └──► [ Sub: Analytics ] ──► Analytics service
```

- Every **subscription** gets its **own copy**.
- Used for **events** / fan-out.
- Each subscription can itself have competing consumers (3 Email instances share the Email subscription).

> **Combination to remember:** *pub/sub between services, competing consumers inside a service.*

## 4. Routing / Content-based routing

The broker decides **where** a message goes based on its properties.

| Broker | Mechanism |
|--------|-----------|
| RabbitMQ | Exchange types: **direct** (exact routing key), **topic** (patterns `order.*.eu`, `order.#`), **fanout** (everyone), **headers** |
| Azure Service Bus | Subscription **filters**: SQL filter (`region = 'EU'`), correlation filter |
| Kafka | No broker-side routing; consumer filters, or separate topics |
| AWS SNS | Subscription **filter policies** |

## 5. Request / Reply over messaging

```
Client ──(Request, ReplyTo=replies.client1, CorrelationId=X)──► [ requests ] ──► Server
Client ◄──(Reply, CorrelationId=X)──────────── [ replies.client1 ] ◄────────── Server
```

- Needed when you want async transport but still need an answer (e.g. legacy integration, long-running jobs with results).
- The client must match replies by **CorrelationId** and handle **timeouts**.
- Often a sign HTTP/gRPC would be simpler. Use it deliberately.

## 6. Queue vs Log (stream) — the big mental model

This is the most important distinction between "classic" brokers and streaming platforms.

| | **Queue** (RabbitMQ, Service Bus, SQS) | **Log / Stream** (Kafka, Event Hubs, Kinesis) |
|--|---------------------------------------|----------------------------------------------|
| Storage | Message deleted after ack | Messages kept for a **retention** period |
| Reading | Broker pushes; tracks per-message state | Consumer **pulls** and tracks its **offset** |
| Re-read history | No | **Yes — replay** from any offset |
| Many readers | Need one queue per subscriber | Any number of consumer groups read the same log |
| Ordering | Per queue (weakened by competing consumers) | Per **partition** |
| Per-message features | Rich: TTL, priority, delay, DLQ, per-message ack | Minimal: it's a log, you commit positions |
| Scaling consumers | Add consumers freely | Max parallelism = **number of partitions** |
| Sweet spot | Commands, task distribution, business workflows | Event streaming, high throughput, analytics, event sourcing |

```
Queue:   [m5][m4][m3]  →  consumer takes m3, acks → m3 is gone
Log:     [m1][m2][m3][m4][m5]   group A at offset 4, group B at offset 1 — nothing is deleted on read
```

> **Interview one-liner:** "A queue is a to-do list — items disappear when done. A log is a journal — entries stay and every reader keeps its own bookmark."

## 7. Push vs Pull

| | Push | Pull |
|--|------|------|
| Who drives | Broker sends to consumer | Consumer asks for messages |
| Examples | RabbitMQ `basic.consume`, Service Bus processor (SDK hides it) | Kafka `poll`, SQS `ReceiveMessage` |
| Flow control | Prefetch / credit limits | Natural — consumer asks when ready |

## 8. In-process vs out-of-process

- **In-process messaging** (MediatR, `System.Threading.Channels`, in-memory event bus): no broker, no durability, same transaction. Good for decoupling *inside* one app.
- **Out-of-process** (a real broker): durability, cross-service, independent scaling.

---

## Interview one-liners

- "Queues for commands and work distribution; topics for events and fan-out."
- "Competing consumers scale throughput but break global ordering."
- "Kafka is a log: consumers track offsets and can replay. RabbitMQ/Service Bus are queues: messages are deleted on ack but you get rich per-message features."

## Check yourself

1. You have 3 instances of the Email service. How do you ensure each event is emailed once, not three times?
2. Why can't you have 10 active Kafka consumers in one group on a topic with 4 partitions?
3. When is request/reply over a broker a good idea?
4. Draw a RabbitMQ topic exchange that routes `order.created.eu` to an EU queue and all `order.*.*` to an audit queue.

<div dir="rtl" lang="fa">

**خلاصه:** صف (Point-to-Point): هر پیام به یک مصرف‌کننده. Competing Consumers: چند نمونه از یک سرویس روی یک صف برای مقیاس‌پذیری (ولی ترتیب از بین می‌رود). Pub/Sub: هر Subscription یک کپی می‌گیرد (برای ایونت‌ها). تفاوت مهم: صف پیام را بعد از Ack حذف می‌کند، ولی Log (مثل Kafka) پیام‌ها را نگه می‌دارد و هر گروه مصرف‌کننده Offset خودش را دارد و می‌تواند دوباره بخواند (Replay).

</div>
