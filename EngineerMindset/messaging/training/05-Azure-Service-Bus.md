# Lab 05 — Azure Service Bus

**Goal:** use a managed enterprise broker and its built-in features: competing consumers, max delivery count & DLQ, explicit dead-lettering, lock duration, scheduled messages, sessions (ordering), topics with SQL filters, and duplicate detection.

**Concepts:** [06 Failures & DLQ](../concepts/06-Failures-Retries-DLQ.md) · [07 Ordering (sessions)](../concepts/07-Ordering-Partitioning-Concurrency.md) · [14 Choosing a Broker](../concepts/14-Choosing-a-Broker.md)

**Time:** 2–3 hours.

---

## Entities you need

| Entity | Settings |
|--------|----------|
| Queue `payments` | Max delivery count **3**, lock duration **30s** |
| Queue `orders-sessions` | **Requires session** |
| Queue `dedup` | **Duplicate detection** on, window 1 minute |
| Topic `order-events` | — |
| Subscription `order-events/all-orders` | No filter (gets everything) |
| Subscription `order-events/eu-orders` | SQL filter `region = 'EU'` |

Pick **one** of the two options below to create them.

---

## Step 1, Option A — Local emulator (free, Docker)

Microsoft provides an official **Service Bus emulator** that runs in Docker (it uses a SQL container for storage).

1. Clone the installer repo (outside this repo):
   ```powershell
   cd C:\Labs\messaging
   git clone https://github.com/Azure/azure-service-bus-emulator-installer.git
   cd azure-service-bus-emulator-installer\ServiceBus-Emulator\Docker-Compose-Template
   ```
2. Read its `README` — it explains the `.env` values (accept EULA, SQL password, config path).
3. Edit `Config.json`. Keep the namespace name the sample uses (`sbemulatorns`) and replace the entities with ours. The shape is roughly:

   ```json
   {
     "UserConfig": {
       "Namespaces": [
         {
           "Name": "sbemulatorns",
           "Queues": [
             { "Name": "payments",        "Properties": { "MaxDeliveryCount": 3, "LockDuration": "PT30S", "RequiresSession": false } },
             { "Name": "orders-sessions", "Properties": { "MaxDeliveryCount": 3, "LockDuration": "PT30S", "RequiresSession": true } },
             { "Name": "dedup",           "Properties": { "RequiresDuplicateDetection": true, "DuplicateDetectionHistoryTimeWindow": "PT1M" } }
           ],
           "Topics": [
             {
               "Name": "order-events",
               "Properties": {},
               "Subscriptions": [
                 { "Name": "all-orders", "Properties": { "MaxDeliveryCount": 3 } },
                 {
                   "Name": "eu-orders",
                   "Properties": { "MaxDeliveryCount": 3 },
                   "Rules": [
                     { "Name": "eu-only", "Properties": { "FilterType": "Sql", "SqlFilter": { "SqlExpression": "region = 'EU'" } } }
                   ]
                 }
               ]
             }
           ]
         }
       ],
       "Logging": { "Type": "File" }
     }
   }
   ```

   > ⚠️ Compare with the sample `Config.json` in the repo — copy any **required** properties it lists for queues/topics/subscriptions (the emulator is strict about the schema). Getting this file right is part of the lab.

4. Start it with the compose file described in the README, e.g.:
   ```powershell
   docker compose -f docker-compose-default.yml up -d
   docker compose -f docker-compose-default.yml logs -f    # wait until it reports it's ready
   ```
5. Connection string for the emulator:
   ```
   Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
   ```

The emulator has some limitations (no portal, fixed quotas, some features missing). If something in this lab doesn't behave as described, check its docs or use Option B.

## Step 1, Option B — Real Azure (≈ cents for a few hours, **delete afterwards**)

Topics need the **Standard** tier.

