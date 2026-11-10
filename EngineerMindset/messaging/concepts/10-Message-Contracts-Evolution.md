# 10 — Message Contracts & Schema Evolution

> **Level 3 — Distributed design.** Messages are an **API**. Producers and consumers deploy independently, so contracts must evolve without breaking anyone.

---

## 1. Why contracts are hard in messaging

- Producers and consumers deploy at **different times**.
- Old messages may still be **in the queue** (or in Kafka for days) when the new code deploys.
- With pub/sub, the producer may not even **know** all consumers.
- With log replay, consumers may read messages from **months ago**.

So at any moment, **multiple versions** of a message exist in flight.

---

## 2. Compatibility types

| Type | Meaning | Allows |
|------|---------|--------|
| **Backward compatible** | **New consumers** can read **old** messages | Upgrade consumers first |
| **Forward compatible** | **Old consumers** can read **new** messages | Upgrade producers first |
| **Full** | Both | Deploy in any order |

### Safe vs breaking changes (JSON/Avro/Protobuf, roughly)

| Change | Safe? |
|--------|-------|
| Add an **optional** field (with default) | ✅ Safe |
| Remove an optional field that consumers tolerate missing | ⚠️ Usually OK |
| Add a **required** field | ❌ Breaks old producers' messages for new consumers |
| Rename a field | ❌ Breaking (= remove + add) |
| Change a field's **type** (`int` → `string`) | ❌ Breaking |
| Change the **meaning** of a field (amount in cents instead of euros) | ❌ Silently breaking — the worst kind |
| Reuse a removed Protobuf field number | ❌ Corrupts data |

### Tolerant Reader
Consumers should **ignore unknown fields** and only read what they need. (System.Text.Json ignores unknown properties by default.) This one principle prevents most breakages.

---

## 3. Versioning strategies

| Strategy | Example | Notes |
|----------|---------|-------|
| **Additive changes only** | Add `discountCode?` to `OrderPlaced` | Preferred. No version bump needed. |
| **Version in type name** | `OrderPlaced.v1`, `OrderPlaced.v2` | For breaking changes. Publish **both** during migration, retire v1 later. |
| **Version header** | `schemaVersion: 2` | Consumer branches on version. |
| **New topic** | `orders.v2` | Clean break; costly migration. |
| **Upcasting** | Convert v1 → v2 on read | Common in event sourcing. |

Migration recipe for a breaking change:
1. Producer publishes **v1 and v2** (or v2 only, if consumers are upgraded first).
2. Consumers migrate to v2 one by one.
3. Monitor until no one consumes v1.
4. Stop publishing v1.

---

## 4. Serialization formats

| Format | Schema? | Size/speed | Notes |
|--------|---------|------------|-------|
| **JSON** | Optional (JSON Schema) | Larger, slower | Human-readable, universal. Default for most business messaging. |
| **Avro** | Yes, required | Compact | Very common with Kafka + Schema Registry; strong evolution rules. |
| **Protobuf** | Yes (`.proto`) | Compact, fast | Field numbers; great tooling; gRPC heritage. |
| **MessagePack** | No | Compact | Binary JSON. |

### Schema Registry
A service (Confluent Schema Registry, Azure Schema Registry, Apicurio) that stores schemas and **enforces compatibility rules** on publish. Messages carry a small **schema id** instead of the full schema. Prevents a producer from shipping a breaking change.

---

## 5. Sharing contracts in .NET (practical)

| Approach | Pros | Cons |
|----------|------|------|
| **Shared NuGet package of message classes** | Type safety, easy | Version lock-step between teams; "distributed monolith" smell |
| **Schema-first** (JSON Schema / Avro / Protobuf files) → generate code | Language-neutral, registry checks | Tooling |
| **Each consumer defines its own DTO** (tolerant reader) | Max independence | No compile-time check; rely on contract tests |
| **Consumer-driven contract tests** (e.g. Pact) | Catches breakage in CI | Setup effort |

---

## 6. Standard envelopes: CloudEvents

**CloudEvents** (CNCF spec) standardizes event metadata: `id`, `source`, `type`, `specversion`, `time`, `subject`, `datacontenttype`, `data`. Supported by Azure Event Grid, Knative, many SDKs. Use it when events cross organizational or platform boundaries.

```json
{
  "specversion": "1.0",
  "id": "6f1c-...",
  "source": "/orders-service",
  "type": "com.shop.order.placed.v1",
  "time": "2026-10-28T10:15:00Z",
  "datacontenttype": "application/json",
  "data": { "orderId": 1234, "total": 99.9 }
}
```

---

## 7. Large messages: Claim Check

Brokers limit message size (Service Bus Standard: 256 KB, Premium: up to 100 MB; Kafka default ~1 MB; SQS 256 KB; RabbitMQ allows big ones but they hurt performance).

**Claim Check pattern:** store the big payload in blob storage, send a message with a **reference** (URL/blob id). The consumer fetches the payload.

```
Producer ──upload──► Blob Storage
         ──{ blobUri, sha256 }──► Broker ──► Consumer ──download──► Blob Storage
```

Remember: lifecycle/cleanup of blobs, access control (SAS tokens), and integrity checks.

---

## 8. Privacy & data in messages

- Messages get copied, logged, retained, and dead-lettered → **avoid sensitive data** (PII, card numbers) or encrypt it.
- GDPR "right to be forgotten" vs immutable logs: techniques include **crypto-shredding** (encrypt per-user, delete the key).

---

## Interview one-liners

- "I treat messages as public APIs: additive changes only, tolerant readers, and a versioned type for breaking changes with a dual-publish migration."
- "With Kafka I'd use Avro or Protobuf with a Schema Registry enforcing backward compatibility."
- "For large payloads I use the claim-check pattern: blob storage plus a reference in the message."

## Check yourself

1. Is adding a required field backward compatible? Forward compatible?
2. Old messages are still in the DLQ when you deploy v2. What could go wrong?
3. Why can a shared NuGet contracts package become a problem?
4. What does a Schema Registry protect you from?

<div dir="rtl" lang="fa">

**خلاصه:** پیام‌ها مثل API عمومی هستند و نسخه‌های مختلف همزمان در جریان‌اند. Backward Compatible یعنی مصرف‌کننده جدید پیام قدیمی را بخواند؛ Forward یعنی مصرف‌کننده قدیمی پیام جدید را بخواند. فقط فیلد اختیاری اضافه کن، فیلدهای ناشناخته را نادیده بگیر (Tolerant Reader)، و برای تغییرات شکننده نسخه جدید (v2) را موازی منتشر کن. با Kafka معمولاً Avro/Protobuf + Schema Registry. برای پیام بزرگ از الگوی Claim Check (ذخیره در Blob و ارسال آدرس) استفاده کن.

</div>
