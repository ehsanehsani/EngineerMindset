# Lab 07 — Capstone: Build It Yourself

**Goal:** design and build a small but production-shaped event-driven system **without step-by-step code**. You decide the details; the spec and acceptance criteria tell you when you're done.

**Concepts:** all of them — keep [15 Interview Questions](../concepts/15-Interview-Questions.md) open and use this project as your story in interviews ("In a project I built, I handled duplicates by…").

**Time:** 1–2 days (spread over a week is fine).

---

## The system: "TicketHub" — concert ticket orders

```
Client ──► Orders.Api ──(outbox)──► broker ──► Payments.Worker
                                          ├──► Tickets.Worker   (reserves seats, issues tickets)
                                          ├──► Notifications.Worker (emails — fake: write to console/file)
                                          └──► Analytics.Worker (counts sales per concert)
```

### Functional requirements

1. `POST /orders` with `{ concertId, seats, customerEmail }` returns **202** + order id within 100 ms.
2. `GET /orders/{id}` returns the status: `Pending → SeatsReserved → Paid → Completed`, or `Cancelled` with a reason.
3. Tickets reserves seats (fixed capacity per concert). Not enough seats → order cancelled.
4. Payments charges (fake). Orders over 500 € fail. 10% of calls throw a **transient** error.
5. If payment fails, seats are released (**compensation**).
6. If payment doesn't complete within **2 minutes**, the order is cancelled and seats released (**timeout**).
7. Notifications sends exactly **one** email per completed or cancelled order.
8. Analytics keeps a sales counter per concert and can **rebuild** it from history.

### Non-functional requirements (the real exercise)

| # | Requirement | Concept |
|---|-------------|---------|
| N1 | No lost events if the broker is down when an order is placed | Outbox ([08](../concepts/08-Consistency-Outbox-Inbox.md)) |
| N2 | No double charge / double email / double ticket under redelivery | Inbox / idempotency ([05](../concepts/05-Delivery-Guarantees-Idempotency.md)) |
| N3 | Transient failures retried with backoff; poison messages to a DLQ with the reason | [06](../concepts/06-Failures-Retries-DLQ.md) |
| N4 | All events for one order processed in order by each service | Partition key / sessions ([07](../concepts/07-Ordering-Partitioning-Concurrency.md)) |
| N5 | Every message has MessageId, CorrelationId (= order id), CausationId, type with version | [02](../concepts/02-Building-Blocks.md), [10](../concepts/10-Message-Contracts-Evolution.md) |
| N6 | One order can be traced end-to-end across all services | [12](../concepts/12-Observability-Security-Operations.md) |
| N7 | Run 2 instances of each worker without breaking anything | Competing consumers ([04](../concepts/04-Communication-Models.md)) |
| N8 | Add a field to `OrderPlaced` without breaking older consumers | Tolerant reader ([10](../concepts/10-Message-Contracts-Evolution.md)) |

---

## Phase 1 — Design on paper (do NOT skip)

Before writing code, write a `DESIGN.md` in your capstone folder with:

1. **Message catalog:** every command and event — name, version, producer, consumers, fields, key used for ordering.
2. **Topology:** exchanges/topics, queues/subscriptions, DLQs, retry queues.
3. **Saga choice:** choreography or orchestration — and **why**. Draw the state machine.
4. **Failure table:** for each step, "what if this crashes / is duplicated / arrives late?"
5. **Broker choice** with a short justification (see [14](../concepts/14-Choosing-a-Broker.md)). Any of RabbitMQ / Service Bus / Kafka works.
6. **Data ownership:** which service owns which tables; no shared databases.

Review your design against the [interview scenario 1](../concepts/15-Interview-Questions.md#scenario-1--e-commerce-order-placement). Would you be comfortable presenting it?

## Phase 2 — Build

Suggested order:
1. Orders.Api with Outbox (reuse Lab 03).
2. Tickets.Worker and Payments.Worker with Inbox.
3. The saga (reuse Lab 06 ideas) + compensation + timeout (scheduled message, or a timer that scans pending orders).
4. Notifications + Analytics.
5. Retries/DLQ (reuse Lab 02 or broker features from Lab 05).
6. Observability.

Infrastructure: write a single `docker-compose.yml` that starts the broker (+ a DB like Postgres/SQL Server if you outgrow SQLite) so the whole system starts with one command.

## Phase 3 — Prove it (acceptance tests)

Run each scenario and record the result in `RESULTS.md`:

| # | Scenario | Expected |
|---|----------|----------|
| A1 | Place 20 orders with all workers running | All `Completed` or correctly `Cancelled`; seats consistent |
| A2 | Stop the broker, place 5 orders, start the broker | All 5 eventually processed (N1) |
| A3 | Kill Payments in the middle of processing, restart | No double charge (N2) |
| A4 | Publish the same `PaymentSucceeded` 3 times manually | One ticket, one email (N2) |
| A5 | Publish a message with invalid JSON | Lands in the DLQ with a reason; other messages unaffected (N3) |
| A6 | Stop Payments for 3 minutes | Pending orders time out, seats released (req. 6) |
| A7 | Run 2 instances of every worker, place 50 orders | Same correctness as A1 (N7) |
| A8 | Add `discountCode` to `OrderPlaced`, deploy only Orders.Api | Other services keep working (N8) |
| A9 | Delete Analytics' data and rebuild | Counters match the number of completed orders (req. 8) |
| A10 | Pick one order id and show its full path | All log lines/spans found by correlation id (N6) |

## Stretch goals (pick any)

- **OpenTelemetry + Jaeger** (or Aspire dashboard): propagate `traceparent` in message headers and see one trace across all services.
- **.NET Aspire** AppHost to orchestrate all services + broker locally.
- **Testcontainers** integration tests for A3/A4 that run in CI.
- **Orchestrated saga with MassTransit 8.x** state machine, then compare with your hand-written version.
- **Kafka version** of Analytics with event replay via consumer group reset.
- **Schema registry** (Avro/Protobuf) for the event contracts.
- **Autoscaling** a worker with **KEDA** on queue length (in a local Kubernetes like kind/k3d).
- **Prometheus/Grafana dashboard**: queue depth, DLQ count, processing time, consumer lag. See [Prometheus & Grafana](../../Tools/Prometheus-Grafana.md).

## Final step — Tell the story

Write a one-page `STORY.md` you could use in an interview:
- The problem, the architecture (one diagram).
- Three hard problems you hit and how you solved them (e.g. duplicate emails, stuck sagas, ordering).
- What you'd change for 100× traffic.

<div dir="rtl" lang="fa">

**پروژه نهایی:** یک سیستم سفارش بلیت کنسرت با چند سرویس (سفارش، پرداخت، بلیت، اعلان، آنالیتیکس) را خودت از صفر طراحی و پیاده‌سازی کن. اول طراحی روی کاغذ (فهرست پیام‌ها، توپولوژی، Saga، جدول خطاها، انتخاب بروکر)، بعد پیاده‌سازی با Outbox، Inbox، Retry/DLQ، ترتیب پیام و ردیابی، و در نهایت سناریوهای پذیرش (قطع بروکر، Kill کردن سرویس، پیام تکراری، Timeout و ...) را اجرا و نتیجه را ثبت کن. در پایان یک «داستان» یک‌صفحه‌ای برای مصاحبه بنویس.

</div>