```powershell
az login
$rg = "rg-messaging-lab"; $ns = "sb-lab-$(Get-Random -Maximum 99999)"; $loc = "westeurope"

az group create -n $rg -l $loc
az servicebus namespace create -g $rg -n $ns -l $loc --sku Standard

az servicebus queue create -g $rg --namespace-name $ns -n payments --max-delivery-count 3 --lock-duration PT30S
az servicebus queue create -g $rg --namespace-name $ns -n orders-sessions --enable-session true
az servicebus queue create -g $rg --namespace-name $ns -n dedup --enable-duplicate-detection true --duplicate-detection-history-time-window PT1M

az servicebus topic create -g $rg --namespace-name $ns -n order-events
az servicebus topic subscription create -g $rg --namespace-name $ns --topic-name order-events -n all-orders
az servicebus topic subscription create -g $rg --namespace-name $ns --topic-name order-events -n eu-orders
az servicebus topic subscription rule create -g $rg --namespace-name $ns --topic-name order-events `
  --subscription-name eu-orders -n eu-only --filter-sql-expression "region = 'EU'"
az servicebus topic subscription rule delete -g $rg --namespace-name $ns --topic-name order-events `
  --subscription-name eu-orders -n '$Default'

az servicebus namespace authorization-rule keys list -g $rg --namespace-name $ns `
  -n RootManageSharedAccessKey --query primaryConnectionString -o tsv
```

> Why delete `$Default`? Every new subscription gets a `$Default` rule that matches **everything** (`1=1`). If you only *add* your filter, the rules are OR-ed and `eu-orders` still receives every message. Classic gotcha.

Explore the namespace in the **Azure Portal** — the **Service Bus Explorer** blade lets you peek, send and receive messages, including from the DLQ.

> In production, use **Managed Identity** (`new ServiceBusClient("<ns>.servicebus.windows.net", new DefaultAzureCredential())`) instead of connection strings. For this lab a connection string is fine.

---

## Step 2 — Create the project

```powershell
mkdir C:\Labs\messaging\lab05-servicebus; cd C:\Labs\messaging\lab05-servicebus
dotnet new console -n SbLab
cd SbLab
dotnet add package Azure.Messaging.ServiceBus

$env:SB_CONNECTION = "<your connection string>"
```

We'll build **one** console app with a "mode" argument: `dotnet run -- <mode> [args]`.

**Program.cs (skeleton)**

```csharp
using Azure.Messaging.ServiceBus;

var connectionString = Environment.GetEnvironmentVariable("SB_CONNECTION")
    ?? throw new InvalidOperationException("Set SB_CONNECTION");
await using var client = new ServiceBusClient(connectionString);

switch (args.ElementAtOrDefault(0))
{
    case "send":        await Send(client, args); break;
    case "receive":     await Receive(client); break;
    case "dlq":         await ReadDeadLetters(client); break;
    case "schedule":    await Schedule(client); break;
    case "session-send":    await SessionSend(client); break;
    case "session-receive": await SessionReceive(client); break;
    case "topic-send":      await TopicSend(client, args); break;
    case "topic-receive":   await TopicReceive(client, args[1]); break;
    case "dedup":       await Dedup(client); break;
    default: Console.WriteLine("modes: send | receive | dlq | schedule | session-send | session-receive | topic-send | topic-receive <sub> | dedup"); break;
}

record Payment(decimal Amount);
```

Add each method below as a `static async Task` local function **above** the `record` line, step by step.

## Step 3 — Send & receive with manual completion

```csharp
static async Task Send(ServiceBusClient client, string[] args)
{
    await using var sender = client.CreateSender("payments");
    var amount = decimal.Parse(args.ElementAtOrDefault(1) ?? "10");

    var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(new Payment(amount)))
    {
        MessageId = Guid.NewGuid().ToString(),
        Subject = "ChargePayment",
        ContentType = "application/json",
        CorrelationId = "order-" + Random.Shared.Next(1000, 9999)
    };
    await sender.SendMessageAsync(message);
    Console.WriteLine($"Sent {amount} ({message.MessageId})");
}

