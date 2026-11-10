# Lab 06 — Choreography Saga with Compensation

**Goal:** build a multi-service order workflow where **no service calls another**: each reacts to events, and failures trigger **compensating actions**. Follow one order through all services with a **CorrelationId**.

**Concepts:** [09 Sagas](../concepts/09-Sagas-Workflows.md) · [03 Message Types](../concepts/03-Message-Types.md) · [12 Observability](../concepts/12-Observability-Security-Operations.md)

**Time:** 3–4 hours. **Prerequisite:** Labs 01–03, RabbitMQ running.

---

## The workflow

```
Orders     ──OrderPlaced──────────►  Inventory
                                        │ stock ok?
                    ┌──────── no ───────┴──────── yes ────────┐
                    ▼                                          ▼
     StockReservationFailed                             StockReserved
                    │                                          │
                    ▼                                          ▼
          Orders: Cancelled                              Payments  (amount > 1000 → decline)
                                        ┌──────── no ──────────┴──────── yes ───────┐
                                        ▼                                           ▼
                                  PaymentFailed                              PaymentSucceeded
                                        │                                           │
                                        ▼                                           ▼
                            Inventory: release stock  (COMPENSATION)        Orders: Confirmed ✔
                                        │
                                        ▼
                                  StockReleased ──► Orders: Cancelled
```

All events go through one **topic exchange** `shop.events`, routing key = event type name. Each service has its own queue bound to the events it cares about.

| Service | Listens to | Publishes |
|---------|-----------|-----------|
| Orders | `PaymentSucceeded`, `StockReservationFailed`, `StockReleased` | `OrderPlaced` |
| Inventory | `OrderPlaced`, `PaymentFailed` | `StockReserved`, `StockReservationFailed`, `StockReleased` |
| Payments | `StockReserved` | `PaymentSucceeded`, `PaymentFailed` |

## Step 1 — Solution layout

```powershell
mkdir C:\Labs\messaging\lab06-saga; cd C:\Labs\messaging\lab06-saga
dotnet new sln -n Lab06
dotnet new classlib -n Shop.Messaging
dotnet new console  -n Orders
dotnet new console  -n Inventory
dotnet new console  -n Payments
dotnet sln add Shop.Messaging Orders Inventory Payments

dotnet add Shop.Messaging package RabbitMQ.Client
foreach ($p in "Orders", "Inventory", "Payments") { dotnet add $p reference Shop.Messaging }
```

> A shared contracts library is the simplest choice for a lab. Remember the trade-off from [concept 10](../concepts/10-Message-Contracts-Evolution.md): in real systems it couples deployments.

## Step 2 — Contracts

**Shop.Messaging/Events.cs**

```csharp
namespace Shop.Messaging;

public record OrderPlaced(Guid OrderId, string Sku, int Quantity, decimal Amount);
public record StockReserved(Guid OrderId, string Sku, int Quantity, decimal Amount);   // carries what Payments needs (ECST)
public record StockReservationFailed(Guid OrderId, string Reason);
public record PaymentSucceeded(Guid OrderId);
public record PaymentFailed(Guid OrderId, string Sku, int Quantity, string Reason);   // carries what Inventory needs to compensate
public record StockReleased(Guid OrderId);
```

## Step 3 — A tiny event bus over RabbitMQ

**Shop.Messaging/RabbitBus.cs**

