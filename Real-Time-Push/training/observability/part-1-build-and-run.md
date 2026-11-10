# Part 1: Build and run the Coffee Shop

## Before you start

- **Visual Studio 2022 (17.12+) or newer** with the **"ASP.NET and web development"** workload.
- **.NET 8 or newer** (you have .NET 10).
- **Aspire project templates.** In Visual Studio → **Create a new project** → search **"Aspire"**. If nothing shows up, close Visual Studio and run this in a terminal, then reopen VS:
  ```powershell
  dotnet new install Aspire.ProjectTemplates
  ```

---

## Step 1: Create the Aspire solution

1. **Create a new project** → search **"Aspire Empty App"** (it may be called ".NET Aspire Empty App") → **Next**.
2. **Project name:** `CoffeeShop` · **Location:** e.g. `C:\Repositories\Ehsan\Training` → **Next** → keep the defaults → **Create**.

✅ **Checkpoint:** Solution Explorer shows two projects:

| Project | What it is |
|---|---|
| `CoffeeShop.AppHost` | the **conductor**: starts all your services and the dashboard. Contains no business code. |
| `CoffeeShop.ServiceDefaults` | **shared setup** every service uses: OpenTelemetry (logs/traces/metrics), health checks, retries for HTTP calls. |

Open `CoffeeShop.ServiceDefaults/Extensions.cs` and look for `ConfigureOpenTelemetry`. Just read it, don't change anything yet. You'll see `WithMetrics(...)` and `WithTracing(...)`. **This is where your app is wired to OpenTelemetry.**

---

## Step 2: Create the Barista service

1. Right-click the **solution** → **Add → New Project…** → **ASP.NET Core Empty** → name `Barista.Api` → **Next**.
2. Framework: same as the others. **Configure for HTTPS: ❌ uncheck.** If you see **"Enlist in .NET Aspire orchestration"**, ✅ tick it (it does items 3 and 4 below for you). → **Create**.
3. Right-click `Barista.Api` → **Add → Project Reference…** → tick `CoffeeShop.ServiceDefaults` → OK.
4. Right-click `CoffeeShop.AppHost` → **Add → Project Reference…** → tick `Barista.Api` → OK.

Replace `Barista.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();   // logs, traces, metrics, health checks

var app = builder.Build();
app.MapDefaultEndpoints();      // /health and /alive endpoints

app.MapGet("/brew", async (string coffee, ILogger<Program> logger) =>
{
    logger.LogInformation("Brewing {Coffee}", coffee);

    if (coffee == "mocha")
    {
        logger.LogError("Out of chocolate, can't make {Coffee}", coffee);
        return Results.Problem("Out of chocolate");   // HTTP 500
    }

    var brewTimeMs = coffee == "cappuccino"
        ? Random.Shared.Next(1500, 2500)   // slow on purpose
        : Random.Shared.Next(100, 300);

    await Task.Delay(brewTimeMs);

    logger.LogInformation("{Coffee} ready after {BrewTimeMs} ms", coffee, brewTimeMs);
    return Results.Ok(new { coffee, brewTimeMs });
});

app.Run();
```

---

## Step 3: Create the CoffeeShop service

Same as Step 2, but name it **`CoffeeShop.Api`** (HTTPS unchecked, reference `ServiceDefaults`, and add it as a reference in `AppHost`).

Replace `CoffeeShop.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// An HttpClient for calling the barista. "barista" is a NAME, not a real address:
// Aspire's service discovery turns it into the barista's real URL.
builder.Services.AddHttpClient("barista", client =>
    client.BaseAddress = new Uri("https+http://barista"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapPost("/orders", async (string coffee, IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    logger.LogInformation("Order received for {Coffee}", coffee);

    var barista = httpClientFactory.CreateClient("barista");
    var response = await barista.GetAsync($"/brew?coffee={coffee}");

    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Order for {Coffee} failed: barista returned {StatusCode}", coffee, (int)response.StatusCode);
        return Results.Problem($"Sorry, we couldn't make your {coffee}.");
    }

    logger.LogInformation("Order for {Coffee} completed", coffee);
    return Results.Ok($"Your {coffee} is ready!");
});

app.Run();
```

---

