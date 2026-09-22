# Task, ValueTask, and async/await in C#

This note is for interview prep. Each section is one question you might get, with enough explanation that the answer actually sticks.

---

# What is a Task?

A `Task` is an object that means: **“this work is not finished yet, but it will be.”**

Think of a restaurant pager. You order food, they hand you a pager, and you sit down. You are not standing in the kitchen watching the cook. When the pager buzzes, you go pick up the plate.

- The **pager** is the `Task`
- The **buzz** is the task completing
- The **plate** is the result (`Task<string>` buzzes and gives you a string)

```csharp
Task<string> pager = http.GetStringAsync("https://api.example.com/me");

// ... the thread can do other work here. The HTTP call is in flight.

string plate = await pager;   // wait for the buzz, then take the result
```

`Task` itself does not mean “a new thread started.” It only means “work that finishes later.” That work might be:

- waiting for a database / HTTP / file (almost no CPU, just waiting)
- or real CPU work running on the thread pool (`Task.Run`)

A `Task` also remembers how it ended: succeeded, failed (exception), or canceled. After it finishes you can read the result, or the exception, as many times as you want. That is one reason `Task` is the default return type for async methods.

If the method has no value to return, use `Task`. If it returns a value, use `Task<T>`:

```csharp
public async Task SaveAsync() { ... }           // "I'm done"
public async Task<User> GetUserAsync() { ... }  // "I'm done, here is the user"
```

---

# What do async and await actually do?

`async` and `await` are **syntax** so you can write the pager story as normal top-to-bottom code.

Without them you would chain callbacks: “when the HTTP call finishes, then parse JSON, then save to DB.” That gets ugly fast. With `await`, you write the steps in order. The compiler turns the method into a **state machine**: it runs until an `await`, pauses, gives the thread back, and continues from that line when the task completes.

```csharp
public async Task<string> GetUserNameAsync(int id)
{
    var user = await db.Users.FindAsync(id);   // pause here, don't block a thread
    return user.Name;                          // continue here later
}
```

**Important sentence for interviews:** `await` does **not** start a new thread. It **releases** the current thread while you wait.

Why that matters: an HTTP call might take 200ms. If you call the sync version, a thread sits idle for 200ms doing nothing. In a web API you only have a limited number of threads. If 1000 requests all block, the server looks “full” even though the CPUs are bored. With `await`, those threads go serve other requests and come back when the database answers.

### Return types you will be asked about

- `async Task` / `async Task<T>` — the normal choice
- `async ValueTask` / `async ValueTask<T>` — same idea, cheaper in a special case (next section)
- `async void` — **only for event handlers** (button click). The caller cannot `await` it. If it throws, the exception is hard to catch and can crash the app.

```csharp
public async Task SaveAsync() { await repo.SaveAsync(); }   // good

public async void Save() { await repo.SaveAsync(); }        // bad, except UI events
```

### Async all the way

If something returns a `Task`, **await it**. Don’t mix async with `.Result` or `.Wait()` on the same path — see the next section.

---

# What is the difference between `await FooAsync()` and `FooAsync().Result`?

Both wait for the same `Task` to finish and give you the value. The difference is **what happens to the thread while you wait**.

```csharp
string name = await GetNameAsync();       // non-blocking
string name = GetNameAsync().Result;      // blocking
```

**`await`** — the method pauses, **gives the thread back** (thread pool, UI, request thread). That thread can serve another request or keep the window responsive. When the task completes, the method continues.

**`.Result`** (same story as `.Wait()` and `.GetAwaiter().GetResult()`) — the current thread **sits there and blocks** until the task is done. It does nothing useful. In a web API that is one fewer worker for other requests. In a desktop app the UI freezes.

That is already enough to prefer `await`. Two extra reasons interviewers like:

