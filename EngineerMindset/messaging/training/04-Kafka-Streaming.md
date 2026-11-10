# Lab 04 — Kafka: Partitions, Consumer Groups, Offsets & Replay

**Goal:** feel the difference between a **queue** and a **log**. See per-key ordering, partition assignment, rebalancing, consumer lag, and replaying history.

**Concepts:** [04 Queue vs Log](../concepts/04-Communication-Models.md) · [07 Ordering & Partitioning](../concepts/07-Ordering-Partitioning-Concurrency.md) · [11 Scaling](../concepts/11-Scaling-Backpressure-Performance.md) · [13 EDA & Streaming](../concepts/13-Event-Driven-Architecture.md)

**Time:** 2–3 hours.

---

## Step 1 — Start Kafka (single node, KRaft mode, no ZooKeeper)

```powershell
docker run -d --name kafka -p 9092:9092 apache/kafka:4.0.0
docker logs kafka --tail 20     # wait for "Kafka Server started"
```

Make a helper so the CLI commands are shorter (PowerShell function, valid for this terminal):

```powershell
function k { $script, $rest = $args; docker exec -it kafka "/opt/kafka/bin/$script" @rest }
```

Usage: `k kafka-topics.sh --bootstrap-server localhost:9092 --list`

## Step 2 — Create a topic with 3 partitions

```powershell
k kafka-topics.sh --bootstrap-server localhost:9092 --create --topic orders --partitions 3 --replication-factor 1
k kafka-topics.sh --bootstrap-server localhost:9092 --describe --topic orders
```

**What you see:** 3 partitions, each with a leader (broker 1). In a real cluster you'd use replication factor 3.

## Step 3 — Create the solution

```powershell
mkdir C:\Labs\messaging\lab04-kafka; cd C:\Labs\messaging\lab04-kafka
dotnet new sln -n Lab04
dotnet new console -n Producer
dotnet new console -n Consumer
dotnet sln add Producer Consumer
dotnet add Producer package Confluent.Kafka
dotnet add Consumer package Confluent.Kafka
```

## Step 4 — Producer: keyed events

**Producer/Program.cs**

```csharp
using Confluent.Kafka;

var config = new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    Acks = Acks.All,              // wait for all in-sync replicas
    EnableIdempotence = true      // no duplicates from producer retries, order kept per partition
};

using var producer = new ProducerBuilder<string, string>(config).Build();

string[] orders = ["order-1", "order-2", "order-3", "order-4", "order-5"];
string[] steps  = ["Created", "Paid", "Shipped"];

foreach (var step in steps)
foreach (var orderId in orders)
{
    var result = await producer.ProduceAsync("orders",
        new Message<string, string> { Key = orderId, Value = $"{orderId}:{step}" });

    Console.WriteLine($"{result.Message.Value,-18} → partition {result.Partition.Value}, offset {result.Offset.Value}");
}
```

Run it: `dotnet run --project Producer`

**What you see:** every event of `order-1` lands in the **same partition**; different orders spread across partitions. Offsets grow per partition independently.

**Read the raw log** with the console consumer:

```powershell
k kafka-console-consumer.sh --bootstrap-server localhost:9092 --topic orders --from-beginning `
  --property print.key=true --property print.partition=true --timeout-ms 5000
```

Notice: **across** partitions the output is interleaved (no global order), but **within** a key the order is always Created → Paid → Shipped.

Run the producer again → messages are **appended**; the old ones are still there. A log doesn't delete on read.

## Step 5 — Consumer with manual commits

**Consumer/Program.cs**

```csharp
using Confluent.Kafka;

var groupId = args.ElementAtOrDefault(0) ?? "billing";
var name    = args.ElementAtOrDefault(1) ?? $"c{Environment.ProcessId}";

var config = new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = groupId,
    AutoOffsetReset = AutoOffsetReset.Earliest,   // where to start if the group has no committed offset
    EnableAutoCommit = false                      // we commit after processing
};

using var consumer = new ConsumerBuilder<string, string>(config)
    .SetPartitionsAssignedHandler((_, parts) =>
        Console.WriteLine($"[{name}] ASSIGNED  {string.Join(",", parts.Select(p => p.Partition.Value))}"))
    .SetPartitionsRevokedHandler((_, parts) =>
        Console.WriteLine($"[{name}] REVOKED   {string.Join(",", parts.Select(p => p.Partition.Value))}"))
    .Build();

