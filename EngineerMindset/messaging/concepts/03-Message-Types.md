# 03 — Message Types: Command, Event, Query, Document

> **Level 1 — Foundations.** "Message" is a generic word. *What kind* of message you send changes naming, ownership, routing and coupling.

---

## 1. The three main intents

| Type | Intent | Naming | Receivers | Can be rejected? | Example |
|------|--------|--------|-----------|------------------|---------|
| **Command** | "Please **do** this" | Imperative verb | Exactly **one** logical handler | Yes (validation, business rule) | `ChargePayment`, `SendWelcomeEmail` |
| **Event** | "This **happened**" | Past tense | **Zero, one or many** | No — it's a fact, it already happened | `OrderPlaced`, `PaymentFailed` |
| **Query** | "Tell me…" | Question / Get | One | — | `GetOrderStatus` (usually HTTP, not a broker) |

```
Command:   Orders ──ChargePayment──► Payments          (Orders knows Payments exists)
Event:     Orders ──OrderPlaced────► ??? (anyone)       (Orders doesn't care who listens)
```

### Ownership — who owns the contract?

- **Command:** the **receiver** owns the contract ("this is what I accept"). Sent to a queue.
- **Event:** the **publisher** owns the contract ("this is what I announce"). Published to a topic.

### Coupling

- Commands couple the sender to the receiver's **capability** ("I know someone charges payments").
- Events invert the dependency: the publisher knows **nothing** about subscribers. Adding a new subscriber needs zero change in the publisher.

> **Smell:** an "event" named `SendEmailEvent` is really a command in disguise. If the publisher expects one specific reaction, it's a command.

---

## 2. Domain events vs integration events

| | Domain event | Integration event |
|--|--------------|-------------------|
| Scope | **Inside** one service / bounded context | **Between** services |
| Transport | Usually in-memory (e.g. MediatR notification) | Broker (RabbitMQ, Kafka, Service Bus) |
| Transaction | Same transaction as the change | Published after commit (ideally via **Outbox**) |
| Contract stability | Can change freely (internal) | Public contract — must be versioned carefully |
| Example | `OrderLineAdded` | `OrderPlaced` (v1) |

A common flow: aggregate raises a domain event → a handler maps it to an integration event → stored in Outbox → published.

---

## 3. How much data should an event carry?

### Event Notification (thin event)

```json
{ "type": "OrderPlaced", "orderId": 1234 }
```

- Small, stable, private details stay private.
- Subscribers often must **call back** the publisher to get data → extra load, runtime coupling, and possible race ("data changed since").

### Event-Carried State Transfer (fat event)

```json
{ "type": "OrderPlaced", "orderId": 1234, "customerId": 42,
  "lines": [{ "sku": "A1", "qty": 2, "price": 10 }], "total": 20, "currency": "EUR" }
```

- Subscribers have everything they need → **no callback**, work even if the publisher is down.
- Bigger messages, more fields to keep compatible, subscribers may keep local copies (data duplication).

| | Thin | Fat |
|--|------|-----|
| Message size | Small | Larger |
| Callback needed | Often | No |
| Autonomy of consumers | Low | High |
| Contract surface | Small | Large |

> **Interview answer:** "I prefer events that carry what most consumers need (ECST) to avoid chatty callbacks, but I keep them focused — not a dump of the entire database row."

### Delta vs snapshot

- **Delta:** `PriceChanged { old: 10, new: 12 }` — what changed.
- **Snapshot / state:** `ProductUpdated { full product }` — current state. Easier for consumers; works well with Kafka log compaction.

---

## 4. Other message styles

| Style | Meaning |
|-------|---------|
| **Document message** | Just transfers data, no intent implied (e.g. a nightly product catalog export). |
| **Request / Reply** | A command or query with a `ReplyTo` and `CorrelationId`; the receiver sends a response message. |
| **Notification** | A lightweight "something changed, go look" — like a thin event. |
| **Fire-and-forget** | Sender doesn't expect any reply. Commands and events usually are. |

---

## 5. Naming conventions (practical)

- Events: `<Entity><PastTenseVerb>` → `OrderPlaced`, `InvoicePaid`, `UserEmailChanged`.
- Commands: `<Verb><Entity>` → `PlaceOrder`, `RefundPayment`.
- Include a **version** in the type or schema: `OrderPlaced.v2` or a `schemaVersion` header.
- Name events in **business language** (`OrderShipped`), not database language (`OrderRowUpdated`).
- Avoid CRUD events (`OrderUpdated`) when you can express the **why** (`OrderAddressCorrected`, `OrderCancelled`).

---

## Interview one-liners

- "A command is a request to one handler that can be rejected; an event is an immutable fact broadcast to anyone interested."
- "Commands go to queues, events go to topics. The receiver owns the command contract; the publisher owns the event contract."
- "Domain events stay inside a bounded context; integration events cross service boundaries and need versioning and an outbox."

## Check yourself

1. Is `SendInvoiceEmail` an event or a command? Why?
2. What's the risk of thin events? What's the risk of fat events?
3. Why should integration events be published *after* the DB commit?
4. Rename `OrderUpdated` into something that reveals business intent.

<div dir="rtl" lang="fa">

**خلاصه:** Command یعنی «این کار را انجام بده» (یک گیرنده، ممکن است رد شود، اسم امری مثل ChargePayment). Event یعنی «این اتفاق افتاد» (صفر یا چند گیرنده، قابل رد نیست، اسم گذشته مثل OrderPlaced). Domain Event داخل یک سرویس است؛ Integration Event بین سرویس‌ها و باید نسخه‌بندی شود. ایونت سبک (فقط ID) باعث فراخوانی برگشتی می‌شود؛ ایونت پر (ECST) استقلال مصرف‌کننده را بیشتر می‌کند ولی قرارداد بزرگ‌تری دارد.

</div>