**1. Deadlock (UI / old ASP.NET)**  
The UI thread calls `.Result` and waits. Inside, `GetNameAsync` does `await http.GetStringAsync(...)` and wants to **resume on that same UI thread**. The UI thread is busy blocked on `.Result`. Nobody can move. Hang forever.

`await` never holds the thread, so the continuation can run.

ASP.NET Core has no SynchronizationContext, so this exact deadlock is rare there — but `.Result` still wastes a thread-pool thread (**thread-pool starvation** under load).

**2. Exceptions**  
`await` throws the **real** exception (`HttpRequestException`, `InvalidOperationException`, …).  
`.Result` wraps it in `AggregateException`. Your `catch` of the inner type misses it unless you unwrap.

```csharp
try { await BoomAsync(); }          // catch InvalidOperationException — works
try { BoomAsync().Result; }         // catch InvalidOperationException — often misses
```

When is `.Result` acceptable? Almost never on a call that is still running. After `await Task.WhenAll(...)`, the tasks are already done — `userTask.Result` is then just reading a finished value (or keep using `await userTask`, which is immediate and still unwraps exceptions cleanly).

**Interview line:** Same result, different wait. `await` frees the thread. `.Result` blocks it, can deadlock a UI, and wraps exceptions in `AggregateException`. Always `await`.

---

# Does `await MethodB()` create a new thread?

This is the question they ask with two nested async methods. Code looks like this:

```csharp
async Task MethodA()
{
    await MethodB();
}

async Task MethodB()
{
    var result = HeavyCpuWork();   // hashing, a big loop, image resize — CPU
    await SomethingAsync();        // HTTP / DB — waiting
}
```

**Short answer:** `async` / `await` does **not** create a thread. It is still **one story, in order**. Threads may **change** after an `await`. They do not run MethodA and MethodB **in parallel**.

Walk through it. Somebody (a controller, a button click) calls `await MethodA()` on **Thread 1**.

| Step | What runs | Which thread |
|---|---|---|
| 1 | `MethodA` starts, calls `MethodB()` | Thread 1 |
| 2 | `HeavyCpuWork()` | **Still Thread 1** — this **blocks**. No extra thread. The UI freezes / the request thread is busy. |
| 3 | `await SomethingAsync()` | Thread 1 is **released**. Nobody sits idle waiting for HTTP. |
| 4 | `SomethingAsync` completes, `MethodB` continues after `await` | Thread pool thread (ASP.NET Core) — maybe Thread 1, maybe Thread 2. UI apps: back to the UI thread unless `ConfigureAwait(false)`. |
| 5 | `MethodB` finishes, `MethodA` continues after `await` | Same rule as step 4 |

So:

- **Before the first `await`**, everything is **synchronous on the caller’s thread**. Marking the method `async` does not move `HeavyCpuWork` off Thread 1.
- **`await` of I/O** is the pause. No dedicated thread is created for the HTTP call.
- **After `await`**, work **resumes** — often on a **different** thread-pool thread. That is a thread **switch**, not “a new thread was created for this method.”
- MethodA and MethodB never run **together**. B runs; A is paused at `await MethodB()`. Then A continues.

If they wanted the CPU work off the request/UI thread, that is the one place for `Task.Run`:

```csharp
async Task MethodB()
{
    var result = await Task.Run(() => HeavyCpuWork());  // now a pool thread does the CPU
    await SomethingAsync();
}
```

Do **not** wrap `SomethingAsync` in `Task.Run`. That I/O already awaits.

**Interview line:** `await` does not start a thread. CPU code before the first `await` runs on the caller’s thread and blocks it. After `await`, you may continue on another pool thread. One flow, not two methods in parallel.

---

# Task vs a Thread — when do I use Task.Run?

A **thread** is an OS worker. It is expensive. A **Task** is just a promise of work.

`async` is also **not** parallelism. One `async` method is still one story, told with pauses. If you want two things at the same time, that is `Task.WhenAll` or `Task.Run`.

