# 14 — Choosing a Broker

> **Level 4 — Architecture.** "Why Kafka and not RabbitMQ?" is a classic. There's no best broker — only a best fit for the requirements.

---

## 1. The main players

| Broker | Model | One-line identity |
|--------|-------|-------------------|
| **RabbitMQ** | Queue (AMQP), plus Streams | Flexible, smart routing (exchanges), classic work queues, self-hosted or managed. |
| **Apache Kafka** | Distributed log | High-throughput event streaming, retention & replay, partitions. |
| **Azure Service Bus** | Queue + Topics (AMQP) | Fully managed enterprise messaging: sessions, transactions, DLQ, scheduled messages, duplicate detection. |
| **Azure Event Hubs** | Distributed log (Kafka-compatible) | Managed big-data / telemetry ingestion. |
| **Azure Event Grid** | Push-based event routing | Reactive "something happened" notifications, webhooks, resource events. |
| **Azure Storage Queues** | Simple queue | Cheap, huge backlogs, minimal features. |
| **AWS SQS / SNS** | Queue / Pub-sub fan-out | Managed, simple, SNS→SQS fan-out pattern; SQS FIFO for ordering. |
| **AWS Kinesis**, **Google Pub/Sub** | Streams / pub-sub | Cloud-native equivalents. |
| **NATS / JetStream** | Lightweight pub/sub (+ persistence) | Very fast, simple, edge/IoT/cloud-native. |
| **Redis Streams / Pub/Sub** | In-memory streams | Good when Redis is already there and durability needs are modest. |
| **Apache Pulsar** | Log + queue hybrid | Multi-tenant, tiered storage, geo-replication. |

---

## 2. RabbitMQ vs Kafka vs Azure Service Bus

| Dimension | RabbitMQ | Kafka | Azure Service Bus |
|-----------|----------|-------|-------------------|
| Core model | Smart broker, simple consumers | Dumb broker (log), smart consumers | Smart broker, managed |
| Message after consume | Deleted on ack | Retained (time/size) | Deleted on complete |
| Replay | No (Streams: yes) | **Yes** | No |
| Routing | **Very rich** (direct/topic/fanout/headers) | By topic/partition key | Subscription filters (SQL/correlation) |
| Ordering | Per queue (single consumer) | Per partition | Per session |
| Throughput | High (tens of thousands msg/s per node) | **Very high** (millions msg/s per cluster) | Moderate–high (tier/MU dependent) |
| Per-message features | TTL, priority, DLX, delayed (plugin) | Few | **TTL, DLQ, scheduled, defer, dedup, sessions, transactions** |
| Consumer scaling | Unlimited competing consumers | ≤ partitions per group | Unlimited competing consumers |
| Ops | You run it (or CloudAMQP, Amazon MQ) | Heavy to run yourself (or Confluent, Event Hubs, MSK) | Zero ops (PaaS) |
| Best for | Task queues, complex routing, request/reply, on-prem | Event streaming, analytics pipelines, event backbone, CDC | Business workflows on Azure, commands & events with strong reliability features |

---

## 3. Decision questions

Ask these in an interview before naming a product:

1. **Do consumers need to replay history** or join later and read everything? → Log (Kafka / Event Hubs).
2. **Volume?** Hundreds/sec → anything. Millions/sec → Kafka / Event Hubs / Kinesis.
3. **Per-message workflow features** (delays, TTL, DLQ, sessions, dedup)? → Service Bus / RabbitMQ.
4. **Complex routing** by attributes? → RabbitMQ exchanges / Service Bus filters / SNS filters.
5. **Ordering requirements?** Per key → Kafka partitions, Service Bus sessions, SQS FIFO.
6. **Ops capacity & cloud?** Small team on Azure → Service Bus. On AWS → SQS/SNS. On-prem → RabbitMQ.
7. **Ecosystem:** need stream processing, connectors, CDC (Debezium)? → Kafka.
8. **Cost model:** per-operation (Service Bus Standard, SQS) vs provisioned capacity (Premium, Event Hubs TUs/PUs, Kafka clusters).
9. **Compliance / data residency / multi-region**?

### Common combinations

- **Service Bus for commands/business events + Event Hubs for telemetry.**
- **Kafka as the event backbone + RabbitMQ/SQS for task queues.**
- **Event Grid** to fan out Azure resource events (blob uploaded) to Functions.

---

## 4. Libraries vs raw SDKs (.NET)

| Option | Notes |
|--------|------|
| **Raw SDKs** (`RabbitMQ.Client`, `Confluent.Kafka`, `Azure.Messaging.ServiceBus`) | Full control, you build retries/outbox/serialization yourself. Great for learning. |
| **MassTransit** | Abstraction over RabbitMQ/Service Bus/SQS/Kafka(rider): sagas, outbox, retries, scheduling. Note: v9+ is commercially licensed; v8 remains open source. |
| **NServiceBus** | Mature commercial framework (Particular), sagas, outbox, tooling (ServicePulse/ServiceInsight). |
| **Wolverine** | Open-source .NET mediator + messaging, with outbox (Marten/EF Core). |
| **Rebus**, **Brighter**, **CAP** | Other OSS options; CAP focuses on outbox + event bus. |

> Interview angle: "A framework saves time on cross-cutting concerns (retries, outbox, sagas), but you should understand what it does underneath — otherwise you can't debug it."

---

## Interview one-liners

- "RabbitMQ is a smart broker with rich routing; Kafka is a distributed log optimized for throughput and replay; Service Bus is a managed enterprise broker with sessions, DLQ, scheduling and dedup."
- "I'd pick based on replay needs, throughput, per-message features, ordering, ops capacity and cloud platform — often more than one broker in the same system."

## Check yourself

1. A team needs to process 50 business commands/sec with delays and DLQ on Azure. Which broker and why?
2. A team needs to ingest 2M IoT events/sec and run real-time aggregations. Which and why?
3. Why might "just use Kafka for everything" be a poor decision?
4. Explain "smart broker / dumb consumer" vs "dumb broker / smart consumer".

<div dir="rtl" lang="fa">

**خلاصه:** RabbitMQ: بروکر هوشمند با مسیریابی غنی، مناسب صف کارها و مسیریابی پیچیده. Kafka: لاگ توزیع‌شده با Throughput بسیار بالا، نگهداری و بازپخش پیام، مناسب Event Streaming. Azure Service Bus: سرویس مدیریت‌شده با Session، DLQ، پیام زمان‌بندی‌شده و حذف تکراری، مناسب جریان‌های تجاری روی Azure. انتخاب بر اساس نیاز به Replay، حجم، امکانات هر پیام، ترتیب، توان تیم عملیات و پلتفرم ابری انجام می‌شود؛ اغلب ترکیبی از چند بروکر استفاده می‌شود.

</div>
