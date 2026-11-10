# 06 — Failures, Retries & Dead Letters

> **Level 2 — Reliability.** In async systems you can't return a 500 to the user. You need a deliberate strategy for every kind of failure.

---

## 1. Classify the failure first

| Kind | Example | Will retrying help? | What to do |
|------|---------|---------------------|-----------|
| **Transient** | DB timeout, network blip, 503, throttling (429), deadlock | ✅ Yes | Retry with backoff |
| **Permanent / poison** | Invalid JSON, missing required field, unknown message type, bug in handler | ❌ No | Don't retry (or few times) → DLQ |
| **Business rejection** | "Insufficient stock", "card declined" | ❌ No | It's not an error! Publish a business event (`PaymentFailed`) and ack |
| **Downstream outage** | Payment provider down for 30 min | Eventually | Back off longer, circuit breaker, pause consuming |

> **Key insight:** business failures are **normal outcomes**, not exceptions. `PaymentFailed` is an event, not a DLQ entry.

---

## 2. Retry strategies

### Immediate retries
Retry a few times in-process (e.g. 3× within milliseconds). Handles tiny blips. Often done with **Polly** in .NET.

### Exponential backoff
Wait longer between attempts: 1s, 2s, 4s, 8s, 16s… Gives the downstream time to recover.

### Jitter
Add randomness to the delay (`delay = base * 2^n ± random`). Without jitter, 1000 consumers that failed at the same time **retry at the same time** → thundering herd.

### Delayed / scheduled redelivery (second-level retries)
Instead of blocking the consumer thread while waiting, put the message back **with a delay**:
- **Service Bus:** schedule a copy with `ScheduledEnqueueTime`, or abandon and rely on `MaxDeliveryCount`.
- **RabbitMQ:** publish to a **retry queue** with a TTL whose dead-letter target is the original queue ("wait queue" pattern), or the delayed-message plugin.
- **Kafka:** retry topics (`orders.retry.1m`, `orders.retry.10m`) then a dead-letter topic.

```
orders ──fail──► orders.retry (TTL 30s) ──expires──► orders  (try again)
       ──fail N times──► orders.dlq
```

### Retry budget
Always cap retries (count and/or total time). Infinite retries of a poison message = a stuck consumer.

---

## 3. Poison messages & Dead-Letter Queues (DLQ)

A **poison message** fails every time. Without protection it is retried forever, blocks ordered queues, and burns CPU.

A **Dead-Letter Queue** is a separate queue where such messages are moved so normal traffic continues.

| Broker | How dead-lettering works |
|--------|--------------------------|
| **Azure Service Bus** | Each queue/subscription has a built-in `$DeadLetterQueue`. Messages go there after `MaxDeliveryCount` (default 10), on TTL expiry (if enabled), or when you call `DeadLetterMessageAsync(reason)`. |
| **RabbitMQ** | Configure a **dead-letter exchange (DLX)** on the queue. Messages go there when rejected without requeue, expired (TTL), queue length exceeded, or (quorum queues) `x-delivery-limit` exceeded. |
| **Kafka** | No built-in DLQ. You publish failed records to a **dead-letter topic** yourself (frameworks help). |
| **AWS SQS** | Redrive policy: after `maxReceiveCount` → DLQ. |

### Good DLQ hygiene

- Add **why** it failed: reason, exception message, stack trace, original queue, attempt count, timestamp (as headers).
- **Monitor and alert** on DLQ depth > 0. An unwatched DLQ is a silent data-loss bucket.
- Have a **replay / resubmit** process (tooling, script, or portal) after fixing the bug.
- Decide on **retention** — DLQ messages are business data.

---

## 4. Protecting downstream systems

| Pattern | What it does |
|---------|-------------|
| **Circuit breaker** | After many failures, stop calling the downstream for a while ("open"), then test with one call ("half-open"). Avoids hammering a dead service. |
| **Pause consumption** | When a dependency is down, stop pulling messages instead of moving everything to the DLQ. |
| **Bulkhead** | Separate resources (thread pools, queues) per dependency so one failure doesn't starve everything. |
| **Timeouts** | Every external call has a timeout shorter than the message lock. |
| **Rate limiting** | Limit concurrent consumers so you don't overload a fragile DB/API. |

---

## 5. Message expiry (TTL)

Some messages lose value with time ("price valid for 5 minutes", "OTP code"). Set a **Time-To-Live**:
- Expired messages are dropped or dead-lettered (configurable).
- Prevents processing stale commands after a long outage.

---

## 6. Where errors go in an async flow

The user is gone when the error happens. Options to surface it:
- Update status in DB (`Order.Status = Failed`) → UI polls or gets a push (SignalR/WebSocket).
- Publish a failure event (`OrderRejected`) → notification service emails the user.
- Compensate previous steps (see [Sagas](09-Sagas-Workflows.md)).

---

## Interview one-liners

- "I separate transient errors (retry with exponential backoff and jitter) from poison messages (dead-letter quickly) and business failures (publish an event, ack the message)."
- "A DLQ without monitoring and a replay process is just a place where data goes to die."
- "Never block a consumer thread for a long backoff — use delayed redelivery or retry queues."

## Check yourself

1. Why is jitter needed? Describe the thundering-herd scenario.
2. A message fails with `JsonException`. Should you retry 10 times?
3. Your payment provider is down for 1 hour. What happens with retries=5, backoff 1s→16s? What would you do instead?
4. "Card declined" — DLQ or not?

<div dir="rtl" lang="fa">

**خلاصه:** خطاها سه نوع‌اند: موقت (Timeout، قطعی شبکه → Retry با Exponential Backoff و Jitter)، دائمی/Poison (JSON خراب، باگ → بعد از چند تلاش به DLQ) و خطای تجاری (کارت رد شد → خطا نیست، یک ایونت مثل PaymentFailed منتشر کن و Ack کن). DLQ باید مانیتور شود و فرایند بازپخش (Replay) داشته باشد. برای سرویس‌های پایین‌دستی از Circuit Breaker، Timeout و توقف مصرف استفاده کن.

</div>
