# 02 — Building Blocks (the vocabulary)

> **Level 1 — Foundations.** Every broker uses slightly different names for the same ideas. Learn the generic concept first, then map it to each product.

---

## 1. The cast

```
 Producer ──► [ Broker:  Exchange/Topic ──► Queue/Subscription ] ──► Consumer
 (sender)                 (routing)          (storage)                (receiver)
```

| Term | Definition |
|------|-----------|
| **Message** | A unit of data sent from one component to another. Has **headers/metadata** + a **body/payload**. |
| **Producer / Publisher / Sender** | The code that creates and sends messages. "Publisher" is usually used for events (pub/sub). |
| **Consumer / Subscriber / Receiver** | The code that receives and processes messages. |
| **Broker** | The middleware server that receives, stores and routes messages (RabbitMQ, Kafka, Azure Service Bus). |
| **Queue** | A buffer that stores messages until a consumer takes them. Each message is processed by **one** consumer. |
| **Topic** | A named channel for pub/sub. One message can reach **many** subscribers. |
| **Subscription** | A subscriber's own "view" of a topic (in Service Bus it behaves like a private queue fed by the topic). |
| **Exchange** (RabbitMQ) | The routing component. Producers publish to an exchange; it decides which queues get a copy. |
| **Binding** (RabbitMQ) | A rule connecting an exchange to a queue (optionally with a routing key/pattern). |
| **Routing key** | A label on the message used to route it (e.g. `order.created.eu`). |
| **Partition** (Kafka, Event Hubs) | A topic is split into ordered, append-only logs called partitions — the unit of ordering and parallelism. |
| **Offset** (Kafka) | The position of a message in a partition. Consumers track "how far did I read". |
| **Consumer group** (Kafka) | A set of consumers sharing the work of a topic; each partition is read by one member of the group. |
| **Channel / Connection** | Connection = TCP connection to the broker (expensive). Channel = lightweight virtual connection inside it (RabbitMQ). |

---

## 2. Anatomy of a message (the envelope)

A good message is like a letter: an **envelope** (metadata) and the **content** (payload).

```json
{
  "headers": {
    "messageId":     "6f1c...e9",            // unique id of THIS message → deduplication
    "correlationId": "order-1234",           // ties messages of the same business flow together
    "causationId":   "a11b...02",            // id of the message that caused this one
    "messageType":   "OrderPlaced.v1",       // what is it → which handler/deserializer
    "timestamp":     "2026-10-28T10:15:00Z",
    "source":        "orders-service",
    "contentType":   "application/json",
    "replyTo":       "orders.replies",       // only for request/reply
    "traceparent":   "00-4bf9...-01"         // distributed tracing (W3C)
  },
  "body": {
    "orderId": 1234,
    "customerId": 42,
    "total": 99.90
  }
}
```

| Header | Why it matters |
|--------|----------------|
| **MessageId** | Detect duplicates (idempotency). |
| **CorrelationId** | Find all messages/logs of one business flow (e.g. one order). |
| **CausationId** | Build the "who caused what" chain. |
| **Type / Subject** | Route and deserialize. |
| **Timestamp** | Debugging, TTL, ordering decisions. |
| **ReplyTo** | Where to send the answer in request/reply. |
| **TTL / Expiration** | Drop the message if it's too old to matter. |
| **Content type / encoding** | JSON, Avro, Protobuf… |

> Tip: put **routing/technical** info in headers, **business** data in the body. Many brokers can filter on headers without deserializing the body.

---

## 3. Lifecycle of a message (generic)

```
1. Producer creates message  →  2. Sends to broker  →  3. Broker stores it (memory/disk)
   →  4. Broker delivers to a consumer  →  5. Consumer processes
   →  6a. ACK  → broker deletes it (queue) / consumer commits offset (log)
   →  6b. NACK / timeout → broker redelivers (maybe to another consumer)
   →  7. After too many failures → Dead-Letter Queue
```

---

## 4. Durability words

| Term | Meaning |
|------|---------|
| **Durable queue** | The queue definition survives a broker restart. |
| **Persistent message** | The message is written to disk, so it survives a broker restart. |
| **Transient / non-persistent** | In memory only. Faster, lost on restart. |
| **Replication** | Copies on several broker nodes (RabbitMQ quorum queues, Kafka replication factor). Survives a node failure. |
| **Retention** (log brokers) | How long messages are kept *after* being read (Kafka: e.g. 7 days). Queues delete on ack; logs keep by time/size. |

> In RabbitMQ you need **both** a durable queue **and** persistent messages to survive a restart. A common interview trap.

---

## 5. Same idea, different names

| Generic concept | RabbitMQ | Kafka | Azure Service Bus | AWS |
|-----------------|----------|-------|-------------------|-----|
| Point-to-point channel | Queue | Topic + one consumer group | Queue | SQS |
| Pub/sub channel | Exchange (fanout/topic) + queues | Topic + multiple consumer groups | Topic + subscriptions | SNS (+ SQS) |
| Routing | Exchange + binding + routing key | Partition key | Subscription filters / rules | SNS filter policy |
| Unit of order | Queue (single consumer) | Partition | Session | SQS FIFO message group |
| Acknowledge | `basic.ack` | Commit offset | Complete | DeleteMessage |
| Dead letter | DLX (dead-letter exchange) | (none built-in; you create a DLT topic) | `$DeadLetterQueue` sub-queue | DLQ (redrive policy) |

---

## Interview one-liners

- "A queue delivers each message to one consumer; a topic delivers a copy to every subscription."
- "Every message should carry a MessageId for deduplication and a CorrelationId for tracing the business flow."
- "In RabbitMQ, durability needs a durable queue *and* persistent messages; for node failures you need replicated (quorum) queues."

## Check yourself

1. What's the difference between an exchange and a queue in RabbitMQ?
2. What's the difference between MessageId and CorrelationId?
3. What is an offset, and who stores it?
4. Map "Service Bus subscription" to its RabbitMQ and Kafka equivalents.

<div dir="rtl" lang="fa">

**خلاصه:** تولیدکننده (Producer) پیام را به بروکر می‌فرستد؛ بروکر آن را از طریق Exchange/Topic به صف یا Subscription می‌رساند و مصرف‌کننده (Consumer) آن را پردازش و Ack می‌کند. هر پیام شامل هدر (MessageId برای حذف تکراری، CorrelationId برای ردیابی جریان، نوع پیام، زمان) و بدنه (داده تجاری) است. برای ماندگاری در RabbitMQ هم صف باید Durable باشد و هم پیام Persistent.

</div>
