# 05 — Delivery Guarantees & Idempotency

> **Level 2 — Reliability.** The single most asked messaging topic in interviews. If you master one file, master this one.

---

## 1. Acknowledgements — how the broker knows you're done

| Term | Meaning |
|------|---------|
| **Ack / Complete** | "Processed OK — remove it." |
| **Nack / Abandon / Reject** | "Failed — give it back (retry) or drop / dead-letter it." |
| **Auto-ack** | Broker considers it done **as soon as it's sent** to you. Fast, but if you crash mid-processing the message is **lost**. |
| **Manual ack** | You ack **after** processing. Safe, but if you crash after processing and before ack → **redelivery**. |
| **Lock / visibility timeout / lease** | While you hold a message, others can't see it. If you don't ack before the lock expires → it becomes visible again → **another consumer gets it** (duplicate). |
| **Offset commit** (Kafka) | You don't ack messages; you commit "I've processed up to offset N" for a partition. |

### Producer side, too

Delivery can fail **before** the broker even has the message.

| Term | Meaning |
|------|---------|
| **Publisher confirms** (RabbitMQ) | Broker confirms it stored the message. Without it, a publish can be silently lost. |
| **acks=all** (Kafka) | Broker confirms after all in-sync replicas have the message. |
| **Send await** (Service Bus SDK) | `await SendMessageAsync` completes after the broker accepted it. |

> A producer that doesn't wait for a confirm is effectively **at-most-once**, no matter what the consumer does.

---

## 2. The three delivery guarantees

| Guarantee | Meaning | How it happens | Risk | Typical use |
|-----------|---------|----------------|------|-------------|
| **At-most-once** | 0 or 1 times | Ack **before** processing (or auto-ack, or no publisher confirm, no retry) | **Loss** | Metrics, telemetry, non-critical logs, live video frames |
| **At-least-once** | 1 or more times | Ack **after** processing; retry on failure | **Duplicates** | Default for business messaging |
| **Exactly-once** | Exactly 1 time | Very hard end-to-end | Cost, complexity | "Exactly-once **effect**" via idempotency |

### Where duplicates come from (at-least-once)

1. Consumer processes, then **crashes before ack** → redelivered.
2. Processing takes **longer than the lock** → lock expires → second consumer gets it.
3. Producer **retries a send** because the confirm was lost (the broker actually stored it) → two copies.
4. Kafka **rebalance** before the offset was committed → new owner re-reads.
5. Network partition: ack sent but never reached the broker.

### Why true exactly-once is (almost) impossible

The **Two Generals problem**: over an unreliable network, the sender can never be 100% sure the other side received the last confirmation. So either you **retry** (risk duplicate) or you **don't** (risk loss).

What exists in practice:

- **Kafka transactions / exactly-once semantics (EOS):** exactly-once **within Kafka** (read from topic → process → write to topic + commit offset atomically). The moment you touch an external system (DB, email, HTTP API), you're back to at-least-once.
- **Broker deduplication windows** (Service Bus duplicate detection, SQS FIFO): broker drops a re-sent message with the same `MessageId` within a time window. Helps on the **producer** side only.

> **Interview one-liner:** "Exactly-once delivery isn't realistic across systems. We design for **at-least-once delivery + idempotent processing**, which gives an exactly-once *effect*."

---

## 3. Idempotency

**Definition:** processing the same message N times has the **same effect** as processing it once.

See also: [Idempotent](../../general-programming/Idempotent.md) (HTTP angle).

### Naturally idempotent vs not

| Operation | Idempotent? |
|-----------|-------------|
| `SET status = 'Shipped' WHERE id = 5` | ✅ Yes |
| `UPDATE accounts SET balance = balance - 10` | ❌ No — every duplicate subtracts again |
| `INSERT INTO orders (id, ...) VALUES (1234, ...)` with PK | ✅ Effectively (second insert fails on PK) |
| `INSERT INTO orders (...)` with auto-increment id | ❌ No — two orders |
| Send email | ❌ No — user gets two emails |
| `DELETE WHERE id = 5` | ✅ Yes |

### Techniques (from simple to robust)

**1. Make the operation itself idempotent**
- Use absolute values instead of increments (`SET balance = 90` with a version check, rather than `balance - 10`).
- Use **natural/business keys** + unique constraints (`orderId` as PK, not an identity).
- **Conditional updates:** `UPDATE orders SET status='Paid' WHERE id=@id AND status='Pending'`.
- Optimistic concurrency with a **version / rowversion** column.

**2. Idempotent consumer with a "processed messages" store (Inbox / dedup table)**

```
on message M:
  BEGIN TRANSACTION
    if exists ProcessedMessages where Id = M.MessageId → COMMIT, ACK, return   (duplicate)
    do business work (update DB)
    INSERT ProcessedMessages (Id = M.MessageId, ProcessedAt = now)
  COMMIT
  ACK
```

- The **business change and the dedup record must be in the same transaction**. Otherwise: crash between them → either you lose the record (process twice) or you record without doing the work.
- A **unique constraint** on `ProcessedMessages.Id` makes concurrent duplicates safe (the second insert fails → rollback).
- Clean old rows periodically (keep longer than the max redelivery window).

**3. Idempotency keys for external side effects**
- Payment providers (Stripe etc.) accept an **Idempotency-Key** header: send the MessageId; the provider returns the same result for a repeated call.
- For emails/SMS: record "email sent for order X" before/after sending, accepting a tiny window, or use a provider that supports dedup keys.

### Which ID to deduplicate on?

| Option | Notes |
|--------|------|
| **MessageId** (set by producer) | Good if the producer sets a **stable** id (e.g. generated once and stored in the Outbox). A retrying producer must reuse it. |
| **Business key** (`OrderId` + event type) | Good when "process once per order" is the real rule. |
| Broker-assigned id | ❌ Avoid — a re-sent message gets a new broker id. |

---

## 4. Putting it together

```
Producer:  Outbox (no loss on producer side) + stable MessageId
Broker:    durable / replicated, publisher confirms
Consumer:  manual ack AFTER processing + idempotent handler (dedup in same transaction)
Failures:  retries with backoff → DLQ
```

This combination is what most real systems mean by "reliable messaging".

---

## Interview one-liners

- "Ack after processing gives at-least-once; ack before processing gives at-most-once."
- "Duplicates come from crashes before ack, lock expiry, producer retries and rebalances — so every consumer must be idempotent."
- "I dedup on a stable MessageId, recorded in the same DB transaction as the business change, with a unique constraint."

## Check yourself

1. Your consumer takes 90s, lock duration is 60s. What happens? Two fixes?
2. Why must the dedup insert and the business update be in one transaction?
3. Kafka offers "exactly-once". Why doesn't that make your email sender exactly-once?
4. Make `balance = balance - 10` idempotent. Give two different ways.

<div dir="rtl" lang="fa">

**خلاصه:** At-most-once: قبل از پردازش Ack می‌کنی → ممکن است پیام گم شود (مناسب متریک و لاگ). At-least-once: بعد از پردازش Ack → ممکن است پیام تکراری برسد (پیش‌فرض سیستم‌های تجاری). Exactly-once واقعی روی شبکه عملاً ممکن نیست؛ در عمل «اثر یک‌باره» با At-least-once + مصرف‌کننده Idempotent می‌سازیم: MessageId را در همان تراکنشی که تغییر تجاری را ذخیره می‌کنیم، در جدول پیام‌های پردازش‌شده با Unique Constraint ثبت می‌کنیم.

</div>
