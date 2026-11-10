# Lab 03 — Reliable Orders: Transactional Outbox + Inbox

**Goal:** build the "save to DB and publish an event" flow the **wrong** way, watch it lose events, then fix it with a **Transactional Outbox** on the producer and an **Inbox** (idempotent consumer) on the receiver.

**Concepts:** [08 Consistency: Outbox & Inbox](../concepts/08-Consistency-Outbox-Inbox.md) · [05 Idempotency](../concepts/05-Delivery-Guarantees-Idempotency.md)

**Time:** 3–4 hours. **Prerequisite:** Labs 01–02, RabbitMQ running.

---

## What you'll build

```
                 ┌─────────────── Orders.Api (SQLite: orders.db) ───────────────┐
POST /orders ──► │ BEGIN TX: INSERT Orders + INSERT Outbox  COMMIT               │
                 │ OutboxPublisher (BackgroundService): poll Outbox → publish     │
                 └──────────────────────────────┬────────────────────────────────┘
                                                │ OrderPlaced (MessageId = Outbox.Id)
                                       ( orders.events  fanout )
                                                │
                 ┌──────────────────────────────▼───────────── Billing.Worker (SQLite: billing.db) ┐
                 │ BEGIN TX: INSERT Inbox(MessageId) + INSERT Invoice  COMMIT → ACK                 │
                 └─────────────────────────────────────────────────────────────────────────────────┘
```

Two services, **two separate databases** (like real microservices).

## Step 1 — Create the solution

```powershell
mkdir C:\Labs\messaging\lab03-outbox; cd C:\Labs\messaging\lab03-outbox
dotnet new sln -n Lab03
dotnet new webapi -n Orders.Api
dotnet new worker -n Billing.Worker
dotnet sln add Orders.Api Billing.Worker

foreach ($p in "Orders.Api", "Billing.Worker") {
  dotnet add $p package RabbitMQ.Client
  dotnet add $p package Microsoft.EntityFrameworkCore.Sqlite
}
```

> We use **SQLite** to keep setup minimal. Everything works the same with SQL Server/Postgres (and in SQL Server you could later replace polling with CDC — see the old [Outbox notes](../Messaging-Outbox-Pattern.md)).

Delete the template's weather-forecast code from `Orders.Api/Program.cs`.

## Step 2 — Orders.Api: data model

**Orders.Api/Data.cs**

```csharp
using Microsoft.EntityFrameworkCore;

public class Order
{
    public Guid Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Placed";
    public DateTime CreatedAt { get; set; }
}

public class OutboxMessage
{
    public Guid Id { get; set; }                 // becomes the MessageId
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }   // null = not yet published
}

public class OrdersDb(DbContextOptions<OrdersDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
}

public record PlaceOrderRequest(int CustomerId, decimal Total);
public record OrderPlaced(Guid OrderId, int CustomerId, decimal Total, DateTime PlacedAt);
```

## Step 3 — The WRONG way first: dual write

**Orders.Api/Program.cs**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<OrdersDb>(o => o.UseSqlite("Data Source=orders.db"));
var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<OrdersDb>().Database.EnsureCreated();

var factory = new ConnectionFactory { HostName = "localhost" };
var connection = await factory.CreateConnectionAsync();
var channel = await connection.CreateChannelAsync();
await channel.ExchangeDeclareAsync("orders.events", ExchangeType.Fanout, durable: true);

app.MapPost("/orders/dual-write", async (PlaceOrderRequest req, OrdersDb db) =>
{
    var order = new Order { Id = Guid.NewGuid(), CustomerId = req.CustomerId, Total = req.Total, CreatedAt = DateTime.UtcNow };
    db.Orders.Add(order);
    await db.SaveChangesAsync();                                   // write #1: DB

    if (req.Total == 666) throw new Exception("💥 crashed after DB commit");

    var evt = new OrderPlaced(order.Id, order.CustomerId, order.Total, order.CreatedAt);
    var props = new BasicProperties { MessageId = Guid.NewGuid().ToString(), Type = "OrderPlaced.v1", Persistent = true };
    await channel.BasicPublishAsync("orders.events", "", false, props, JsonSerializer.SerializeToUtf8Bytes(evt)); // write #2: broker

    return Results.Accepted($"/orders/{order.Id}", new { order.Id });
});

app.MapGet("/orders", async (OrdersDb db) => await db.Orders.ToListAsync());

app.Run();
```

Run it (`dotnet run --project Orders.Api`) and note the URL it prints (`Now listening on: http://localhost:5xxx`) — replace `5000` below with your port. In PowerShell use `curl.exe` (plain `curl` is an alias in Windows PowerShell):

```powershell
curl.exe -X POST http://localhost:5000/orders/dual-write -H "Content-Type: application/json" -d '{\"customerId\":1,\"total\":50}'
curl.exe -X POST http://localhost:5000/orders/dual-write -H "Content-Type: application/json" -d '{\"customerId\":1,\"total\":666}'
```

