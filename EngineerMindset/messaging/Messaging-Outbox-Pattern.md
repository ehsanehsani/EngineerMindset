# Messaging & Azure Service Bus (Interview)

Core messaging concepts + the classic “save to DB then publish event” problem.

---

## The interview scenario (start here)

**Question:** An API writes to the database, then publishes an event. What can go wrong?

```
API → INSERT into DB → Publish to Service Bus / Kafka
```

**Problem:** DB and message broker are **two separate systems**. There is no single ACID transaction across both.

| Failure | Result |
|---------|--------|
| DB succeeds, publish fails | Data saved, no event → other services never react |
| Publish succeeds, DB rolls back | Event sent, no data → consumers act on something that “never happened” |
| Publish succeeds, consumer crashes after processing but before Ack | Message redelivered → **duplicate** processing |

Your instinct (transaction / rollback) is right about the risk. Industry solutions usually do **not** try to roll back the DB from the broker. They use patterns like **Transactional Outbox** (+ polling or CDC) so the write and the “intent to publish” are in the **same DB transaction**.

<div dir="rtl" lang="fa">

**خلاصه:** دیتابیس و بروکر یک تراکنش مشترک ندارند. یا دیتا ذخیره می‌شود و ایونت نمی‌رود، یا برعکس. راه‌حل رایج: Outbox + Polling یا CDC — نه رول‌بک از روی بروکر.

</div>

### Industry solution: Transactional Outbox

<div dir="rtl" lang="fa">

**خلاصه:**  
به‌جای اینکه مستقیم در دیتابیس ذخیره کنی و هم‌زمان ایونت بفرستی (که ممکن است یکی موفق شود و دیگری نه)، هر دو را در **یک تراکنش دیتابیس** می‌نویسی: رکورد اصلی (مثلاً سفارش) + یک رکورد در جدول Outbox برای همان ایونت. بعداً یک پروسه جدا از دیتابیس آن رکورد Outbox را می‌خواند و به بروکر publish می‌کند. این‌طور ذخیره و «قصد ارسال ایونت» با هم یا موفق می‌شوند یا هیچ‌کدام.

</div>

1. In one DB transaction: insert business data **and** insert a row into an `Outbox` table (the event payload).
2. A background process (or CDC) reads the Outbox and publishes to Service Bus / Kafka.
3. After successful publish, mark the Outbox row as sent (or delete it).

```
API ──► [Orders + Outbox]  same DB transaction
              │
              ▼
     Poller / CDC / Debezium
              │
              ▼
        Service Bus / Kafka
              │
              ▼
           Consumers
```

**Polling:** a worker periodically queries `Outbox WHERE Sent = 0`. Simple, reliable, slightly delayed.

**Near real-time (SQL Server):** use **CDC** (or Debezium on top of CDC) instead of hammering the main table — see section below.

**Interview one-liner:** "Dual-write between DB and broker is unsafe. Use an Outbox in the same transaction, then a poller or CDC to publish — that gives reliable events without a distributed transaction."

---

## Queue vs Pub/Sub

| Model | Behavior | Azure example |
|-------|----------|---------------|
| **Point-to-Point (Queue)** | One message → **one** consumer (even if many compete) | Service Bus **Queue** |
| **Pub/Sub (Topic)** | One message → **all** interested subscribers | Service Bus **Topic + Subscriptions** |

**Competing consumers:** several workers on one queue; each message goes to only one.

**Kafka tip:** same Consumer Group ≈ queue (one gets it). Different Consumer Groups ≈ pub/sub (each group gets a copy).

<div dir="rtl" lang="fa">

**صف:** یک پیام، یک مصرف‌کننده. **تاپیک:** یک پیام، همه اشتراک‌ها یک کپی می‌گیرند.

</div>

---

## Delivery guarantees (almost always asked)

| Guarantee | Meaning | Risk / typical use |
|-----------|---------|-------------------|
| **At-most-once** | Send and forget; may never arrive | Message loss. OK for **non-critical** data — e.g. some logs, metrics, telemetry — where speed matters more than never losing a message. |
| **At-least-once** | Delivered ≥ 1 time; may duplicate | Duplicate processing. Default for most business messaging; consumer must be **idempotent**. |
| **Exactly-once** | Ideal: process once only | Hard/expensive on the network |

**Most brokers = at-least-once.** True exactly-once on the wire is basically impractical (Two Generals problem). In practice you get **exactly-once effect** with:

**at-least-once delivery + idempotent consumer**

<div dir="rtl" lang="fa">

**برای مصاحبه:** exactly-once خالص روی شبکه تقریباً غیرممکن یا خیلی گران است. در عمل: تحویل حداقل‌یک‌بار + مصرف‌کننده بدون اثر تکراری. برای لاگ و متریک غیرحیاتی معمولاً at-most-once کافی است.