```csharp
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Shop.Messaging;

public sealed class RabbitBus : IAsyncDisposable
{
    private const string Exchange = "shop.events";
    private readonly string _service;
    private readonly IConnection _connection;
    private readonly IChannel _publishChannel;
    private readonly IChannel _consumeChannel;
    private readonly Dictionary<string, Func<ReadOnlyMemory<byte>, string, Task>> _handlers = new();

    private RabbitBus(string service, IConnection connection, IChannel publish, IChannel consume)
        => (_service, _connection, _publishChannel, _consumeChannel) = (service, connection, publish, consume);

    public static async Task<RabbitBus> ConnectAsync(string service)
    {
        var factory = new ConnectionFactory { HostName = "localhost", ClientProvidedName = service };
        var connection = await factory.CreateConnectionAsync();
        var publish = await connection.CreateChannelAsync();
        var consume = await connection.CreateChannelAsync();
        await publish.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        return new RabbitBus(service, connection, publish, consume);
    }

    public async Task PublishAsync<T>(T message, string correlationId) where T : class
    {
        var props = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            Type = typeof(T).Name,
            ContentType = "application/json",
            Persistent = true,
            Headers = new Dictionary<string, object?> { ["source"] = _service }
        };
        await _publishChannel.BasicPublishAsync(Exchange, typeof(T).Name, mandatory: false, props,
                                                JsonSerializer.SerializeToUtf8Bytes(message));
        Log(correlationId, $"PUBLISHED {typeof(T).Name}");
    }

    public void On<T>(Func<T, string, Task> handler) where T : class =>
        _handlers[typeof(T).Name] = (body, correlationId) =>
            handler(JsonSerializer.Deserialize<T>(body.Span)!, correlationId);

    public async Task StartAsync()
    {
        var queue = $"{_service.ToLowerInvariant()}.events";
        await _consumeChannel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        foreach (var eventType in _handlers.Keys)
            await _consumeChannel.QueueBindAsync(queue, Exchange, routingKey: eventType);

        var consumer = new AsyncEventingBasicConsumer(_consumeChannel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var type = ea.BasicProperties.Type ?? ea.RoutingKey;
            var correlationId = ea.BasicProperties.CorrelationId ?? "?";
            try
            {
                Log(correlationId, $"RECEIVED  {type}");
                await _handlers[type](ea.Body, correlationId);
                await _consumeChannel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Log(correlationId, $"FAILED    {type}: {ex.Message}");
                await _consumeChannel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
            }
        };
        await _consumeChannel.BasicConsumeAsync(queue, autoAck: false, consumer);
    }

    public void Log(string correlationId, string text) =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{_service}] [{correlationId[..Math.Min(8, correlationId.Length)]}] {text}");

    public async ValueTask DisposeAsync()
    {
        await _publishChannel.DisposeAsync();
        await _consumeChannel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

**Read it carefully** — it's a mini version of what MassTransit/NServiceBus give you: typed publish, handler registration, routing by message type, correlation propagation, ack/nack.

## Step 4 — Inventory service

**Inventory/Program.cs**

```csharp
using Shop.Messaging;

var stock = new Dictionary<string, int> { ["BOOK"] = 10, ["LAPTOP"] = 2 };
var reservations = new Dictionary<Guid, (string Sku, int Qty)>();

await using var bus = await RabbitBus.ConnectAsync("Inventory");

bus.On<OrderPlaced>(async (e, corr) =>
{
    if (reservations.ContainsKey(e.OrderId)) return;                       // idempotency: already handled

    if (stock.GetValueOrDefault(e.Sku) < e.Quantity)
    {
        await bus.PublishAsync(new StockReservationFailed(e.OrderId, $"Not enough {e.Sku}"), corr);
        return;
    }
    stock[e.Sku] -= e.Quantity;
    reservations[e.OrderId] = (e.Sku, e.Quantity);
    bus.Log(corr, $"Reserved {e.Quantity}x {e.Sku}. Stock left: {stock[e.Sku]}");
    await bus.PublishAsync(new StockReserved(e.OrderId, e.Sku, e.Quantity, e.Amount), corr);
});

bus.On<PaymentFailed>(async (e, corr) =>                                    // COMPENSATION
{
    if (!reservations.Remove(e.OrderId, out var r)) return;                // idempotency: already released
    stock[r.Sku] += r.Qty;
    bus.Log(corr, $"Released {r.Qty}x {r.Sku}. Stock now: {stock[r.Sku]}");
    await bus.PublishAsync(new StockReleased(e.OrderId), corr);
});

await bus.StartAsync();
Console.WriteLine("Inventory running. [enter] to exit");
Console.ReadLine();
```

## Step 5 — Payments service

**Payments/Program.cs**

```csharp
using Shop.Messaging;

var charged = new HashSet<Guid>();

await using var bus = await RabbitBus.ConnectAsync("Payments");

bus.On<StockReserved>(async (e, corr) =>
{
    if (charged.Contains(e.OrderId)) return;                               // idempotency

    await Task.Delay(500);                                                 // call the payment provider...
    if (e.Amount > 1000)
    {
        await bus.PublishAsync(new PaymentFailed(e.OrderId, e.Sku, e.Quantity, "Card declined"), corr);
        return;
    }
    charged.Add(e.OrderId);
    await bus.PublishAsync(new PaymentSucceeded(e.OrderId), corr);
});

await bus.StartAsync();
Console.WriteLine("Payments running. [enter] to exit");
Console.ReadLine();
```

Notice: "card declined" is a **business outcome** → an event, not an exception/DLQ ([concept 06](../concepts/06-Failures-Retries-DLQ.md)).

## Step 6 — Orders service

**Orders/Program.cs**

```csharp
using Shop.Messaging;

var orders = new Dictionary<Guid, string>();   // OrderId → status

await using var bus = await RabbitBus.ConnectAsync("Orders");

bus.On<PaymentSucceeded>((e, corr) => SetStatus(e.OrderId, "Confirmed ✔", corr));
bus.On<StockReservationFailed>((e, corr) => SetStatus(e.OrderId, $"Cancelled ✖ ({e.Reason})", corr));
bus.On<StockReleased>((e, corr) => SetStatus(e.OrderId, "Cancelled ✖ (payment failed, stock released)", corr));