static async Task Receive(ServiceBusClient client)
{
    await using var processor = client.CreateProcessor("payments", new ServiceBusProcessorOptions
    {
        AutoCompleteMessages = false,   // we decide: complete / abandon / dead-letter
        MaxConcurrentCalls = 1
    });

    processor.ProcessMessageAsync += async args =>
    {
        var m = args.Message;
        var payment = m.Body.ToObjectFromJson<Payment>()!;
        Console.WriteLine($"→ {m.MessageId} amount={payment.Amount} deliveryCount={m.DeliveryCount}");

        if (payment.Amount < 0)
        {
            await args.DeadLetterMessageAsync(m, "InvalidAmount", "Amount must be positive");   // poison → DLQ now
            Console.WriteLine("  dead-lettered");
            return;
        }
        if (payment.Amount == 13)
            throw new TimeoutException("flaky payment API");     // handler throws → message abandoned → retried

        if (payment.Amount == 60)
            await Task.Delay(TimeSpan.FromSeconds(40));          // longer than the 30s lock

        await args.CompleteMessageAsync(m);
        Console.WriteLine("  completed");
    };

    processor.ProcessErrorAsync += args =>
    {
        Console.WriteLine($"  ERROR ({args.ErrorSource}): {args.Exception.Message}");
        return Task.CompletedTask;
    };

    await processor.StartProcessingAsync();
    Console.WriteLine("Receiving. Press [enter] to stop.");
    Console.ReadLine();
    await processor.StopProcessingAsync();
}
```

**Run:**
1. `dotnet run -- send 10` (a few times, receiver stopped) → see the messages in the portal / explorer.
2. `dotnet run -- receive` → completed.
3. Run **two** receivers and send 10 messages → **competing consumers**.

## Step 4 — 🔨 Max delivery count → DLQ

1. With the receiver running: `dotnet run -- send 13`
   → `deliveryCount=1, 2, 3` → then it disappears. Service Bus moved it to the **DLQ** automatically (`MaxDeliveryCount = 3`).
2. `dotnet run -- send -5` → dead-lettered immediately by our code.

Now read the DLQ:

```csharp
static async Task ReadDeadLetters(ServiceBusClient client)
{
    await using var receiver = client.CreateReceiver("payments",
        new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
    await using var sender = client.CreateSender("payments");

    var messages = await receiver.ReceiveMessagesAsync(maxMessages: 20, maxWaitTime: TimeSpan.FromSeconds(5));
    foreach (var m in messages)
    {
        Console.WriteLine($"{m.MessageId} reason={m.DeadLetterReason} desc={m.DeadLetterErrorDescription} body={m.Body}");
        Console.Write("  resubmit? (y/n) ");
        if (Console.ReadLine() == "y")
            await sender.SendMessageAsync(new ServiceBusMessage(m));   // copy back to the main queue
        await receiver.CompleteMessageAsync(m);                       // remove from DLQ
    }
}
```

`dotnet run -- dlq` → notice the two different reasons: `MaxDeliveryCountExceeded` vs your `InvalidAmount`.

## Step 5 — 🔨 Lock expiry → duplicate

`dotnet run -- send 60` → the handler takes 40s, but the lock is 30s.

The SDK processor **auto-renews locks** by default (`MaxAutoLockRenewalDuration` = 5 minutes), so this actually works. Now disable it:

```csharp
MaxAutoLockRenewalDuration = TimeSpan.Zero
```

Repeat → `CompleteMessageAsync` fails with **MessageLockLost**, and the message is delivered **again** (deliveryCount=2). This is the classic source of duplicates described in [concept 05](../concepts/05-Delivery-Guarantees-Idempotency.md). Restore auto-renewal afterwards.

## Step 6 — Scheduled messages

```csharp
static async Task Schedule(ServiceBusClient client)
{
    await using var sender = client.CreateSender("payments");
    var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(new Payment(42))) { MessageId = Guid.NewGuid().ToString() };
    var seq = await sender.ScheduleMessageAsync(message, DateTimeOffset.UtcNow.AddSeconds(20));
    Console.WriteLine($"Scheduled (sequence {seq}) for +20s");
}
```

Run with the receiver open → the message appears ~20s later. Use cases: **timeouts in sagas** ("if not paid in 15 min, cancel"), reminders, delayed retries. You can cancel it with `sender.CancelScheduledMessageAsync(seq)`.

## Step 7 — Sessions: ordered processing per key

```csharp
static async Task SessionSend(ServiceBusClient client)
{
    await using var sender = client.CreateSender("orders-sessions");
    foreach (var step in new[] { "Created", "Paid", "Shipped" })
    foreach (var order in new[] { "order-1", "order-2", "order-3" })
        await sender.SendMessageAsync(new ServiceBusMessage($"{order}:{step}") { SessionId = order });
    Console.WriteLine("Sent 9 messages in 3 sessions");
}