</div>

### Idempotency (required with at-least-once)

Same message processed twice → same final result as once.

Because at-least-once can deliver the **same message more than once** (e.g. consumer crashes after work but before Ack → broker redelivers), the consumer must detect duplicates.

**Common approach — save MessageId:**
1. Every message has a unique **MessageId** (or business key).
2. Before processing, check a store (DB table / Redis): “Have I already processed this id?”
3. If yes → **skip** (Ack and stop). If no → process, then **save the MessageId**, then Ack.

That way a redelivery does not charge twice, create two orders, send two emails, etc.

Other helpers:
- Prefer conditional updates (`WHERE status <> 'done'`) over blind increments
- Unique constraints so duplicate inserts fail safely
---

## Ack, Lock, and duplicates

| Term | Meaning |
|------|---------|
| **Ack** | “Processed OK — delete/remove from queue” |
| **Nack / abandon** | “Failed — make available again for retry” |
| **Lock / visibility timeout** | Message locked while one consumer works; if no Ack in time → visible again → **duplicate** |

This lock timeout is a main source of at-least-once duplicates.

---

## Dead-Letter Queue (DLQ)

After N failed retries (poison message), move to **DLQ** instead of blocking the main queue forever. Inspect later (manual or tooling).

---

## Ordering

- **Kafka:** order guaranteed **per partition only**. Use a stable key (e.g. `OrderId`) so related messages share a partition.
- **Azure Service Bus:** enable **Sessions** for ordered processing per session id.

---

## Event vs Command vs Message

| Term | Meaning |
|------|---------|
| **Command** | “Do this” — imperative, usually one target (`CreateOrder`) |
| **Event** | “This happened” — past tense (`OrderCreated`); anyone may subscribe |
| **Message** | Generic word for both |

**Fan-out:** one `OrderCreated` → Email + Inventory + Analytics (classic Topic / pub-sub).

---

## Backpressure

Producer faster than consumers → queue grows.

Options: scale out consumers, throttle producer, use a broker that buffers well (e.g. Kafka on disk).

---

## Saga (multi-service “transaction”)

When one business flow spans several services (each with its own DB), you cannot use one ACID transaction.

**Saga:** each step does local work + publishes an event; on failure, run **compensating** actions (logical undo), not a global DB rollback.

Often pairs with Outbox for reliable step events.

---

## SQL Server: CDC instead of heavy polling

**Change Data Capture (CDC)** — built-in. Reads the **transaction log** (not the live business table), writes changes into CDC change tables. Lighter than polling `Orders` every few hundred ms.

```sql
EXEC sys.sp_cdc_enable_db;

EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name   = N'Orders',
    @role_name     = NULL,
    @supports_net_changes = 1;
```

Creates something like `cdc.dbo_Orders_CT`. Operation codes: `1` Delete, `2` Insert, `3` Update (before), `4` Update (after).

**Interview nuance:** CDC alone is still a **pull** (you query change tables). For near real-time **push** to Kafka/Service Bus, teams often add **Debezium** (or similar) on top of CDC:

```
SQL Server (CDC) → Debezium → Kafka / Service Bus → Consumers
```

| Approach | Idea |
|----------|------|
| **Polling Outbox** | Simple worker on `Outbox` table |
| **CDC** | Read log/change tables, less load on main tables |
| **CDC + Debezium** | Near real-time stream to the broker |

**Change Tracking** (lighter cousin): tells you *what* changed, not full before/after values like CDC. Sometimes asked as a comparison.

<div dir="rtl" lang="fa">

**سی‌دی‌سی:** از لاگ تراکنش می‌خواند، نه از جدول اصلی. خودش هنوز pull است؛ برای push نزدیک به لحظه معمولاً با دیبزیوم ترکیب می‌شود.

</div>

---

## Azure Service Bus — quick map

| Feature | Use when |
|---------|----------|
| **Queue** | One work item, one processor |
| **Topic + Subscriptions** | One event, many independent handlers |
| **Sessions** | Ordered processing per key |
| **DLQ** | Poison messages after max delivery |
| **Lock duration** | How long a consumer “owns” a message |

---

## Ready-to-say answers

**Messaging basics:**  
"We use queues for competing consumers and topics for pub/sub. Delivery is usually at-least-once, so consumers must be idempotent. Ordering is per partition or session. Failures after retries go to a DLQ."

**DB + event:**  
"You can't safely commit DB and broker in one transaction. Put the event in an Outbox in the same DB transaction, then publish via a poller or CDC/Debezium. That is the common industry pattern for reliable messaging."

**Exactly-once:**  
"Pure exactly-once over the network is unrealistic. We aim for exactly-once *effect* with at-least-once plus idempotency."
