# Lab 02 — Failures, Retries, Dead Letters & Idempotency

**Goal:** experience the failure modes of at-least-once messaging and fix them: infinite redelivery loops, poison messages, delayed retries, dead-letter queues, and duplicates.

**Concepts:** [05 Delivery Guarantees & Idempotency](../concepts/05-Delivery-Guarantees-Idempotency.md) · [06 Failures, Retries & DLQ](../concepts/06-Failures-Retries-DLQ.md)

**Time:** ~2 hours. **Prerequisite:** Lab 01 done, RabbitMQ container running.

---

## Topology you'll end up with

```
                       ┌──────────── fail (retry < 3) ──────────────┐
                       ▼                                             │
Producer ──► [ payments ]  ──► Consumer ── ok ──► ack                │
              (quorum, delivery-limit)  │                            │
                       ▲                └── fail ──► [ payments.retry ] (TTL 5s) ─┘
                       │                                    expires → back to payments
                       │
              too many deliveries / poison
                       ▼
            ( payments.dlx ) ──► [ payments.dlq ]
```

## Step 1 — Create the solution

```powershell
mkdir C:\Labs\messaging\lab02-failures; cd C:\Labs\messaging\lab02-failures
dotnet new sln -n Lab02
dotnet new console -n Producer
dotnet new console -n Consumer
dotnet sln add Producer Consumer
dotnet add Producer package RabbitMQ.Client
dotnet add Consumer package RabbitMQ.Client
```

## Step 2 — Producer: payment commands with a stable MessageId

**Producer/Program.cs**

```csharp
using System.Text.Json;
using RabbitMQ.Client;

// usage: dotnet run -- <amount> [messageId]
var amount = decimal.Parse(args.ElementAtOrDefault(0) ?? "10");
var messageId = args.ElementAtOrDefault(1) ?? Guid.NewGuid().ToString();

var factory = new ConnectionFactory { HostName = "localhost" };
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

var body = JsonSerializer.SerializeToUtf8Bytes(new { PaymentId = messageId, Amount = amount });
var props = new BasicProperties
{
    Persistent = true, MessageId = messageId,
    Type = "ChargePayment.v1", ContentType = "application/json"
};

await channel.BasicPublishAsync(string.Empty, "payments", mandatory: true, props, body);
Console.WriteLine($" [x] ChargePayment {amount} (id {messageId})");
```

Rules our fake payment handler will follow:
- `amount < 0` → **poison** (invalid data, never succeeds).
- `amount == 13` → **transient** failure (fails ~70% of the time, like a flaky API).
- anything else → success.

## Step 3 — The naive consumer: infinite redelivery loop

**Consumer/Program.cs** (first version)

```csharp
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory { HostName = "localhost" };
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync("payments", durable: true, exclusive: false, autoDelete: false, arguments: null);
await channel.BasicQosAsync(0, 1, false);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, ea) =>
{
    var cmd = JsonSerializer.Deserialize<ChargePayment>(ea.Body.Span)!;
    try
    {
        Handle(cmd);
        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($" [!] {cmd.PaymentId}: {ex.Message} → requeue");
        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
    }
};
await channel.BasicConsumeAsync("payments", autoAck: false, consumer);
Console.ReadLine();

static void Handle(ChargePayment cmd)
{
    if (cmd.Amount < 0) throw new InvalidOperationException("Invalid amount (poison)");
    if (cmd.Amount == 13 && Random.Shared.NextDouble() < 0.7) throw new TimeoutException("Payment API timeout");
    Console.WriteLine($" [✓] Charged {cmd.Amount} for {cmd.PaymentId}");
}

record ChargePayment(string PaymentId, decimal Amount);
```

**Run:** start the consumer, then `dotnet run --project Producer -- -5`.

**What you see:** the consumer spins **forever**, printing the error thousands of times. CPU goes up. In a real system this message also blocks or delays others. That's a **poison message** with no protection.