static async Task SessionReceive(ServiceBusClient client)
{
    await using var processor = client.CreateSessionProcessor("orders-sessions", new ServiceBusSessionProcessorOptions
    {
        AutoCompleteMessages = false,
        MaxConcurrentSessions = 2,          // 2 orders in parallel
        MaxConcurrentCallsPerSession = 1    // but strictly one-at-a-time inside an order
    });

    processor.ProcessMessageAsync += async args =>
    {
        Console.WriteLine($"[session {args.SessionId}] {args.Message.Body}");
        await Task.Delay(500);
        await args.CompleteMessageAsync(args.Message);
    };
    processor.ProcessErrorAsync += args => { Console.WriteLine(args.Exception.Message); return Task.CompletedTask; };

    await processor.StartProcessingAsync();
    Console.ReadLine();
    await processor.StopProcessingAsync();
}
```

**Run:** `session-send`, then **two** `session-receive` instances.

**What you see:** each order's messages are always Created → Paid → Shipped, a session is owned by **one** receiver at a time, and different orders are processed in parallel.

🔨 Try sending to `orders-sessions` **without** `SessionId` → error. And try receiving from it with a normal (non-session) processor → error. Sessions are an all-or-nothing queue setting.

## Step 8 — Topics & subscription filters

```csharp
static async Task TopicSend(ServiceBusClient client, string[] args)
{
    await using var sender = client.CreateSender("order-events");
    var region = args.ElementAtOrDefault(1) ?? "EU";
    var message = new ServiceBusMessage($"OrderPlaced in {region}") { Subject = "OrderPlaced" };
    message.ApplicationProperties["region"] = region;        // filters run on properties, not the body
    await sender.SendMessageAsync(message);
    Console.WriteLine($"Published OrderPlaced region={region}");
}

static async Task TopicReceive(ServiceBusClient client, string subscription)
{
    await using var processor = client.CreateProcessor("order-events", subscription);
    processor.ProcessMessageAsync += args =>
    {
        Console.WriteLine($"[{subscription}] {args.Message.Body}");
        return Task.CompletedTask;                          // auto-complete is on by default here
    };
    processor.ProcessErrorAsync += args => { Console.WriteLine(args.Exception.Message); return Task.CompletedTask; };
    await processor.StartProcessingAsync();
    Console.ReadLine();
}
```

**Run:** two receivers (`topic-receive all-orders`, `topic-receive eu-orders`), then `topic-send EU`, `topic-send US`.

| Sent | all-orders | eu-orders |
|------|-----------|-----------|
| EU | ✔ | ✔ |
| US | ✔ | ✖ |

## Step 9 — Duplicate detection

```csharp
static async Task Dedup(ServiceBusClient client)
{
    await using var sender = client.CreateSender("dedup");
    await using var receiver = client.CreateReceiver("dedup");

    for (int i = 0; i < 3; i++)
        await sender.SendMessageAsync(new ServiceBusMessage("charge 25") { MessageId = "pay-001" });

    var received = await receiver.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(5));
    Console.WriteLine($"Sent 3 with the same MessageId, received {received.Count}");
    foreach (var m in received) await receiver.CompleteMessageAsync(m);
}
```

**What you see:** `received 1`. The broker dropped the duplicates within the detection window.

**Think:** does this make the *consumer* idempotent? (No — it protects against **producer** re-sends only, within a time window. Lock-expiry redeliveries from Step 5 still happen.)

---

## ✅ Checkpoint

1. What three ways did a message end up in the DLQ? Which reason does each produce?
2. Why did the 40-second handler work at first, and what broke when you disabled lock renewal?
3. How do sessions give ordering *and* parallelism?
4. Why must you delete the `$Default` rule on a filtered subscription?
5. What does duplicate detection protect against, and what doesn't it?
6. Map each Service Bus feature you used to its RabbitMQ / Kafka equivalent (see [concept 02](../concepts/02-Building-Blocks.md)).

## 🧹 Cleanup

- Emulator: `docker compose -f docker-compose-default.yml down`
- Azure: **`az group delete -n rg-messaging-lab --yes`** — don't forget, Standard tier is billed hourly.

Next: [Lab 06 — Choreography Saga](06-Saga-Choreography.md)