(Or create an `orders.http` file in VS Code / Visual Studio — easier to repeat.)

Bind a temporary queue to see the events: in the RabbitMQ UI create queue `debug.orders`, bind it to `orders.events`.

**What you see:**
- The 50 order: row in DB ✔, event in `debug.orders` ✔.
- The 666 order: row in DB ✔ (`GET /orders`), **no event**. Billing will never know this order exists. **Lost event.**

🔨 **Break it 2:** `docker stop rabbitmq`, place an order → DB commit succeeds, publish throws. Same problem. `docker start rabbitmq` afterwards.

Write in your notes: *why can't I just wrap both in a transaction?*

## Step 4 — The RIGHT way: Transactional Outbox

Add a new endpoint that writes the order **and** the event into the **same DB transaction**:

```csharp
app.MapPost("/orders", async (PlaceOrderRequest req, OrdersDb db) =>
{
    var order = new Order { Id = Guid.NewGuid(), CustomerId = req.CustomerId, Total = req.Total, CreatedAt = DateTime.UtcNow };
    var evt = new OrderPlaced(order.Id, order.CustomerId, order.Total, order.CreatedAt);

    db.Orders.Add(order);
    db.Outbox.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = "OrderPlaced.v1",
        Payload = JsonSerializer.Serialize(evt),
        OccurredAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();          // ONE local transaction: both rows or none

    if (req.Total == 666) throw new Exception("💥 crashed after DB commit");   // no longer a problem!

    return Results.Accepted($"/orders/{order.Id}", new { order.Id });
});
```

> `SaveChangesAsync` wraps all pending inserts in a single transaction automatically.

Delete `orders.db` (schema changed) and restart. Place orders → `Outbox` rows appear with `ProcessedAt = NULL`. Inspect:

```powershell
sqlite3 orders.db "SELECT Id, Type, ProcessedAt FROM Outbox;"
```

> The `.db` files are created in the folder you run `dotnet run` from — here, the solution folder.

## Step 5 — The Outbox publisher (relay)

