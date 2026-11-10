# Lab 01 — RabbitMQ Fundamentals

**Goal:** see with your own eyes how queues, acks, durability, competing consumers, pub/sub and routing work.

**You'll build:** a `Producer` and a `Consumer` console app that evolve step by step.

**Concepts:** [02 Building Blocks](../concepts/02-Building-Blocks.md) · [03 Message Types](../concepts/03-Message-Types.md) · [04 Communication Models](../concepts/04-Communication-Models.md) · [05 Ack](../concepts/05-Delivery-Guarantees-Idempotency.md)

**Time:** 2–3 hours.

---

## Step 1 — Start RabbitMQ

```powershell
docker run -d --name rabbitmq --hostname rabbit `
  -p 5672:5672 -p 15672:15672 `
  rabbitmq:4-management
```

- `5672` = AMQP port (your apps).
- `15672` = management UI → open http://localhost:15672, login `guest` / `guest`.
- `--hostname rabbit` keeps the node name stable so data survives `docker restart`.

**Explore the UI:** tabs *Connections*, *Channels*, *Exchanges* (notice the built-in ones like `amq.direct` and the nameless **default exchange**), *Queues*.

## Step 2 — Create the solution

```powershell
mkdir C:\Labs\messaging\lab01-rabbitmq; cd C:\Labs\messaging\lab01-rabbitmq
dotnet new sln -n Lab01
dotnet new console -n Producer
dotnet new console -n Consumer
dotnet sln add Producer Consumer
dotnet add Producer package RabbitMQ.Client
dotnet add Consumer package RabbitMQ.Client
```

## Step 3 — Hello queue (auto-ack)

**Producer/Program.cs**

```csharp
using System.Text;
using RabbitMQ.Client;

var factory = new ConnectionFactory { HostName = "localhost" };
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false,
                                autoDelete: false, arguments: null);

var body = Encoding.UTF8.GetBytes($"Hello messaging! {DateTime.Now:T}");
await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "hello", body: body);

Console.WriteLine(" [x] Sent");
```

**Consumer/Program.cs**

```csharp
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory { HostName = "localhost" };
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false,
                                autoDelete: false, arguments: null);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += (_, ea) =>
{
    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($" [x] Received: {message}");
    return Task.CompletedTask;
};

await channel.BasicConsumeAsync(queue: "hello", autoAck: true, consumer: consumer);

Console.WriteLine(" Waiting for messages. Press [enter] to exit.");
Console.ReadLine();
```

**Run:**

1. Run the **Producer** 3 times *without* the consumer running.
   ```powershell
   dotnet run --project Producer
   ```
   → In the UI, queue `hello` shows **Ready = 3**. The messages wait — that's **temporal decoupling**.
2. Start the consumer: `dotnet run --project Consumer` → it prints all 3.

**Why:** we published to the **default exchange** (`""`), which routes to the queue whose name equals the routing key. Both apps declare the queue — declaring is idempotent, whoever starts first creates it.

## Step 4 — Durability (lose messages, then stop losing them)

1. Stop the consumer. Run the producer 3 times. Queue shows Ready = 3.
2. Restart the broker: `docker restart rabbitmq`.
3. Refresh the UI → **the `hello` queue is gone** (non-durable) and so are your messages.