## Step 4: Wire the services together in the AppHost

Replace `CoffeeShop.AppHost/AppHost.cs` (in older versions the file is called `Program.cs`):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var barista = builder.AddProject<Projects.Barista_Api>("barista");

builder.AddProject<Projects.CoffeeShop_Api>("coffeeshop")
    .WithReference(barista)   // coffeeshop may call barista; tells it barista's address
    .WaitFor(barista);        // start barista first

builder.Build().Run();
```

> 🧠 `Projects.Barista_Api` is generated automatically from the project reference (dots become underscores). If it shows red, build once (**Ctrl+Shift+B**).
>
> 🧠 `"barista"` here is the **same name** used in `https+http://barista` in Step 3. That's how service discovery connects them.

---

## Step 5: Run and tour the dashboard

1. Right-click `CoffeeShop.AppHost` → **Set as Startup Project** → **F5**.
2. A browser opens the **Aspire dashboard**.

✅ **Checkpoint: the Resources page** lists `barista` and `coffeeshop`, both **Running**, each with an address (e.g. `http://localhost:5123`). **Copy the coffeeshop address.** You'll use it below.

### Place some orders

Open a terminal (**View → Terminal**) and set the address you copied:

```powershell
$shop = "http://localhost:5123"   # ← your coffeeshop address

Invoke-RestMethod -Method Post "$shop/orders?coffee=latte"
Invoke-RestMethod -Method Post "$shop/orders?coffee=cappuccino"
Invoke-RestMethod -Method Post "$shop/orders?coffee=mocha"
```

✅ **Checkpoint:**
- latte → `Your latte is ready!` (fast)
- cappuccino → `Your cappuccino is ready!` (takes ~2 s)
- mocha → after a while (several seconds), an error with `Sorry, we couldn't make your mocha.` Part 2 explains why it takes so long.

### Tour the five dashboard pages

Open each page and just look around. Part 2 explains everything in detail.

| Page | What you see | Try |
|---|---|---|
| **Resources** | your services, their state and address | click a resource → its details and environment variables (look for `services__barista__...`: that's service discovery) |
| **Console** | the raw text output of each service, like a terminal | choose `barista`; you'll see the "Brewing ..." lines |
| **Structured** (logs) | the same logs as **searchable records with fields** | click a log line → see fields like `Coffee`, `TraceId`, `SpanId` |
| **Traces** | one row per request, with duration and the services involved | click the **mocha** order → a timeline with several barista calls (more on that in Part 2) |
| **Metrics** | charts of numbers over time | choose `coffeeshop` → `http.server.request.duration` |

> 🧠 **Console vs Structured logs:** Console is plain text, good for reading. Structured logs keep `{Coffee}` as a real **field**, so you can filter "all logs where Coffee = mocha". That's why the log calls use `{Coffee}` and not string interpolation `$"...{coffee}"`.

---

## What you learned in Part 1

- An Aspire solution = **AppHost** (starts everything) + **ServiceDefaults** (shared OpenTelemetry setup) + your services.
- `builder.AddServiceDefaults()` is the **one line** that makes a service send logs, traces and metrics.
- **Service discovery:** call another service by **name** (`https+http://barista`) instead of a hard-coded URL.
- The dashboard has a page per signal: **Structured logs**, **Traces**, **Metrics**.

## Troubleshooting

| Problem | Fix |
|---|---|
| No "Aspire" templates | `dotnet new install Aspire.ProjectTemplates`, then restart Visual Studio |
| `Projects.Barista_Api` is red | add the project reference to the AppHost (Step 2.4) and build once |
| The dashboard asks for a login token | start from Visual Studio (it logs you in automatically), or copy the login URL with `?t=...` from the AppHost's console window |
| A certificate error on start | run `dotnet dev-certs https --trust` once, then restart |
| The CoffeeShop call fails with "No such host is known (barista)" | check `.WithReference(barista)` in the AppHost and the `AddServiceDefaults()` line in `CoffeeShop.Api` |
| `AddServiceDefaults` not found | the service is missing its project reference to `CoffeeShop.ServiceDefaults` |

**Next:** [Part 2: Logs, traces, metrics](part-2-logs-traces-metrics.md)
