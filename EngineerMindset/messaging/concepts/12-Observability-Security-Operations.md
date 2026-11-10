# 12 — Observability, Security & Operations

> **Level 3 — Distributed design.** Running messaging in production: seeing what happens, keeping it safe, and operating it day to day.

---

## 1. Observability — the three pillars applied to messaging

### Correlation
Every message carries:
- **CorrelationId** — the business flow (e.g. `OrderId` or the original request id). Put it in **every log line** (logging scope).
- **CausationId** — the message that caused this one → you can rebuild the chain.
- **MessageId** — this specific message.

### Distributed tracing
- **W3C Trace Context** (`traceparent` header) propagates a trace across HTTP **and** messages.
- **OpenTelemetry** in .NET: the Azure Service Bus SDK, Confluent.Kafka (via instrumentation packages), RabbitMQ.Client 7.x and MassTransit/NServiceBus emit `Activity`s you can export to Jaeger, Zipkin, Application Insights, Grafana Tempo.
- In async flows a trace is usually **linked** spans: `publish` span → `receive/process` span (possibly minutes later).

```
HTTP POST /orders ─┬─ db insert
                   └─ publish OrderPlaced ··· (queue) ··· process OrderPlaced (Payments) ─ publish PaymentSucceeded ··· process (Orders)
```

### Metrics to dashboard & alert on

| Metric | Alert when |
|--------|-----------|
| Queue depth / consumer lag | Growing for > N minutes |
| Age of oldest message | Above your SLA (e.g. > 5 min) |
| DLQ count | **> 0** (or rising) |
| Processing error rate / retries | Spike |
| Processing duration p95/p99 | Approaching lock duration |
| Active consumers | 0 for a queue that has messages |
| Broker health | Disk/memory alarms, node down, throttling |

See also: [Prometheus & Grafana](../../Tools/Prometheus-Grafana.md).

### Structured logging
Log `MessageId`, `CorrelationId`, `MessageType`, `DeliveryCount`, `Queue`, and duration. Never log full payloads with sensitive data.

---

## 2. Security

| Area | Practice |
|------|---------|
| **Authentication** | Prefer identity-based auth: **Managed Identity / Entra ID** for Azure Service Bus (no connection strings in config). Otherwise SAS keys / users with least privilege, stored in Key Vault. |
| **Authorization** | Least privilege per app: Send-only for producers, Listen-only for consumers, Manage only for ops/IaC. RabbitMQ: per-vhost permissions (configure/write/read regex). Kafka: ACLs. |
| **Encryption in transit** | TLS (AMQPS 5671, Kafka SSL). Cloud brokers enforce it. |
| **Encryption at rest** | Provided by cloud brokers; customer-managed keys if required. |
| **Network** | Private endpoints / VNet integration, no public broker endpoints for internal traffic. |
| **Payload trust** | Validate every message like untrusted input — schema validation, size limits. Consumers shouldn't blindly deserialize polymorphic types (deserialization attacks). |
| **Sensitive data** | Minimize PII in messages; encrypt fields if needed; remember DLQs and retention keep copies. |
| **Multi-tenancy** | Tenant id in headers, enforced by the consumer; or isolation by namespace/vhost. |

---

## 3. Operations

### Infrastructure as Code
Declare queues, topics, subscriptions, DLQ settings, partitions in **Bicep/Terraform** (or broker definitions files). Avoid apps creating production topology on startup with admin rights (fine in dev).

### Deployments
- Consumers must handle **old and new message versions** during rolling deploys (see [10](10-Message-Contracts-Evolution.md)).
- **Graceful shutdown:** stop receiving, finish in-flight messages, ack, then exit. (In .NET: respect the `CancellationToken` in `BackgroundService`, `StopProcessingAsync`.)

### Runbooks
- **DLQ triage:** inspect → classify (bug / bad data / transient) → fix → **resubmit** or discard (with audit).
- **Replay** (Kafka): reset consumer-group offsets to a time; requires idempotent consumers.
- **Backlog recovery:** scale out, then scale back; watch the DB.
- **Broker failover/DR:** Service Bus geo-disaster recovery / geo-replication, Kafka MirrorMaker / cluster linking, RabbitMQ federation/shovel.

### Testing messaging code
| Level | How |
|-------|-----|
| Unit | Test handlers as plain methods with a fake message. |
| Integration | Real broker in Docker via **Testcontainers** (RabbitMQ, Kafka, Service Bus emulator). |
| Contract | Consumer-driven contracts / schema compatibility checks in CI. |
| Chaos / failure | Kill consumers mid-processing, stop the broker, inject duplicates — verify idempotency. |

---

## Interview one-liners

- "I propagate CorrelationId and W3C traceparent in message headers, log them in a scope, and use OpenTelemetry to see the whole async flow."
- "Key alerts: backlog age, consumer lag, DLQ > 0, and zero active consumers."
- "Producers get send-only, consumers listen-only, with managed identity instead of connection strings."

## Check yourself

1. A customer says "my order is stuck". Which ids and tools do you use to find out where?
2. Which three alerts would you create first for a new queue?
3. How do you test that your consumer is idempotent?
4. What does graceful shutdown mean for a message consumer?

<div dir="rtl" lang="fa">

**خلاصه:** برای دیده‌پذیری: CorrelationId و CausationId در هر پیام و هر لاگ، انتشار traceparent و استفاده از OpenTelemetry برای ردیابی جریان غیرهمزمان. هشدارهای مهم: رشد صف یا Lag، سن قدیمی‌ترین پیام، DLQ بیشتر از صفر، و نبودن مصرف‌کننده فعال. امنیت: Managed Identity، حداقل دسترسی (فقط Send یا فقط Listen)، TLS، شبکه خصوصی، و اعتبارسنجی هر پیام. عملیات: تعریف زیرساخت با IaC، خاموشی امن (Graceful Shutdown)، رویه رسیدگی به DLQ و تست با Testcontainers.

</div>