Two different kinds of work:

**I/O (database, HTTP, file, queue)** — you are waiting. Use the `*Async` API and `await` it. Do **not** wrap it in `Task.Run`. That would just move the wait onto another thread, which is extra cost for no speedup.

**CPU (hashing, resizing an image, a heavy loop)** — a thread must actually work. If you are on a web request or a UI thread and you don’t want to freeze it:

```csharp
var hash = await Task.Run(() => ComputeHeavyHash(fileBytes));
```

`Task.Run` sends the CPU work to the thread pool. That is the right use.

Also: don’t fake async. If the method is just `return a + b`, make it a normal method. `async` adds a state machine. If an interface forces `Task<int>`, return `Task.FromResult(a + b)` instead of marking the method `async`.

---

# What is ValueTask? When would I use it instead of Task?

Default to `Task`. Say that first in an interview.

`ValueTask<T>` exists for a performance niche: a method that is called **a lot**, and **often already has the answer** (cache hit, buffer already filled).

Every `Task` is a class, so it lives on the heap. If your cache hits 99% of the time, you still allocate a `Task` object on every call just to say “here is the cached user.” That is wasted garbage collection. `ValueTask<T>` is a **struct**. On a cache hit it can carry the result itself, with no `Task` allocation. On a cache miss it can still wrap a real `Task` and await the database.

```csharp
public async ValueTask<User> GetUserAsync(int id)
{
    if (cache.TryGetValue(id, out var cached))
        return cached;                         // already have it — no Task allocated

    var user = await db.Users.FindAsync(id);   // really async — fine
    cache[id] = user;
    return user;
}
```

Use `ValueTask` only when **all** of these are true:

1. The method is on a hot path (called constantly)
2. It often completes without actually waiting
3. You care about allocations (you measured it, or you are writing a library like a serializer / socket pipeline)

If you are writing a normal ASP.NET controller or service, `Task` is simpler and fast enough.

### The catch (they like this)

A `Task` is a real object you can await twice, store, pass around. A `ValueTask` might be backed by a **reusable** slot. After you await it, that slot can be recycled.

```csharp
ValueTask<int> vt = GetAsync();
int a = await vt;     // OK
int b = await vt;     // not OK — do not await twice
```

If you need to wait more than once, or store it, convert it first:

```csharp
Task<int> t = GetAsync().AsTask();
await t;
await t;              // OK, it's a normal Task now
```

Don’t block on it with `.Result` either. Await it once, or call `.AsTask()`.

---

# What is ConfigureAwait(false)?

After an `await`, C# has to decide: **where do I continue?**

By default it tries to continue **where you started** — the same UI thread, or the same old ASP.NET request context. That is usually what you want in *your app*. It is often *not* what you want inside a *library*.

`ConfigureAwait(false)` means: **“I don’t need to go back. Continue on any free thread.”**

### A real scenario: a desktop button

You have a WinForms / WPF window. Only the **UI thread** is allowed to touch controls (`label.Text = ...`). If a background thread does it, you get a crash.

```csharp
private async void DownloadButton_Click(object sender, EventArgs e)
{
    statusLabel.Text = "Downloading...";

    var json = await http.GetStringAsync(url);   // default await

    statusLabel.Text = json;   // must run on the UI thread — and it will
}
```

What happens:

1. Click runs on the UI thread
2. `await` starts the download and **frees the UI thread** — the window can still paint, you can still move it
3. When the download finishes, default `await` **comes back to the UI thread**
4. Setting `statusLabel.Text` is safe

That “come back to where I was” behavior is the whole point of the default.

Now imagine you write this instead in the button handler:

```csharp
var json = await http.GetStringAsync(url).ConfigureAwait(false);
statusLabel.Text = json;   // maybe a random thread-pool thread → crash
```