Stop the consumer. In the UI, **delete** the `payments` queue (we'll redeclare it with new arguments).

## Step 4 — Quorum queue + delivery limit + dead-letter exchange

Replace the queue declaration in the consumer:

```csharp
await channel.ExchangeDeclareAsync("payments.dlx", ExchangeType.Fanout, durable: true);
await channel.QueueDeclareAsync("payments.dlq", durable: true, exclusive: false, autoDelete: false, arguments: null);
await channel.QueueBindAsync("payments.dlq", "payments.dlx", string.Empty);

await channel.QueueDeclareAsync("payments", durable: true, exclusive: false, autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
        ["x-queue-type"] = "quorum",            // replicated, supports delivery limit
        ["x-delivery-limit"] = 3,               // after 3 redeliveries → dead-letter
        ["x-dead-letter-exchange"] = "payments.dlx"
    });
```

Run the poison message again (`-- -5`).

**What you see:** a few attempts, then it stops. In the UI, `payments.dlq` has **1 message**. Click *Get messages* on the DLQ and look at the headers: RabbitMQ added `x-death` / `x-first-death-reason` information telling you **why** and **from where** it was dead-lettered.

> Interview link: "After N failed deliveries the poison message is moved to a DLQ so the main queue keeps flowing."

🔨 **Break it:** try to redeclare `payments` as a classic queue with different arguments without deleting it → `PRECONDITION_FAILED`. Queue arguments are fixed at creation time — in production you manage topology with IaC/definitions, not ad hoc.

## Step 5 — Fail fast on poison, retry only transient errors

Retrying `-5` three times is pointless — it's **permanent**. Classify the exception:

```csharp
catch (InvalidOperationException ex)          // permanent → straight to DLQ
{
    Console.WriteLine($" [x] {cmd.PaymentId}: {ex.Message} → dead-letter");
    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);   // requeue:false → DLX
}
catch (Exception ex)                          // transient → retry
{
    Console.WriteLine($" [!] {cmd.PaymentId}: {ex.Message} → requeue");
    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
}
```

Also handle **unparseable** messages: wrap the `Deserialize` call — a `JsonException` is poison too. Test it by publishing `not json` from the UI (*Queues → payments → Publish message*).

## Step 6 — Delayed retries with a retry queue (backoff)

Immediate requeue retries in milliseconds — useless if the payment API is down for 10 seconds. We want **"try again in 5 seconds"** without blocking the consumer thread.

Trick: a **wait queue** with a TTL whose dead-letter target is the original queue.

Add to the consumer setup:

```csharp
await channel.QueueDeclareAsync("payments.retry", durable: true, exclusive: false, autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
        ["x-message-ttl"] = 5000,                       // wait 5s
        ["x-dead-letter-exchange"] = string.Empty,      // then go to the default exchange...
        ["x-dead-letter-routing-key"] = "payments"      // ...with routing key = original queue
    });
```

Change the transient branch: **republish to the retry queue with a counter header**, then ack the original.

```csharp
catch (Exception ex)
{
    var attempt = GetAttempt(ea.BasicProperties) + 1;
    if (attempt > 3)
    {
        Console.WriteLine($" [x] {cmd.PaymentId}: giving up after {attempt - 1} retries → dead-letter");
        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
        return;
    }

    Console.WriteLine($" [!] {cmd.PaymentId}: {ex.Message} → retry #{attempt} in 5s");
    var props = new BasicProperties(ea.BasicProperties)       // copy original properties
    {
        Headers = new Dictionary<string, object?> { ["x-retry-attempt"] = attempt }
    };
    await channel.BasicPublishAsync(string.Empty, "payments.retry", mandatory: true, props, ea.Body);
    await channel.BasicAckAsync(ea.DeliveryTag, false);
}
```

```csharp
static int GetAttempt(IReadOnlyBasicProperties p) =>
    p.Headers?.TryGetValue("x-retry-attempt", out var v) == true ? Convert.ToInt32(v) : 0;
```

**Run:** `dotnet run --project Producer -- 13` a few times.

**What you see:** failures, a 5-second pause, retries, eventually success — or the DLQ after 3 retries. Watch `payments.retry` in the UI filling and emptying.

**Questions to think about:**
- Why do we **publish then ack** — and what happens if we crash between those two lines? (Hint: a duplicate. Which is fine *if* the handler is idempotent → Step 7.)
- How would you do **exponential** backoff? (Several retry queues: `payments.retry.5s`, `.30s`, `.5m` — pick by attempt.)
- Why not just `await Task.Delay(5000)` inside the handler? (Blocks the consumer, holds the message, lowers throughput, risks redelivery on crash.)

## Step 7 — Duplicates & idempotency

**Create a duplicate on purpose.** Send the same payment twice with the **same MessageId** (a producer retry after a lost confirm looks exactly like this):

```powershell
dotnet run --project Producer -- 25 pay-001
dotnet run --project Producer -- 25 pay-001
```

The consumer charges the customer **twice**. 💸

**Fix v1 — in-memory dedup:**

```csharp
var processed = new HashSet<string>();     // outside the handler
...
if (!processed.Add(ea.BasicProperties.MessageId!))
{
    Console.WriteLine($" [=] duplicate {ea.BasicProperties.MessageId} → skip");
    await channel.BasicAckAsync(ea.DeliveryTag, false);
    return;
}
```

Repeat → second one skipped. ✔

🔨 **Break it:** restart the consumer and send `pay-001` again → charged again. In-memory dedup is lost on restart and isn't shared between competing consumers. It must be **durable and in the same transaction as the business change** → that's exactly what you'll build in **Lab 03 (Inbox)**.

🔨 **Break it 2 — crash before ack:** add this after `Handle(cmd)`:

```csharp
if (cmd.Amount == 99) Environment.Exit(1);   // crash after charging, before ack
```

Send `99`, restart the consumer (without the line) → the payment is charged **again** (redelivered). Write in your notes: *this* is why at-least-once implies duplicates.

## Step 8 — Inspect and replay the DLQ

Use the management UI:
1. *Queues → payments.dlq → Get messages* (Ack mode: "Nack message requeue true" to just peek).
2. Read the `x-death` header: `reason` (`rejected`, `expired`, `delivery_limit`), `queue`, `count`.
3. **Replay:** in the UI under *Admin → Shovel Management* you can move messages back (requires the shovel plugin: `docker exec rabbitmq rabbitmq-plugins enable rabbitmq_shovel rabbitmq_shovel_management`). Or write a tiny console app that consumes `payments.dlq` and republishes to `payments`.

> Bonus exercise: write that `DlqReplayer` console app. It's ~30 lines and a real tool teams build.

---

## ✅ Checkpoint

1. Why is `requeue: true` on every error dangerous?
2. What three things can send a message to a dead-letter exchange?
3. How does the retry-queue trick delay a message without blocking the consumer?
4. What's the difference between how you handle `InvalidOperationException` and `TimeoutException` here?
5. Why is in-memory dedup not enough? What are the two requirements for a correct dedup store?

## 🧹 Cleanup

Delete the lab queues in the UI (or `docker rm -f rabbitmq` to reset everything).

Next: [Lab 03 — Reliable Orders: Outbox + Inbox](03-Outbox-Inbox.md)