await bus.StartAsync();

Console.WriteLine("Commands:  place <SKU> <qty> <amount>   |   list   |   exit");
while (Console.ReadLine() is { } line && line != "exit")
{
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts is ["place", var sku, var qty, var amount])
    {
        var id = Guid.NewGuid();
        orders[id] = "Pending";
        // In production: save order + OrderPlaced in an Outbox (Lab 03)!
        await bus.PublishAsync(new OrderPlaced(id, sku, int.Parse(qty), decimal.Parse(amount)), correlationId: id.ToString());
    }
    else if (parts is ["list"])
        foreach (var (id, status) in orders) Console.WriteLine($"  {id.ToString()[..8]}  {status}");
}

Task SetStatus(Guid orderId, string status, string corr)
{
    orders[orderId] = status;
    bus.Log(corr, $"Order {status}");
    return Task.CompletedTask;
}
```

## Step 7 — Run the three paths

Start **Inventory**, **Payments**, then **Orders** in three terminals (Orders last, it's the one you type into). Place the Orders terminal where you can see the other two.

| Command (in Orders) | Expected path |
|---------------------|---------------|
| `place BOOK 1 20` | OrderPlaced → StockReserved → PaymentSucceeded → **Confirmed** |
| `place LAPTOP 5 3000` | OrderPlaced → StockReservationFailed → **Cancelled** |
| `place LAPTOP 1 1500` | OrderPlaced → StockReserved → PaymentFailed → StockReleased → **Cancelled** (stock back to 2) |

Then `list`.

**Follow the correlation id:** every log line starts with the first 8 chars of the order id. Pick one order and trace it across the 3 terminals — that's what distributed tracing tools do for you automatically.

## Step 8 — 🔨 Break it

1. **Service down:** stop **Payments**, `place BOOK 1 20` → order stays **Pending**. Inventory already reserved stock. Start Payments → the `StockReserved` event was waiting in `payments.events` → order completes. *Temporal decoupling in action.*
2. **No isolation:** while Payments is down, check the stock in Inventory's logs — stock is reserved for an order that's not paid yet. That's the saga's missing "I". The reservation acts as a **semantic lock**.
3. **Duplicate event:** stop Payments, place `place BOOK 1 20`, and in the RabbitMQ UI peek the `StockReserved` message in `payments.events` (*Get messages*, requeue). Copy its payload, then on *Exchanges → shop.events → Publish message* send the same payload with routing key `StockReserved` and properties `type=StockReserved`, `correlation_id=<same id>`. Start Payments → it receives the event **twice**, and must **not** charge twice. Remove the `charged.Contains` check and try again — what happens?
4. **Restart = amnesia:** restart Inventory → the in-memory stock resets to 10/2 and reservations are lost. What would you need? (A database, plus Outbox/Inbox from Lab 03.)
5. **Stuck saga:** stop Payments forever. The order is Pending forever. Nothing in a pure choreography notices! → Step 9.

## Step 9 — Add a timeout (your turn)

Implement in **Orders**: every 5 seconds, any order **Pending** for more than 30 seconds is marked `Cancelled (timeout)` and an `OrderCancelled` event is published. Then make **Inventory** listen to `OrderCancelled` and release stock.

Questions to answer in your notes:
- What if `PaymentSucceeded` arrives **after** the timeout cancelled the order? (Hint: you now need a refund — another compensation. This is why orchestrators with explicit states become attractive.)
- Draw the state machine of an order: `Pending → Confirmed | Cancelled`, including the race above.

## Step 10 — Reflect: choreography vs orchestration

Answer:
1. Where is the complete workflow written down? (Nowhere in one place — it's spread across three services.)
2. How would you answer "what's the status of order X and why"?
3. Sketch how an **orchestrator** (`OrderSaga` state machine in Orders) would send **commands** (`ReserveStock`, `ChargePayment`, `ReleaseStock`) instead of reacting to events. What gets simpler, what gets coupled?

> Bonus: re-implement this lab with **MassTransit 8.x** (open source) and its saga state machine (`MassTransitStateMachine<OrderState>`). Compare the amount of code and what you get for free (retries, outbox, scheduling of timeouts).

---

## ✅ Checkpoint

1. Which step is the compensation in this saga, and why isn't it a "rollback"?
2. Why does `PaymentFailed` carry `Sku` and `Quantity`?
3. How did the CorrelationId help you debug?
4. What problems did you hit that a real system solves with Outbox, Inbox and persistent state?
5. When would you switch to orchestration?

## 🧹 Cleanup

Delete the `shop.events` exchange and `*.events` queues in the UI.

Next: [Lab 07 — Capstone](07-Capstone.md)