**Orders.Api/OutboxPublisher.cs**

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public class OutboxPublisher(IServiceScopeFactory scopes, ILogger<OutboxPublisher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync(ct);

        // Publisher confirms: BasicPublishAsync completes only when the broker has the message.
        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                                      publisherConfirmationTrackingEnabled: true);
        await using var channel = await connection.CreateChannelAsync(channelOptions, ct);
        await channel.ExchangeDeclareAsync("orders.events", ExchangeType.Fanout, durable: true, cancellationToken: ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrdersDb>();

                var batch = await db.Outbox
                    .Where(m => m.ProcessedAt == null)
                    .OrderBy(m => m.OccurredAt)
                    .Take(20)
                    .ToListAsync(ct);

                foreach (var msg in batch)
                {
                    var props = new BasicProperties
                    {
                        MessageId = msg.Id.ToString(),    // stable id → consumers can dedup
                        Type = msg.Type,
                        ContentType = "application/json",
                        Persistent = true
                    };
                    await channel.BasicPublishAsync("orders.events", "", false, props,
                                                    Encoding.UTF8.GetBytes(msg.Payload), ct);

                    if (Environment.GetEnvironmentVariable("CRASH_AFTER_PUBLISH") == "1")
                        Environment.Exit(1);              // 🔨 simulate crash before marking as sent

                    msg.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    log.LogInformation("Published {Type} {Id}", msg.Type, msg.Id);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Outbox publish failed, will retry");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddHostedService<OutboxPublisher>();
```

**Run & observe:**
1. Place a few orders → logs show `Published OrderPlaced…` within ~1s; `debug.orders` receives them; `ProcessedAt` gets filled.
2. Place the `666` order → the HTTP call fails **but** the order and its outbox row were committed → the event is **still published**. ✔
3. 🔨 `docker stop rabbitmq` → place 3 orders → publisher logs warnings, rows stay unprocessed. `docker start rabbitmq` → all 3 published. **No lost events.** ✔

> Note: if the RabbitMQ connection drops, this simple publisher may need a restart — the client has automatic recovery, but handling it well is an exercise for you. Real outbox libraries handle reconnects, batching, and locking for multiple instances.

## Step 6 — Billing.Worker with an Inbox

**Billing.Worker/Data.cs**

```csharp
using Microsoft.EntityFrameworkCore;

public class InboxMessage
{
    public string MessageId { get; set; } = "";   // primary key → unique
    public DateTime ProcessedAt { get; set; }
}

public class Invoice
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BillingDb(DbContextOptions<BillingDb> options) : DbContext(options)
{
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.Entity<InboxMessage>().HasKey(x => x.MessageId);
}

// Billing's own view of the event (tolerant reader: only the fields it needs)
public record OrderPlaced(Guid OrderId, decimal Total);
```

**Billing.Worker/Worker.cs** (replace the template)

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Worker(IServiceScopeFactory scopes, ILogger<Worker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        var connection = await factory.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync("orders.events", ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync("billing.order-placed", durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync("billing.order-placed", "orders.events", "", cancellationToken: ct);
        await channel.BasicQosAsync(0, 10, false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var messageId = ea.BasicProperties.MessageId ?? throw new InvalidOperationException("MessageId required");
            var evt = JsonSerializer.Deserialize<OrderPlaced>(ea.Body.Span)!;

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDb>();

            if (await db.Inbox.AnyAsync(x => x.MessageId == messageId))
            {
                log.LogInformation("Duplicate {MessageId} → skip", messageId);
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            db.Inbox.Add(new InboxMessage { MessageId = messageId, ProcessedAt = DateTime.UtcNow });
            db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), OrderId = evt.OrderId, Amount = evt.Total, CreatedAt = DateTime.UtcNow });

            try
            {
                await db.SaveChangesAsync();      // Inbox row + Invoice in ONE transaction
                log.LogInformation("Invoice created for order {OrderId}", evt.OrderId);
            }
            catch (DbUpdateException)
            {
                // A concurrent duplicate won the race (PK violation on Inbox) — safe to ignore
                log.LogInformation("Concurrent duplicate {MessageId} → skip", messageId);
            }

            if (Environment.GetEnvironmentVariable("CRASH_BEFORE_ACK") == "1")
                Environment.Exit(1);              // 🔨 simulate crash after commit, before ack

            await channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await channel.BasicConsumeAsync("billing.order-placed", autoAck: false, consumer, ct);
        await Task.Delay(Timeout.Infinite, ct);
    }
}
```

**Billing.Worker/Program.cs**

```csharp
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<BillingDb>(o => o.UseSqlite("Data Source=billing.db"));
builder.Services.AddHostedService<Worker>();
var host = builder.Build();

using (var scope = host.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<BillingDb>().Database.EnsureCreated();

host.Run();
```

Run both apps, place orders → one invoice per order.

## Step 7 — 🔨 Prove the whole pipeline is effectively-once

### Experiment A — Duplicate from the producer side

1. Stop Orders.Api. Start it with the crash flag:
   ```powershell
   $env:CRASH_AFTER_PUBLISH = "1"; dotnet run --project Orders.Api
   ```
2. Place one order → the publisher publishes, then the process dies **before** setting `ProcessedAt`.
3. Restart normally (`Remove-Item Env:CRASH_AFTER_PUBLISH`; run again) → the same outbox row is published **again** (same MessageId).
4. Billing logs `Duplicate … → skip`. Count invoices:
   ```powershell
   sqlite3 billing.db "SELECT OrderId, COUNT(*) FROM Invoices GROUP BY OrderId;"
   ```
   → exactly **1** per order. ✔

### Experiment B — Duplicate from the consumer side

1. Run Billing with `$env:CRASH_BEFORE_ACK = "1"`.
2. Place an order → invoice committed, then crash before ack → RabbitMQ redelivers.
3. Restart Billing normally → `Duplicate … → skip`. Still 1 invoice. ✔

### Experiment C — Remove the protection

Comment out the Inbox check and the Inbox insert, repeat Experiment B → **2 invoices** for one order. Put it back.

### Experiment D — Why the same transaction matters

Change Billing to call `SaveChangesAsync()` **twice**: first after adding the invoice, then after adding the inbox row. Add a crash between them. What happens on redelivery? Write the answer in your notes, then revert.

## Step 8 — Housekeeping (think, then implement)

- Outbox and Inbox tables grow forever. Add a cleanup job: delete processed outbox rows older than 7 days, inbox rows older than 30 days. Why must inbox retention be longer than the maximum redelivery window?
- Run **two instances** of Orders.Api. Both publishers read the same unprocessed rows → duplicates (harmless thanks to the inbox, but wasteful). How would you prevent it? (Ideas: `SELECT … FOR UPDATE SKIP LOCKED` / `UPDLOCK, READPAST` in Postgres/SQL Server, a leader lock, or a single relay instance.)

---

## ✅ Checkpoint

1. Explain the dual-write problem with the `666` example.
2. Why does the outbox guarantee no lost events, but **not** no duplicates?
3. Why is `Outbox.Id` used as `MessageId`?
4. What breaks if the Inbox insert and the business insert are in different transactions?
5. Draw the full flow from HTTP request to invoice, marking every place a crash could happen and what protects it.

## 🧹 Cleanup

Delete `orders.db`, `billing.db`, and the `debug.orders` queue.

Next: [Lab 04 — Kafka](04-Kafka-Streaming.md)