You told C# “don’t bother coming back.” Continuation may run on a background thread. The label update blows up. So **in UI code, you usually want the default `await`.** Same idea in old ASP.NET: after the await you may still need `HttpContext`. ASP.NET Core does not have this context, so this exact problem is rare in Core apps.

### The other half of the scenario: a library

You are writing `UserClient`, used by many apps. It only downloads JSON and deserializes it. It never touches a label, never reads `HttpContext`.

```csharp
public async Task<User> GetUserAsync()
{
    var json = await http.GetStringAsync(url).ConfigureAwait(false);
    return JsonSerializer.Deserialize<User>(json);
}
```

Here `ConfigureAwait(false)` is the right call:

- The library does not need the caller’s UI / request thread
- Coming back to that thread would only queue extra work on it
- If the app above you made a mistake and blocked with `.Result` on the UI thread, capturing that context can deadlock: the UI thread is stuck waiting, and your continuation is stuck waiting for the UI thread

So the practical rule:

| Where you are | What to do |
|---|---|
| App code (controller, button click, page) | `await foo;` — you probably still need that context |
| Library / shared helper that only computes or I/O | `await foo.ConfigureAwait(false);` — you don’t |

**Interview line:** `ConfigureAwait(false)` says the rest of this method does not need the original context (UI thread, old ASP.NET request). Continue anywhere. Use it in libraries. In application code, default `await` is the safe choice.

---

# How do I run several async calls together?

If the second call **needs** the first result, await them in order:

```csharp
var user = await GetUserAsync(id);
var orders = await GetOrdersAsync(user.CustomerId);   // needs user first
```

If they are **independent**, start both, then wait for both. That is the difference between 200ms + 200ms and about 200ms.

```csharp
var userTask = GetUserAsync(id);
var ordersTask = GetOrdersAsync(id);

await Task.WhenAll(userTask, ordersTask);

var user = await userTask;       // already done, returns immediately
var orders = await ordersTask;
```

- `WhenAll` — wait until every task finishes. If one fails, you get the exception.
- `WhenAny` — wait until the first one finishes (timeouts, “first answer wins”).

Related: `await foreach` (`IAsyncEnumerable<T>`) is for **streaming** — process rows as they arrive instead of loading a full list first (EF `AsAsyncEnumerable()`, reading a file line by line).

---

# How do I cancel an async call?

Pass a `CancellationToken` down the chain. Don’t swallow it. In ASP.NET Core, `HttpContext.RequestAborted` is already there when the client disconnects.

```csharp
public async Task<string> GetAsync(string url, CancellationToken ct)
{
    return await http.GetStringAsync(url, ct);
}

// timeout: cancel after 3 seconds
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
var json = await GetAsync(url, cts.Token);
```

---

# Short answers to memorize

**What is a Task?** A promise that work will finish later. Not the same thing as a thread.

**What does await do?** Pauses the method, frees the thread, continues when the task completes. It does not create a thread.

**Task vs ValueTask?** `Task` is the default. `ValueTask` avoids an allocation when a hot method often already has the result. Await a `ValueTask` only once (or call `.AsTask()`).

**What is ConfigureAwait(false)?** “I don’t need to resume on the original UI/request thread.” Use it in libraries. In app/UI code, use normal `await` so you can safely touch the UI / request.

**Why not async void?** Caller cannot await it. Exceptions are easy to lose. Only event handlers.

**`await` vs `.Result`?** Same value. `await` frees the thread; `.Result` blocks it, can deadlock UI/old ASP.NET, and throws `AggregateException`. Always `await`.

**Does await create a thread?** No. Nested `async` methods are one sequential flow. CPU work before the first `await` stays on the caller’s thread. After `await`, you may resume on a different pool thread. `Task.Run` is what moves CPU work.

**When Task.Run?** CPU-heavy work you want off the UI/request thread. Not for HTTP/DB that already has an async API.

**WhenAll vs sequential await?** `WhenAll` when the calls don’t depend on each other. Sequential when they do.
