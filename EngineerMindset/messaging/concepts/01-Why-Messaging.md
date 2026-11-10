# 01 — Why Messaging?

> **Level 1 — Foundations.** Before learning *how*, understand *why*. Most bad messaging designs come from using messages where a simple HTTP call was enough — or the opposite.

---

## 1. Two ways services talk

### Synchronous (request/response)

```
Client ──HTTP──► Orders API ──HTTP──► Payment API ──HTTP──► Email API
         ◄──────────────────  waits for everything  ◄────────────
```

The caller **waits** for the answer. Examples: HTTP/REST, gRPC, a direct method call.

### Asynchronous (messaging)

```
Client ──HTTP──► Orders API ──► [ Broker ] ──► Payment worker
         ◄── 202 Accepted          │
                                   └──────────► Email worker
```

The sender puts a **message** somewhere (a broker) and **moves on**. Someone else processes it later — milliseconds or hours later.

| | Synchronous | Asynchronous (messaging) |
|--|-------------|--------------------------|
| Caller waits? | Yes | No |
| Both sides must be online? | Yes | No — broker stores the message |
| Failure of receiver | Caller fails too (cascading failure) | Message waits; processed when receiver is back |
| Latency of the user request | Sum of all calls | Only the first step |
| Consistency | Easier (immediate) | **Eventual** consistency |
| Debugging | Easy (one stack trace) | Harder (flow is spread over time and services) |
| Typical tools | REST, gRPC | RabbitMQ, Kafka, Azure Service Bus, SQS |

---

## 2. Coupling — the key word

Messaging is mainly a tool to **reduce coupling**. There are several kinds:

| Coupling type | Meaning | How messaging helps |
|---------------|---------|---------------------|
| **Temporal coupling** | Both services must be **running at the same time** | Broker stores messages; receiver can be down |
| **Location coupling** | Sender must know **where** the receiver is (URL, IP) | Sender only knows the queue/topic name |
| **Behavioral / knowledge coupling** | Sender knows **who** reacts and **what** they do | With events, the sender doesn't even know who listens |
| **Rate coupling** | Receiver must handle traffic **at the speed it arrives** | Queue acts as a buffer (load leveling) |

> **Interview one-liner:** "Messaging removes temporal and location coupling: the producer doesn't need the consumer to be online, or even to know who the consumer is."

---

## 3. Why we use messaging (the benefits)

1. **Resilience** — a downstream service crash does not break the upstream one. Messages wait.
2. **Load leveling / buffering** — a traffic spike (Black Friday) fills the queue; workers drain it at a steady pace instead of falling over.
3. **Scalability** — add more consumers to process faster (competing consumers).
4. **Extensibility** — add a new subscriber (e.g. Analytics) **without changing** the producer.
5. **Responsiveness** — return `202 Accepted` to the user quickly and do heavy work in the background (PDF generation, video encoding, emails).
6. **Integration** — connect systems in different languages/teams/companies through a common contract.
7. **Auditing / replay** (log-based brokers like Kafka) — re-read history to rebuild state or fix bugs.

---

## 4. What it costs you (the trade-offs)

Messaging is **not free**. Senior engineers are expected to mention costs:

| Cost | Why it hurts |
|------|--------------|
| **Eventual consistency** | The user may not see the result immediately ("your order is being processed") |
| **Duplicates** | Most brokers deliver *at least once* → consumers must be **idempotent** |
| **Ordering problems** | Messages may arrive out of order |
| **Harder debugging** | No single stack trace; you need correlation IDs and distributed tracing |
| **Operational overhead** | A broker to run, monitor, secure, upgrade, pay for |
| **Error handling is asynchronous** | You can't just return a 500 to the user; you need retries, DLQs, compensations |
| **Hidden coupling** | Contracts (message schemas) still couple services — changing a message can break consumers |

---

## 5. When to use messaging — and when not

### Good fits

- Work that **doesn't need to finish before replying** to the user (emails, notifications, reports, thumbnails).
- **Fan-out**: one thing happened, many services must react (`OrderPlaced` → Email, Inventory, Analytics).
- **Spiky load** that should be smoothed out.
- Integrating **independent services/teams** that deploy separately.
- **Long-running workflows** (minutes, hours, days).
- **Streaming data** (IoT telemetry, click streams, logs).

### Bad fits

- The user **needs the answer now** ("Is this username available?", "What's my balance?") → use a query (HTTP/gRPC).
- Simple CRUD apps or a single monolith with no integration needs — messaging adds complexity for no gain.
- Strong, immediate consistency is required across steps and you can keep it in **one database transaction** — do that instead.
- Request/reply over a broker just to "be async" — often it's an HTTP call with extra steps.

> **Rule of thumb:** *Queries* (I need data now) → synchronous. *Commands that can be deferred* and *events* (something happened) → messaging.

---

## 6. A real example: placing an order

**Fully synchronous:**

```
POST /orders
  → save order
  → call Payment (2s)
  → call Inventory (1s)
  → call Email (3s, and it's down!)  ✖ whole request fails
```

**With messaging:**

```
POST /orders
  → save order + publish OrderPlaced  → return 202 in 50ms
OrderPlaced ─► Payment worker
            ─► Inventory worker
            ─► Email worker (down? message waits, sent when it's back)
```

The user experience is faster and an Email outage no longer breaks ordering.

---

## Interview one-liners

- "Messaging trades **immediate consistency** for **resilience, scalability and decoupling**."
- "I use sync calls for queries and anything the user is waiting for; I use messages for side effects, fan-out, and long-running or spiky work."
- "The main costs are duplicates, ordering, eventual consistency and observability — so I design for idempotency and correlation IDs from day one."

## Check yourself

1. What is temporal coupling, and how does a queue remove it?
2. Give two cases where messaging is the **wrong** choice.
3. What does "load leveling" mean? Draw it.
4. Why does messaging make debugging harder, and what do you do about it?

<div dir="rtl" lang="fa">

**خلاصه:** در ارتباط همزمان (HTTP) فرستنده منتظر جواب می‌ماند و اگر سرویس مقصد خراب باشد، کل درخواست خراب می‌شود. در پیام‌رسانی، پیام در بروکر ذخیره می‌شود و فرستنده ادامه می‌دهد. مزایا: کاهش وابستگی (زمانی و مکانی)، تحمل خطا، مقیاس‌پذیری و جذب ترافیک ناگهانی. هزینه‌ها: سازگاری نهایی (Eventual Consistency)، پیام تکراری، مشکل ترتیب، و دیباگ سخت‌تر. برای کوئری و چیزی که کاربر منتظرش است از HTTP استفاده کن؛ برای کارهای جانبی، اعلان رویداد و کارهای طولانی از پیام.

</div>
