# 08 — Consistency: Dual Write, Outbox, Inbox

> **Level 3 — Distributed design.** How to change your database **and** publish a message without losing either. (A cleaner version of the topic in [Messaging-Outbox-Pattern.md](../Messaging-Outbox-Pattern.md).)

---

## 1. The dual-write problem

```csharp
await db.SaveChangesAsync();          // 1. write to DB
await bus.PublishAsync(orderPlaced);  // 2. write to broker
```

Two different systems, **no shared transaction**:

| Failure | Result |
|---------|--------|
| 1 succeeds, 2 fails (broker down, process crash) | Order exists, nobody is told → **lost event** |
| Publish first, then DB fails | Event says "order placed" but there's no order → **ghost event** |
| Publish inside the DB transaction, then rollback | Same ghost event |

### Why not a distributed transaction (2PC / XA / MSDTC)?

- Most modern brokers (Kafka, Service Bus with most setups, cloud services) **don't support** joining a 2PC with your DB.
- 2PC is slow, holds locks, and the coordinator is a single point of failure.
- **Availability** suffers: if any participant is down, nothing commits.

> **Interview one-liner:** "Dual writes are unsafe and 2PC isn't practical with brokers, so I make the database the single source of truth and publish *from* it — the Transactional Outbox."

---

## 2. Transactional Outbox

**Idea:** write the business change **and** the message into the **same database**, in the **same local transaction**. A separate process publishes it later.

```
API ──► BEGIN TX
          INSERT Orders (...)
          INSERT Outbox (Id, Type, Payload, OccurredAt, ProcessedAt = NULL)
        COMMIT
                    │
      Outbox relay (poller / CDC)
                    │  read unprocessed rows
                    ▼
               Broker.Publish  ──► consumers
                    │  on confirm
                    ▼
        UPDATE Outbox SET ProcessedAt = now   (or DELETE)
```

### Outbox table (typical)

| Column | Purpose |
|--------|---------|
| `Id` (GUID) | Becomes the **MessageId** → consumers can dedup |
| `Type` | Message type (`OrderPlaced.v1`) |
| `Payload` | Serialized body (JSON) |
| `OccurredAt` | When it was created; publish in this order |
| `ProcessedAt` | NULL = not yet published |
| `Attempts`, `Error` | For diagnostics |
| (optional) `CorrelationId`, `TraceParent`, `PartitionKey` | Carry context through the relay |

### Guarantees

- **No lost events:** if the TX commits, the outbox row exists and will eventually be published.
- **No ghost events:** if the TX rolls back, the outbox row is gone too.
- **Duplicates are still possible:** relay publishes, crashes before marking processed → publishes again. That's why consumers must be **idempotent** (and why `Outbox.Id` is the MessageId).

### The relay: polling vs CDC

| Relay | How | Pros | Cons |
|-------|-----|------|------|
| **Polling publisher** | Background worker: `SELECT TOP 100 ... WHERE ProcessedAt IS NULL ORDER BY OccurredAt` every N ms | Simple, works everywhere | Latency = poll interval; DB load; multi-instance relay needs locking (`UPDLOCK, READPAST` / `FOR UPDATE SKIP LOCKED`) |
| **Transaction log tailing (CDC)** | Read the DB's transaction log (SQL Server CDC, Postgres logical replication) e.g. via **Debezium** | Near real-time, no polling load | More infrastructure (Kafka Connect/Debezium), operational complexity |

### Ordering with an outbox
- Publish in `OccurredAt`/sequence order.
- With multiple relay instances, ordering per aggregate needs partitioning (e.g. one relay owns a set of aggregate IDs) or a single relay.

### Frameworks that implement it
MassTransit (EF Core outbox), NServiceBus (outbox), Wolverine, CAP (DotNetCore.CAP), Debezium outbox event router. Know that they exist; know how to build it by hand (see Lab 03).

---

## 3. Inbox (idempotent receiver)

The mirror image on the consumer side:

```
on message M:
  BEGIN TX
    INSERT Inbox (MessageId = M.Id)       ← unique constraint; duplicate → violation → skip
    apply business change
  COMMIT
  ACK
```

Variants:
- **Dedup-only inbox:** just a processed-IDs table (as above).
- **Store-then-process inbox:** first persist the raw message to the Inbox and ack the broker immediately; a local worker processes inbox rows. Decouples broker lock time from processing time.

> **Outbox** protects the **producer** side (no lost/ghost events). **Inbox** protects the **consumer** side (no double processing). Together: an *effectively-once* pipeline.

---

## 4. Alternatives & related ideas

| Approach | Notes |
|----------|-------|
| **Listen to yourself** | Publish the event first; your own service consumes it to update its DB. Single write (to the broker). Works, but your own read model is eventually consistent. |
| **Event sourcing** | The event store **is** the database. Events are the state; publishing = reading the store. No dual write by design. (See [13](13-Event-Driven-Architecture.md).) |
| **CDC without outbox** | Stream raw table changes. Easy, but leaks your **internal schema** as the public contract — prefer outbox tables for integration events. |
| **Best-effort publish + reconciliation** | Publish after commit, and a nightly job finds missed events. Sometimes good enough. |

---

## 5. Eventual consistency vocabulary

| Term | Meaning |
|------|---------|
| **Strong consistency** | Every read sees the latest write. |
| **Eventual consistency** | If no new writes happen, all replicas/services **eventually** agree. |
| **Read-your-writes** | A user sees their own change immediately (often solved in the UI or by reading from the write side). |
| **Consistency boundary** | Inside one aggregate/service DB → strong. Across services → eventual. |

---

## Interview one-liners

- "Outbox: business row and message row in one local transaction; a relay publishes later via polling or CDC. No lost events, no ghost events — duplicates are still possible, so consumers are idempotent."
- "The Outbox Id doubles as the MessageId so consumers can deduplicate with an Inbox table."
- "I avoid 2PC: brokers rarely support it, and it hurts availability."

## Check yourself

1. Why does the outbox still produce duplicates?
2. Two relay instances poll the same outbox. How do you stop them from publishing the same row twice?
3. Why is raw CDC of business tables a bad public contract?
4. Draw the full flow: API → Outbox → Relay → Broker → Inbox → Consumer DB.

<div dir="rtl" lang="fa">

**خلاصه:** نوشتن هم‌زمان در دیتابیس و بروکر (Dual Write) ناامن است چون تراکنش مشترک ندارند و 2PC هم عملی نیست. راه‌حل Outbox: رکورد تجاری و رکورد پیام را در یک تراکنش محلی ذخیره کن؛ یک پروسه جدا (Polling یا CDC/Debezium) پیام را منتشر می‌کند. این روش گم‌شدن پیام و پیام شبح را حذف می‌کند ولی تکرار ممکن است؛ پس سمت مصرف‌کننده Inbox (جدول MessageIdهای پردازش‌شده با Unique Constraint در همان تراکنش) لازم است.

</div>
