# 13 — Event-Driven Architecture (EDA)

> **Level 4 — Architecture.** Zooming out: how messaging shapes the whole system. Many terms here sound alike — knowing the differences is a senior-level signal.

---

## 1. What is Event-Driven Architecture?

An architecture where components communicate mainly by **producing and reacting to events** — facts about things that happened. Services don't call each other to make things happen; they announce state changes and others react.

```
Orders ──OrderPlaced──►  ( event backbone: Kafka / Service Bus topics / Event Grid )
                              ├──► Payments
                              ├──► Inventory
                              ├──► Notifications
                              └──► Analytics / Data lake
```

### Martin Fowler's four "event-driven" meanings (classic interview reference)

| Pattern | Meaning |
|---------|---------|
| **Event Notification** | Send a small event, receivers call back for details if needed. Low coupling, chatty. |
| **Event-Carried State Transfer** | Events carry the data; receivers keep their own copy. No callbacks, data duplication. |
| **Event Sourcing** | Store **all state changes as events**; current state = replay of events. |
| **CQRS** | Separate models for **writes (commands)** and **reads (queries)**. |

They are **independent**: you can do EDA without event sourcing, and CQRS without messaging.

---

## 2. Event Sourcing

Instead of storing the current row, store the **sequence of events**:

```
Account 42:
  1  AccountOpened   { owner: "Sara" }
  2  MoneyDeposited  { amount: 100 }
  3  MoneyWithdrawn  { amount: 30 }
  → current balance = 70 (computed by replaying)
```

| Pros | Cons |
|------|------|
| Full audit trail / history for free | Harder to query current state → needs projections (read models) |
| Time travel / rebuild state at any point | Event schema evolution is forever (upcasting) |
| Natural source of integration events (no dual write) | Steeper learning curve, eventual consistency of read side |
| Debugging: replay what happened | Snapshots needed for long streams |

Concepts: **event store** (EventStoreDB/KurrentDB, Marten on Postgres, Cosmos DB change feed), **stream per aggregate**, **projections**, **snapshots**, **optimistic concurrency on stream version**.

> **Trap:** "event sourcing" ≠ "using Kafka". Kafka is a log of events, but event sourcing is a persistence model for an aggregate's state. Kafka is usually **not** a good event store per aggregate (no efficient per-entity stream reads / optimistic concurrency per stream).

---

## 3. CQRS

**Command Query Responsibility Segregation:** writes go through a model optimized for business rules; reads come from models optimized for queries.

```
Command ──► Write model (normalized DB / event store)
                 │ events
                 ▼
            Projections ──► Read models (denormalized tables, Elasticsearch, Redis)
Query  ────────────────────────────────────────► Read model
```

- Messaging is the usual glue that **updates read models** from write-side events.
- Read models are **eventually consistent** → handle "read your own writes" in the UI.
- CQRS can be as simple as separate query classes on the same DB — messaging isn't required.

---

## 4. Event streaming & stream processing

**Event streaming:** treating events as a continuous, durable, replayable stream (Kafka, Event Hubs, Kinesis, Redpanda).

| Concept | Meaning |
|---------|---------|
| **Retention** | Keep events by time/size (days, forever). |
| **Log compaction** | Keep only the **latest** event per key → the topic becomes a "table" of current state. |
| **Replay** | New consumer reads history from the beginning to build its state. |
| **Stream processing** | Continuous computations: filter, map, join, aggregate, **windowing** (tumbling, hopping, session windows). Tools: Kafka Streams, Flink, ksqlDB, Azure Stream Analytics. |
| **Stream–table duality** | A table is a snapshot of a stream; a stream is the changelog of a table. |
| **Event time vs processing time** | When it happened vs when you processed it. Late events and **watermarks**. |

---

## 5. Event-driven building blocks in the cloud (Azure flavor)

| Service | Role |
|---------|------|
| **Event Grid** | Lightweight **event routing** (push, HTTP/webhooks), reacting to resource events (blob created) and custom events. Discrete events. |
| **Event Hubs** | **Event streaming** / telemetry ingestion, Kafka-compatible endpoint. Millions of events/s. |
| **Service Bus** | **Enterprise messaging**: commands, business events, transactions, sessions, DLQ. |
| **Azure Functions** | Serverless handlers triggered by all of the above. |

---

## 6. Pitfalls of EDA

| Pitfall | Description |
|---------|-------------|
| **Event soup / spaghetti** | Too many fine-grained events, nobody knows the end-to-end flow. Document flows (event storming, AsyncAPI, catalogs). |
| **Distributed monolith** | Services must deploy together because of shared contracts or synchronous chains. |
| **Using events as remote procedure calls** | "Events" that expect a specific reaction (`DoThisEvent`). |
| **Leaking internal models** | Publishing DB-shaped events; every schema change breaks consumers. |
| **Ignoring eventual consistency in UX** | Users see stale data with no feedback. |

### Documenting events
- **AsyncAPI** — like OpenAPI, but for message-driven APIs (channels, messages, schemas).
- **Event catalog** — inventory of events, owners, consumers, schemas.
- **Event Storming** — collaborative workshop to discover domain events with business people.

---

## Interview one-liners

- "EDA, event sourcing and CQRS are separate ideas: EDA is about communication, event sourcing is about persistence, CQRS is about separating reads from writes."
- "Kafka is an event streaming platform, not automatically an event store; for event sourcing I'd use a store with per-stream optimistic concurrency."
- "On Azure: Event Grid for routing discrete events, Event Hubs for high-volume streams, Service Bus for business messaging."

## Check yourself

1. Explain the difference between event-driven and event-sourced.
2. What is log compaction and when is it useful?
3. How does CQRS create eventual consistency, and how do you hide it from users?
4. What is AsyncAPI?

<div dir="rtl" lang="fa">

**خلاصه:** در معماری رویدادمحور (EDA) سرویس‌ها با انتشار و واکنش به ایونت‌ها با هم کار می‌کنند. چهار مفهوم جدا: Event Notification، Event-Carried State Transfer، Event Sourcing (ذخیره همه تغییرات به‌صورت ایونت و ساخت وضعیت با Replay) و CQRS (جداسازی مدل نوشتن و خواندن). Kafka پلتفرم Streaming است، نه لزوماً Event Store. در Azure: Event Grid برای مسیریابی ایونت، Event Hubs برای جریان داده حجیم، Service Bus برای پیام‌رسانی تجاری.

</div>
