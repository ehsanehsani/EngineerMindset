# Azure Functions

Quick reference for interviews: what Azure Functions are and the main trigger types.

## What is it?

**Azure Functions** is a **serverless compute** service on Azure. You write small pieces of code (functions) that run in response to events — without managing servers, VMs, or always-on apps.

Each function has:
- A **trigger** — what starts the function (HTTP request, timer, new blob, queue message, etc.).
- Optional **bindings** — easy input/output to other Azure services (read from a queue, write to a table, send to Service Bus) without boilerplate SDK code.

You pay mainly for **executions and compute time** (on the Consumption plan). Scale up/down automatically. Idle = near-zero cost.

**One-liner:** "Azure Functions is serverless, event-driven code on Azure — a function runs when a trigger fires, scales automatically, and you don't manage infrastructure."

## Simple .NET example

```csharp
public class MyFunctions
{
    [Function("SayHello")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Hello from Azure Functions!");
        return response;
    }
}
```

Trigger = HTTP. Function runs when someone hits the endpoint.

---

## Trigger types (how a function starts)

These are the main **types** of Azure Functions — grouped by what fires them.

### Web & API

| Trigger | Fires when |
|---------|------------|
| **HTTP** | HTTP request hits the function URL. Used for APIs, webhooks, lightweight REST endpoints. |

### Schedule

| Trigger | Fires when |
|---------|------------|
| **Timer** | Cron-style schedule (e.g. every night at 2 AM, every 5 minutes). Background jobs, cleanup, reports. |

### Storage & files

| Trigger | Fires when |
|---------|------------|
| **Blob** | A blob is created or updated in Azure Blob Storage. Image processing, file imports. |
| **Queue (Storage)** | A message arrives on an Azure Storage Queue. Simple async work, decoupling. |

### Messaging & events

| Trigger | Fires when |
|---------|------------|
| **Service Bus** | Message on a Service Bus queue or topic. Enterprise messaging, reliable delivery. |
| **Event Hubs** | Events arrive on an Event Hub. High-volume streaming / telemetry ingestion. |
| **Event Grid** | An Event Grid event is published. React to Azure resource changes or custom events. |

### Data

| Trigger | Fires when |
|---------|------------|
| **Cosmos DB** | Document changes in a Cosmos DB container. Sync, cache invalidation, projections. |

### Other (via extensions)

| Trigger | Fires when |
|---------|------------|
| **RabbitMQ** | Message on a RabbitMQ queue. |
| **Kafka** | Event on an Apache Kafka topic. |

> **Interview tip:** You don't need to memorize every extension. Know **HTTP**, **Timer**, **Blob**, **Queue**, **Service Bus**, and **Event Hub/Grid** — that covers most real-world and interview scenarios.

---

## Durable Functions (a special pattern)

**Durable Functions** is an extension for **long-running workflows** — not a separate hosting product, but a different *kind* of function:

| Type | Role |
|------|------|
| **Orchestrator** | Defines the workflow — calls activities in order, handles retries, waits. |
| **Activity** | A single step in the workflow (send email, call API, save to DB). |
| **Entity** | Stateful actor pattern — small stateful objects (counters, sessions). |

Use when you need multi-step processes (order pipeline, approval flow, fan-out/fan-in) instead of a single short trigger handler.

---

## Hosting plans (brief)

| Plan | When to use |
|------|-------------|
| **Consumption** | Default serverless — pay per execution, auto-scale, cold starts possible. |
| **Premium** | Always-warm instances, VNET, longer runs — production APIs with steady traffic. |
| **Dedicated (App Service)** | Run on your own App Service plan — predictable cost, full control, no consumption scaling model. |

---

## When to use Azure Functions

**Good fit:**
- Event-driven tasks (file uploaded → process it)
- Scheduled jobs (nightly sync)
- Lightweight APIs or webhooks
- Queue/message processing
- Small, focused logic — not a full monolith

**Less ideal:**
- Large, always-on web apps with complex UI → consider App Service or containers
- Very long, stateful processes without Durable Functions
- Heavy synchronous traffic needing minimal latency and no cold starts → Premium or App Service

**Interview one-liner:** "Azure Functions runs small event-driven code without managing servers — HTTP, Timer, Blob, Queue, and Service Bus are the most common triggers; Durable Functions adds orchestrated multi-step workflows on top."
