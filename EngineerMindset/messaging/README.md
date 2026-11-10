# Messaging in Software Design — Learning Plan

A structured path to really understand messaging: **why** we use it, **when** (and when not), **how** it works, and the **deep concepts** that come up in interviews, system design, and real production systems.

The folder has three parts:

| Part | What it is | How to use it |
|------|-----------|---------------|
| [concepts/](concepts/) | Theory, terms, definitions, trade-offs — from basic to advanced | Read in order. Each file ends with interview one-liners and a short Persian summary. |
| [training/](training/) | Hands-on labs. You create empty projects, install tools, write code, break things on purpose | Do them in order. Each lab builds on the previous one. |
| [Messaging-Outbox-Pattern.md](Messaging-Outbox-Pattern.md) | Older notes (Outbox, CDC, Service Bus quick map) | Archive / extra reading. The concepts are re-covered in a cleaner way in `concepts/`. |

---

## The roadmap

### Level 1 — Foundations (the "why" and the vocabulary)

| # | Topic | You should be able to... |
|---|-------|--------------------------|
| 01 | [Why Messaging?](concepts/01-Why-Messaging.md) | Explain sync vs async, coupling, and when **not** to use messaging |
| 02 | [Building Blocks](concepts/02-Building-Blocks.md) | Name every component: producer, consumer, broker, queue, topic, exchange, subscription, envelope, headers |
| 03 | [Message Types](concepts/03-Message-Types.md) | Tell command vs event vs query; domain vs integration events; thin vs fat events |
| 04 | [Communication Models](concepts/04-Communication-Models.md) | Choose between point-to-point, pub/sub, competing consumers, request-reply, queue vs log |

### Level 2 — Reliability (the part interviews love)

| # | Topic | You should be able to... |
|---|-------|--------------------------|
| 05 | [Delivery Guarantees & Idempotency](concepts/05-Delivery-Guarantees-Idempotency.md) | Explain ack/lock, at-most/at-least/exactly-once, and build an idempotent consumer |
| 06 | [Failures, Retries & Dead Letters](concepts/06-Failures-Retries-DLQ.md) | Design retry policies (backoff, jitter), handle poison messages, operate a DLQ |
| 07 | [Ordering, Partitioning & Concurrency](concepts/07-Ordering-Partitioning-Concurrency.md) | Explain why ordering is hard, partitions/sessions, consumer groups, rebalancing |

### Level 3 — Distributed design (professional level)

| # | Topic | You should be able to... |
|---|-------|--------------------------|
| 08 | [Consistency: Dual Write, Outbox, Inbox](concepts/08-Consistency-Outbox-Inbox.md) | Explain the dual-write problem and solve it without distributed transactions |
| 09 | [Sagas & Long-Running Workflows](concepts/09-Sagas-Workflows.md) | Design a saga (choreography vs orchestration) with compensations |
| 10 | [Message Contracts & Evolution](concepts/10-Message-Contracts-Evolution.md) | Version messages safely, explain schema registry, CloudEvents, claim check |
| 11 | [Scaling, Backpressure & Performance](concepts/11-Scaling-Backpressure-Performance.md) | Reason about throughput, latency, lag, batching, prefetch, autoscaling |
| 12 | [Observability, Security & Operations](concepts/12-Observability-Security-Operations.md) | Trace a message across services, secure a broker, define alerts |

### Level 4 — Architecture & choices (senior / architect level)

| # | Topic | You should be able to... |
|---|-------|--------------------------|
| 13 | [Event-Driven Architecture](concepts/13-Event-Driven-Architecture.md) | Explain EDA, event streaming, event sourcing vs event-driven, CQRS |
| 14 | [Choosing a Broker](concepts/14-Choosing-a-Broker.md) | Compare RabbitMQ, Kafka, Azure Service Bus, Event Hubs, Event Grid, SQS/SNS… and justify a choice |
| 15 | [Interview Questions & System Design](concepts/15-Interview-Questions.md) | Answer common questions and walk through messaging system-design scenarios |

---

## How to study (suggested rhythm)

1. Read one concept file (Level 1 files are short; Level 3 files take longer).
2. Do the matching training lab (see table below) — **typing the code yourself** matters more than reading it.
3. Close everything and explain the concept out loud in 2 minutes (as if in an interview).
4. Come back a week later and answer the "Check yourself" questions at the end of the file.

### Concepts ↔ Labs map

| Lab | Practices concepts |
|-----|--------------------|
| [Lab 00 — Setup](training/00-Setup.md) | Tools only |
| [Lab 01 — RabbitMQ Fundamentals](training/01-RabbitMQ-Fundamentals.md) | 02, 03, 04, 05 (ack) |
| [Lab 02 — Failures, Retries, DLQ, Idempotency](training/02-Failures-Retries-Idempotency.md) | 05, 06 |
| [Lab 03 — Reliable Orders: Outbox + Inbox](training/03-Outbox-Inbox.md) | 05, 08 |
| [Lab 04 — Kafka: Partitions, Groups, Replay](training/04-Kafka-Streaming.md) | 04, 07, 11, 13 |
| [Lab 05 — Azure Service Bus](training/05-Azure-Service-Bus.md) | 04, 06, 07, 14 |
| [Lab 06 — Choreography Saga](training/06-Saga-Choreography.md) | 09, 12 |
| [Lab 07 — Capstone](training/07-Capstone.md) | Everything |

---

## Progress checklist

Copy this into your notes and tick as you go.

```
Level 1   [ ] 01  [ ] 02  [ ] 03  [ ] 04      Labs  [ ] 00  [ ] 01
Level 2   [ ] 05  [ ] 06  [ ] 07              Labs  [ ] 02
Level 3   [ ] 08  [ ] 09  [ ] 10  [ ] 11  [ ] 12   Labs  [ ] 03  [ ] 04  [ ] 05  [ ] 06
Level 4   [ ] 13  [ ] 14  [ ] 15              Labs  [ ] 07 (capstone)
```

<div dir="rtl" lang="fa">

**نقشه راه:** اول مفاهیم پایه (چرا پیام‌رسانی، اجزا، انواع پیام، مدل‌های ارتباطی)، بعد قابلیت اطمینان (تضمین تحویل، Idempotency، Retry و DLQ، ترتیب پیام‌ها)، بعد طراحی توزیع‌شده (Outbox، Saga، نسخه‌بندی پیام، مقیاس‌پذیری، مانیتورینگ) و در نهایت معماری (EDA، انتخاب بروکر، سوالات مصاحبه). هر مفهوم را با یک تمرین عملی در پوشه training تمرین کن.

</div>
