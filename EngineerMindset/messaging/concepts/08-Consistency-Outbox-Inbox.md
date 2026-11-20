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

**What is 2PC?** *Two-Phase Commit* is a protocol that makes several separate systems (e.g. your database **and** the message broker) commit **all together or not at all**. A **coordinator** runs it in two steps:

1. **Prepare (vote):** the coordinator asks every participant "can you commit?" Each one does the work, locks the data, and answers *yes* or *no*.
2. **Commit / Rollback:** if **everyone** said yes, the coordinator tells all of them to commit; if **anyone** said no (or didn't answer), it tells all of them to roll back.

**XA** is the standard interface for this protocol, and **MSDTC** (Microsoft Distributed Transaction Coordinator) is Windows' implementation of the coordinator.

**Who takes part, and who is the coordinator?**

- **Every participant must support the protocol.** Each system has to be able to "prepare" (hold the change in a pending, locked state) and then wait for the final commit/rollback. If even one system can't, 2PC is impossible.
  - SQL Server, Oracle, PostgreSQL, MySQL → **yes** (they can join XA / MSDTC transactions).
  - **MSMQ**, IBM MQ, ActiveMQ → **yes**.
  - **RabbitMQ, Kafka, Azure Service Bus (with a DB), AWS SQS** → **no**. (RabbitMQ's channel transactions and Kafka transactions only cover the broker itself, not your database.)
- **The coordinator is *not* your code.** It is a separate component called a **transaction manager**: MSDTC on Windows/.NET, or a JTA manager (Atomikos, Narayana) in Java. Your code only opens and completes the transaction; the transaction manager talks to the participants and runs the two phases.

**Simple scenario — placing an order with SQL Server + MSMQ (both support MSDTC):**

```csharp
using (var scope = new TransactionScope())          // your code: "start a transaction"
{
    db.Orders.Add(order);
    db.SaveChanges();                                // participant 1: SQL Server joins

    queue.Send(new OrderPlaced(order.Id),
               MessageQueueTransactionType.Automatic); // participant 2: MSMQ joins
                                                       // → two resources, so .NET hands
                                                       //   the transaction to MSDTC

    scope.Complete();                                // your code: "I'm done, please commit"
}                                                    // MSDTC now runs the two phases:
```

1. **Prepare:** MSDTC asks SQL Server "ready?" → *yes* (order row written but locked). Asks MSMQ "ready?" → *yes* (message stored but invisible).
2. **Commit:** both said yes → MSDTC tells both to commit. The order and the message appear **together**.
   - If MSMQ had said *no* (disk full, service down), MSDTC would tell SQL Server to roll back → **no order, no message**. No lost or ghost event.

**Now swap MSMQ for RabbitMQ:** RabbitMQ can't join the MSDTC transaction, so the `TransactionScope` only covers SQL Server and you are back to the dual-write problem above. That is why the outbox pattern exists.

So 2PC sounds like the perfect fix for dual writes, but in practice:

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

### The problem: the same message can arrive twice

The outbox gives you **at-least-once** delivery, so duplicates *will* happen. For example:

- The outbox relay publishes, crashes before marking the row processed, and publishes again.
- The consumer processes the message, crashes **before sending the ACK**, and the broker redelivers it.

**Scenario:** the Billing service receives `OrderPlaced { MessageId = 42, OrderId = 7 }` and charges the customer €50. If message 42 arrives twice, the customer is charged **€100**. 😱

### The idea: keep a "receipt book"

Think of a cashier who writes every paid invoice number into a receipt book. When an invoice shows up, the cashier first checks the book: *"Already paid? Then ignore it."*

The **Inbox** is that receipt book: a table of the `MessageId`s this consumer has **already processed**.

```sql
CREATE TABLE Inbox (
    MessageId   UNIQUEIDENTIFIER PRIMARY KEY,   -- unique: the same id can't be stored twice
    ProcessedAt DATETIME2 NOT NULL
);
```

### The key trick: same transaction

Recording the receipt and doing the work must happen in **one local DB transaction**, so they succeed or fail together:

```csharp
async Task Handle(OrderPlaced msg)
{
    using var tx = await db.Database.BeginTransactionAsync();

    if (await db.Inbox.AnyAsync(x => x.MessageId == msg.MessageId))
        return;                                   // already processed → just ACK, do nothing

    db.Inbox.Add(new InboxRow(msg.MessageId));    // 1. write the receipt
    db.Payments.Add(Charge(msg.OrderId, 50));     // 2. do the business work

    await db.SaveChangesAsync();                  // the PRIMARY KEY blocks a parallel duplicate
    await tx.CommitAsync();
}   // the broker is ACKed after Handle returns
```

### Walk through the failure cases

| What happens | Result |
|---|---|
| Message 42 arrives the first time | No receipt → charge €50, store receipt 42 → ACK ✅ |
| Crash **before COMMIT** | Both the charge and the receipt roll back → redelivery processes it cleanly ✅ |
| Crash **after COMMIT, before ACK** | Broker redelivers 42 → receipt found → skip → ACK ✅ (charged once) |
| Two instances receive 42 **at the same time** | Both try to insert receipt 42 → the PK lets only one commit; the other fails and on retry sees the receipt ✅ |

**Why not just check and then act without a transaction?** If you charge the customer and crash before saving the receipt, the redelivered message finds no receipt and charges again. Tying the receipt and the work into one transaction closes that gap. It's the same trick as the outbox, used on the receiving side.

> **Note:** this only protects work done **in the same database**. If the handler calls an external API (for example a payment provider), pass `MessageId` as that API's **idempotency key** too.

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
