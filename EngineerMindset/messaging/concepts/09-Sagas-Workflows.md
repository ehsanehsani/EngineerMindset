# 09 — Sagas & Long-Running Workflows

> **Level 3 — Distributed design.** How to run a business "transaction" that spans several services, each with its own database.

---

## 1. The problem

Place order = reserve stock (Inventory DB) + charge card (Payment service) + create shipment (Shipping DB).

- No shared database → no ACID transaction across them.
- 2PC is impractical (see [08](08-Consistency-Outbox-Inbox.md)).
- Steps may take seconds, hours (manual approval), or days (shipping).

## 2. Saga — definition

A **saga** is a sequence of **local transactions**. Each step commits in its own service and triggers the next step via a message. If a step fails, the saga runs **compensating transactions** to semantically undo the previous steps.

```
Happy path:     ReserveStock ✔ → ChargePayment ✔ → CreateShipment ✔ → OrderConfirmed
Failure path:   ReserveStock ✔ → ChargePayment ✖ → ReleaseStock (compensate) → OrderCancelled
```

### Compensation ≠ rollback

- A rollback erases as if nothing happened. A **compensation** is a **new business action** that offsets the old one: `RefundPayment`, `ReleaseStock`, `CancelShipment`, send an apology email.
- Some actions **can't** be compensated (an email was already sent, a package already left). Order steps so the hard-to-undo ones come **last** (pivot transaction).

### Saga ACID? → "ACD" without I

Sagas give **Atomicity** (eventually all-or-compensated), **Consistency**, **Durability**, but **no Isolation**: other transactions can see intermediate states ("stock reserved but not yet paid"). Countermeasures:
- **Semantic locks:** a `Pending` status that others respect.
- **Commutative updates**, re-reading values, versioning.

---

## 3. Choreography vs Orchestration

### Choreography — services react to each other's events

```
Orders ──OrderPlaced──► Inventory ──StockReserved──► Payment ──PaymentSucceeded──► Orders (confirm)
                                                       │
                                                       └──PaymentFailed──► Inventory (release) ──► Orders (cancel)
```

| Pros | Cons |
|------|------|
| No central component, loose coupling | The workflow is **implicit** — spread over many services, hard to see |
| Simple for 2–4 steps | Cyclic dependencies between services creep in |
| Each service is autonomous | Hard to answer "where is order 1234 now?" |

### Orchestration — one coordinator tells each service what to do

```
                    ┌──ReserveStock──► Inventory ──StockReserved──┐
Orders ──► Order Saga Orchestrator                                  │
 (state machine)    ├──ChargePayment─► Payment ──PaymentFailed────┤
                    └──ReleaseStock──► Inventory                    │
                    ◄──────────────────── replies ─────────────────┘
```

| Pros | Cons |
|------|------|
| Workflow is **explicit** in one place (state machine) | Orchestrator can become a "god service" with too much logic |
| Easy to monitor, timeout, and change | One more component to run |
| Clear compensation logic | Participants are coupled to the orchestrator's commands |

> **Rule of thumb:** choreography for simple flows with few steps; orchestration when there are many steps, branching, timeouts, or you need visibility.

---

## 4. Saga state & mechanics

An orchestrated saga is a **persistent state machine**:

```
OrderSaga { CorrelationId = OrderId, State = AwaitingPayment, StockReservationId, PaymentId, CreatedAt, Version }
States:   Started → AwaitingStock → AwaitingPayment → Completed
                                                   ↘ Compensating → Cancelled
```

Must-haves:
- **Correlation:** every message carries the `OrderId` (correlation id) so it finds its saga instance.
- **Persistence:** state stored in a DB (with optimistic concurrency) — the orchestrator can crash and resume.
- **Idempotency:** every step and every compensation must tolerate duplicates.
- **Timeouts:** "If no `PaymentSucceeded` within 15 minutes → compensate." Implemented with scheduled messages.
- **Outbox:** state change + outgoing command in one transaction.
- **Retryable compensations:** a compensation **must eventually succeed** (retry, then human escalation).

---

## 5. Related patterns & tools

| Term | Meaning |
|------|---------|
| **Process manager** | Enterprise Integration Patterns name for an orchestrator that routes messages based on state. Often used interchangeably with "orchestrated saga". |
| **Routing slip** | The message carries the list of steps; each step does its work and forwards to the next (MassTransit Courier). |
| **Workflow engines** | Temporal, Azure Durable Functions, AWS Step Functions, Camunda — write workflows as code/diagrams; the engine handles state, retries, timers. |
| **Saga frameworks (.NET)** | MassTransit state machines, NServiceBus sagas, Wolverine sagas. |

---

## Interview one-liners

- "A saga splits a distributed transaction into local transactions with compensating actions; it gives eventual atomicity but no isolation."
- "Choreography is event-driven and decentralized — great for simple flows. Orchestration uses a state machine — better for complex flows, timeouts and visibility."
- "Put irreversible steps last, make compensations idempotent and retryable, and always add timeouts."

## Check yourself

1. Why isn't a compensation the same as a rollback? Give an example where compensation is impossible.
2. What does "no isolation" mean in a saga, and how do semantic locks help?
3. Design the order saga as an orchestrator: list states, commands, events, and timeouts.
4. When would you pick choreography over orchestration?

<div dir="rtl" lang="fa">

**خلاصه:** Saga یک تراکنش تجاری توزیع‌شده را به چند تراکنش محلی تقسیم می‌کند؛ اگر یک مرحله شکست بخورد، مراحل قبلی با «عملیات جبرانی» (مثل بازپرداخت یا آزادسازی موجودی) خنثی می‌شوند — نه Rollback. Saga ایزوله‌سازی ندارد. دو سبک: Choreography (سرویس‌ها به ایونت‌های هم واکنش نشان می‌دهند؛ ساده ولی جریان پنهان است) و Orchestration (یک State Machine مرکزی دستور می‌دهد؛ شفاف‌تر و مناسب جریان‌های پیچیده). Correlation، ذخیره وضعیت، Idempotency و Timeout الزامی‌اند.

</div>