Now fix it. Use a new queue name `tasks` (you can't change the durability of an existing queue — try it and read the `PRECONDITION_FAILED` error!).

**Producer:**

```csharp
await channel.QueueDeclareAsync(queue: "tasks", durable: true, exclusive: false,
                                autoDelete: false, arguments: null);

var props = new BasicProperties { Persistent = true };   // write message to disk
await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "tasks",
                                mandatory: true, basicProperties: props, body: body);
```

Update the consumer to declare/consume `tasks` (durable: true) as well. Repeat 1–3 → messages **survive** the restart.

🔨 **Break it:** keep `durable: true` but remove `Persistent = true`. Restart. The queue survives, the messages don't. You need **both**.

## Step 5 — Manual ack (and what a crash does)

Make the producer send a "task" whose number of dots = seconds of work. Replace the body line:

```csharp
var message = args.Length > 0 ? string.Join(' ', args) : "Hello...";
var body = Encoding.UTF8.GetBytes(message);
```

Change the consumer to **simulate work** and **ack manually**:

```csharp
consumer.ReceivedAsync += async (_, ea) =>
{
    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($" [x] Received '{message}' (redelivered: {ea.Redelivered})");

    int seconds = message.Count(c => c == '.');
    await Task.Delay(seconds * 1000);

    Console.WriteLine(" [x] Done");
    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
};

await channel.BasicConsumeAsync(queue: "tasks", autoAck: false, consumer: consumer);
```

**Run:**

```powershell
dotnet run --project Producer -- "Long task.........."     # 10 seconds of work
```

While the consumer is "working", **kill it** (`Ctrl+C`). Look at the UI: the message moves from *Unacked* back to *Ready*. Start the consumer again → `redelivered: True`.

🔨 **Break it:** set `autoAck: true`, send the long task, kill the consumer mid-work → the message is **gone forever**. That is **at-most-once**. With manual ack after the work you get **at-least-once**.

🔨 **Break it 2:** forget to call `BasicAckAsync` (comment it out). Process a few messages, then stop the consumer → all of them come back. "Forgotten acks" is a classic production bug (memory grows, messages redelivered).

## Step 6 — Competing consumers & prefetch

1. Open **3 terminals**: two consumers and one producer.
2. Send tasks of different lengths:

```powershell
dotnet run --project Producer -- "A.........."   # 10s
dotnet run --project Producer -- "B."
dotnet run --project Producer -- "C.........."
dotnet run --project Producer -- "D."
dotnet run --project Producer -- "E.........."
dotnet run --project Producer -- "F."
```

**What you see:** RabbitMQ dispatches **round-robin** — one consumer may get all the long tasks while the other sits idle.

Fix with **fair dispatch** — add to the consumer before `BasicConsumeAsync`:

```csharp
await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
```

Repeat → a consumer only gets a new message after acking the previous one. Work is balanced.

**Observe ordering:** with two consumers, `B` may finish before `A`. Competing consumers **break ordering**.

## Step 7 — Message metadata

Add proper metadata in the producer:

```csharp
var props = new BasicProperties
{
    Persistent = true,
    MessageId = Guid.NewGuid().ToString(),
    CorrelationId = "order-1234",
    Type = "ProcessTask.v1",
    ContentType = "text/plain",
    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
    Headers = new Dictionary<string, object?> { ["source"] = "lab01-producer" }
};
```

Print them in the consumer:

```csharp
var p = ea.BasicProperties;
Console.WriteLine($"   id={p.MessageId} corr={p.CorrelationId} type={p.Type}");
if (p.Headers?.TryGetValue("source", out var src) == true && src is byte[] bytes)
    Console.WriteLine($"   source={Encoding.UTF8.GetString(bytes)}");
```

Note: string headers arrive as `byte[]` — AMQP has no native string type for header values on the wire.

In the UI: *Queues → tasks → Get messages* lets you peek a message and see all properties.

## Step 8 — Publish/Subscribe with a fanout exchange

Now model an **event**: `OrderPlaced` should reach **Email** *and* **Inventory**.

Create a new console app:

```powershell
dotnet new console -n Publisher
dotnet sln add Publisher
dotnet add Publisher package RabbitMQ.Client
```

**Publisher/Program.cs**

```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

var factory = new ConnectionFactory { HostName = "localhost" };
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync(exchange: "orders.events", type: ExchangeType.Fanout, durable: true);

var orderId = Random.Shared.Next(1000, 9999);
var evt = new { OrderId = orderId, Total = 49.90m, PlacedAt = DateTime.UtcNow };
var body = JsonSerializer.SerializeToUtf8Bytes(evt);

var props = new BasicProperties
{
    Persistent = true, MessageId = Guid.NewGuid().ToString(),
    Type = "OrderPlaced.v1", ContentType = "application/json"
};
await channel.BasicPublishAsync("orders.events", routingKey: string.Empty, mandatory: false, props, body);
Console.WriteLine($" [x] Published OrderPlaced {orderId}");
```

Make the **Consumer** reusable — pass the queue name and exchange as arguments. Replace its setup:

```csharp
var queue    = args.ElementAtOrDefault(0) ?? "tasks";
var exchange = args.ElementAtOrDefault(1);            // null = no binding
var binding  = args.ElementAtOrDefault(2) ?? string.Empty;

await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
if (exchange is not null)
    await channel.QueueBindAsync(queue, exchange, binding);
```

(Use `queue` in `BasicConsumeAsync` too, and declare the exchange in the consumer as well, or start the publisher once first.)

**Run** (3 terminals):

```powershell
dotnet run --project Consumer -- email.order-placed     orders.events
dotnet run --project Consumer -- inventory.order-placed orders.events
dotnet run --project Publisher
```

**What you see:** **both** consumers get every event. Each subscriber has its **own queue** — that's how RabbitMQ does pub/sub.

Now start a **second instance** of the email consumer (same queue `email.order-placed`). Publish several events → the two email instances **compete**, inventory still gets everything.

> 💡 *Pub/sub between services, competing consumers inside a service.* You just built it.

🔨 **Break it:** stop the inventory consumer, publish 5 events, start it again → it receives all 5 (its durable queue buffered them). Then delete its queue in the UI, publish again → messages for inventory are **dropped**: a fanout exchange with no bound queue discards messages. Subscribers must exist (bind) *before* events are published.

## Step 9 — Routing with a topic exchange

Declare a topic exchange `shop.events` and publish with routing keys like `order.placed.eu`, `order.placed.us`, `order.cancelled.eu`.

Change the publisher:

```csharp
await channel.ExchangeDeclareAsync("shop.events", ExchangeType.Topic, durable: true);
var routingKey = args.ElementAtOrDefault(0) ?? "order.placed.eu";
await channel.BasicPublishAsync("shop.events", routingKey, mandatory: false, props, body);
Console.WriteLine($" [x] Published with key {routingKey}");
```

Run consumers with different binding patterns (`*` = exactly one word, `#` = zero or more words):

```powershell
dotnet run --project Consumer -- eu-orders    shop.events "order.*.eu"
dotnet run --project Consumer -- placed-only  shop.events "order.placed.*"
dotnet run --project Consumer -- audit        shop.events "#"
```

```powershell
dotnet run --project Publisher -- order.placed.eu
dotnet run --project Publisher -- order.placed.us
dotnet run --project Publisher -- order.cancelled.eu
dotnet run --project Publisher -- invoice.paid.eu
```

**Predict first**, then check which consumer prints what. Fill in this table in your notes:

| Routing key | eu-orders | placed-only | audit |
|-------------|-----------|-------------|-------|
| order.placed.eu | | | |
| order.placed.us | | | |
| order.cancelled.eu | | | |
| invoice.paid.eu | | | |

---

## ✅ Checkpoint

1. Why did the `hello` queue disappear after a restart? What two settings fix it?
2. What exactly happens to an unacked message when the consumer dies?
3. What does `prefetchCount: 1` change, and what does it cost?
4. How did you get both pub/sub and competing consumers at the same time?
5. A fanout exchange has no queues bound. What happens to published messages?
6. Which routing key reaches `order.*.eu` but not `order.#.eu`? (Trick question — think about it.)

## 🧹 Cleanup

```powershell
docker rm -f rabbitmq      # or keep it for Lab 02
```

Next: [Lab 02 — Failures, Retries, DLQ, Idempotency](02-Failures-Retries-Idempotency.md)