consumer.Subscribe("orders");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    while (true)
    {
        var cr = consumer.Consume(cts.Token);
        Console.WriteLine($"[{name}] p{cr.Partition.Value}@{cr.Offset.Value}  {cr.Message.Value}");

        Thread.Sleep(300);                                        // simulate work

        if (cr.Message.Value.EndsWith("Paid") && Environment.GetEnvironmentVariable("CRASH_ON_PAID") == "1")
            Environment.Exit(1);                                  // 🔨 crash before commit

        consumer.Commit(cr);                                      // "processed up to here"
    }
}
catch (OperationCanceledException) { }
finally
{
    consumer.Close();     // leave the group cleanly → fast rebalance
}
```

Run one consumer: `dotnet run --project Consumer -- billing A`

**What you see:** `ASSIGNED 0,1,2` (one consumer owns all partitions), then all historical messages (because `Earliest`).

## Step 6 — Scale the consumer group

1. Keep consumer **A** running. In a new terminal start **B**: `dotnet run --project Consumer -- billing B`
   → Watch both: **REVOKED** / **ASSIGNED** — a **rebalance**. Now A has e.g. `0,1` and B has `2`.
2. Start **C** → each owns one partition.
3. Start **D** → D gets **nothing**. Max parallelism = number of partitions.
4. Run the producer while all four run → D stays idle.
5. Stop **A** (`Ctrl+C`) → another rebalance, D finally gets work.

> Interview link: "Consumers in a group ≤ partitions; extra consumers are idle standbys."

## Step 7 — Pub/sub with consumer groups

Start a consumer in a **different group**:

```powershell
dotnet run --project Consumer -- analytics X
```

**What you see:** `analytics` reads **all** messages from the beginning, independently of `billing`. Each group has its own offsets → that's how Kafka does pub/sub. Same group = queue semantics (share work); different groups = pub/sub (each gets everything).

## Step 8 — Offsets & consumer lag

Describe the group:

```powershell
k kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group billing
```

Columns to understand: `CURRENT-OFFSET` (committed), `LOG-END-OFFSET` (latest), `LAG` (= difference), `CONSUMER-ID`.

Create lag on purpose: stop all `billing` consumers, run the producer 5 times, describe again → LAG > 0. Start one consumer and watch LAG drain.

## Step 9 — 🔨 Crash before commit → reprocessing

```powershell
$env:CRASH_ON_PAID = "1"; dotnet run --project Consumer -- crashers A
```

It processes `Created` events, hits the first `Paid`, dies **before committing**. Run again without the flag:

```powershell
Remove-Item Env:CRASH_ON_PAID; dotnet run --project Consumer -- crashers A
```

→ It **re-reads** from the last committed offset; the `Paid` event is processed again. That's at-least-once on Kafka → consumers must be idempotent (same story as Lab 03).

**Think:** what would happen if you committed **before** processing? (At-most-once.) And with `EnableAutoCommit = true`? (Commits happen on a timer in the background — you can lose *or* duplicate depending on timing.)

## Step 10 — Replay history

The superpower of a log. Two ways:

**A. New consumer group** — just pick a new group id: `dotnet run --project Consumer -- billing-v2 R` → reads everything from the start (because `Earliest`). This is how a *new* service bootstraps its state from history.

**B. Reset offsets of an existing group** (the group must have no running consumers):

```powershell
k kafka-consumer-groups.sh --bootstrap-server localhost:9092 --group billing --topic orders --reset-offsets --to-earliest --dry-run
k kafka-consumer-groups.sh --bootstrap-server localhost:9092 --group billing --topic orders --reset-offsets --to-earliest --execute
```

Other options: `--to-datetime 2026-10-28T10:00:00.000`, `--shift-by -5`, `--to-offset 3`. Real-world use: "we deployed a bug at 10:00, fix it, then reprocess everything since 10:00".

🔨 Change `AutoOffsetReset` to `Latest`, use a brand-new group → it sees **nothing** until you produce new messages. Know this default — it surprises many people.

## Step 11 — Bonus experiments

- **Hot partition:** change the producer key to a constant (`"same-key"`) and send 100 messages → all go to one partition; only one consumer in the group works.
- **Retention:** `k kafka-configs.sh --bootstrap-server localhost:9092 --alter --entity-type topics --entity-name orders --add-config retention.ms=60000` → old segments get deleted after ~1 minute (deletion happens per segment, so it may take longer). Kafka retention is by **time/size**, not by "was it consumed".
- **More partitions:** `k kafka-topics.sh --bootstrap-server localhost:9092 --alter --topic orders --partitions 6`, then produce again → notice some keys now land in a **different partition** than before. Why is that dangerous for ordering?
- **Throughput:** produce 100,000 messages without awaiting each one (`producer.Produce(...)` with a delivery handler, then `producer.Flush()`); compare the time with the awaited version. Then try `LingerMs = 20` and `CompressionType = CompressionType.Lz4`.

---

## ✅ Checkpoint

1. Why do all events of `order-1` stay in order, but the console output across orders is interleaved?
2. You have 3 partitions and 4 consumers in a group. What does the 4th do?
3. How does Kafka do "queue" semantics vs "pub/sub" semantics?
4. What is consumer lag, and how did you measure it?
5. Commit before vs after processing — which delivery guarantee does each give?
6. Name two ways to replay history, and one real reason to do it.

## 🧹 Cleanup

```powershell
docker rm -f kafka
```

Next: [Lab 05 — Azure Service Bus](05-Azure-Service-Bus.md)
