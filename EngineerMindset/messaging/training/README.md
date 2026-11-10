# Messaging Training — Hands-on Labs

Step-by-step labs: you create empty projects, install the tools, type the code, run it, and **break it on purpose** to see the concepts happen for real.

> These files are **guides only**. Create your lab projects **outside this repository** (for example `C:\Labs\messaging\`). Every time you come back, you can start from an empty folder and do it again — that repetition is the point.

---

## Labs

| Lab | Topic | Time | Broker | Concepts |
|-----|-------|------|--------|----------|
| [00 — Setup](00-Setup.md) | Install tools, prepare workspace | 30 min | — | — |
| [01 — RabbitMQ Fundamentals](01-RabbitMQ-Fundamentals.md) | Queues, ack, durability, competing consumers, pub/sub, routing | 2–3 h | RabbitMQ | [02](../concepts/02-Building-Blocks.md), [03](../concepts/03-Message-Types.md), [04](../concepts/04-Communication-Models.md) |
| [02 — Failures, Retries, DLQ, Idempotency](02-Failures-Retries-Idempotency.md) | Poison messages, delivery limit, retry queue, duplicates | 2 h | RabbitMQ | [05](../concepts/05-Delivery-Guarantees-Idempotency.md), [06](../concepts/06-Failures-Retries-DLQ.md) |
| [03 — Reliable Orders: Outbox + Inbox](03-Outbox-Inbox.md) | Dual write, transactional outbox, idempotent consumer with a DB | 3–4 h | RabbitMQ + SQLite | [08](../concepts/08-Consistency-Outbox-Inbox.md) |
| [04 — Kafka: Partitions, Groups, Replay](04-Kafka-Streaming.md) | Keys, partitions, ordering, consumer groups, offsets, lag, replay | 2–3 h | Kafka | [07](../concepts/07-Ordering-Partitioning-Concurrency.md), [11](../concepts/11-Scaling-Backpressure-Performance.md), [13](../concepts/13-Event-Driven-Architecture.md) |
| [05 — Azure Service Bus](05-Azure-Service-Bus.md) | Queues, topics + filters, DLQ, sessions, scheduling, dedup | 2–3 h | Service Bus (emulator or Azure) | [06](../concepts/06-Failures-Retries-DLQ.md), [07](../concepts/07-Ordering-Partitioning-Concurrency.md), [14](../concepts/14-Choosing-a-Broker.md) |
| [06 — Choreography Saga](06-Saga-Choreography.md) | Multi-service workflow with compensations and correlation | 3–4 h | RabbitMQ | [09](../concepts/09-Sagas-Workflows.md), [12](../concepts/12-Observability-Security-Operations.md) |
| [07 — Capstone](07-Capstone.md) | Build a full system yourself from a spec | 1–2 days | Your choice | All |

---

## How each lab is structured

Every lab follows the same shape:

1. **Goal** — what you'll build and which concepts you'll *see*.
2. **Steps** — numbered. Each step has: what to do → code → run → **what you should see** → why.
3. **🔨 Break it** — experiments where you cause failures on purpose (kill a process, stop the broker, send duplicates).
4. **✅ Checkpoint** — questions to answer before moving on. If you can't answer, re-read the linked concept.
5. **🧹 Cleanup** — stop containers, delete folders.

## Rules for real learning

- **Type the code**, don't copy-paste blocks blindly. Small typos teach you how the APIs work.
- **Predict before you run.** Write down what you expect, then compare.
- **Keep a lab journal** (a notes file in your lab folder): what surprised you, errors you hit, how you fixed them.
- After finishing a lab, **delete it and redo it from memory** a week later — only peeking when stuck.

## Library versions

The labs were written for **.NET 10**, **RabbitMQ.Client 7.x**, **Confluent.Kafka 2.x**, **Azure.Messaging.ServiceBus 7.x**, **EF Core 10**. If an API name doesn't compile, check the package's official docs/tutorials — names occasionally change between major versions. Fixing that yourself is part of the exercise.

<div dir="rtl" lang="fa">

**راهنما:** این‌ها فقط راهنمای تمرین هستند؛ پروژه‌ها را بیرون از این مخزن (مثلاً در C:\Labs\messaging) بساز. تمرین‌ها را به ترتیب انجام بده، کد را خودت تایپ کن، قبل از اجرا نتیجه را پیش‌بینی کن، و در بخش «Break it» عمداً سیستم را خراب کن تا مفاهیم (تکرار پیام، گم‌شدن پیام، DLQ و ...) را واقعاً ببینی. بعد از یک هفته، تمرین را از اول و از حافظه دوباره انجام بده.

</div>
