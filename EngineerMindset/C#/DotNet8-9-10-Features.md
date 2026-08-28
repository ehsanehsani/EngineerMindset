# .NET 8, 9, and 10 — features worth knowing for interviews

Newest first. This is **not** a changelog — only things an interviewer might ask, and that you would actually use.

| | .NET 10 | .NET 9 | .NET 8 |
|---|---|---|---|
| Released | Nov 2025 | Nov 2024 | Nov 2023 |
| C# | 14 | 13 | 12 |
| Support | **LTS** until Nov 2028 | STS (18 months) until May 2026 | **LTS** until Nov 2026 |

Companies usually ship on **LTS** (8 or 10). .NET 9 is still fair game: “what landed last year?”

Each version starts with **1–2 platform (.NET) headlines**, then the C# / library questions.

---

# .NET 10 (C# 14) — LTS

**Platform headlines:** Native AOT is the default for file-based apps and keeps getting smaller/faster. You can run a single `.cs` file with no project.

---

## What is Native AOT?

Default .NET is **JIT**: at runtime the CLR turns IL into machine code, method by method. That needs a .NET runtime on the machine, uses more memory, and the first call of a method is slower.

**Native AOT** compiles to a **native executable at publish time**. No JIT. No separate .NET runtime on the server. Startup is fast, RAM is lower — closer to a Go binary than to a classic ASP.NET app.

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

```bash
dotnet publish -c Release -r linux-x64
```

**The tradeoff** (say this in the interview): reflection, `Assembly.Load`, and some runtime code gen may break. The publish is slower and the binary is **per OS/CPU**. JSON usually needs a source generator, not `Newtonsoft` + reflection.

**Where it showed up:**

- **.NET 7** — preview, mostly console apps
- **.NET 8** — first version you could AOT a real **ASP.NET Core API** (Minimal APIs). This is when people started asking it in interviews.
- **.NET 9** — more of the BCL and ASP.NET is AOT-safe, smaller output
- **.NET 10** — file-based apps **enable AOT by default**; you can publish **.NET tools** as AOT; more JIT/AOT runtime work under the hood

Use AOT for: containers, CLI tools, cold-start (serverless), small APIs. Skip it for: plugin hosts, heavy reflection, “we use Newtonsoft everywhere.”

Longer comparison: [AOT vs JIT](../dotnet/AOT-vs-JIT.md).

**Interview line:** AOT compiles to native code at publish time — faster start, less memory, no runtime install. Cost is weaker reflection and a platform-specific binary. It became real for web APIs in .NET 8; .NET 10 turns it on by default for single-file apps.

---

## What are file-based apps?

You can run a **single `.cs` file** without a `.csproj`.

```csharp
// hello.cs
Console.WriteLine("Hello");
```

```bash
dotnet run hello.cs
```

Same niche as a Python script: spikes, glue, small tools. NuGet is a directive at the top (`#:package ...`) instead of a project file. Native AOT is on by default here — turn it off with `#:property PublishAot false` if a library needs reflection.

This is not how you build a production API. When the script grows, `dotnet project convert` can turn it into a real project.

**Interview line:** .NET 10 runs `dotnet run app.cs` with no `.csproj`. Scripts and samples, not big apps. AOT is on by default.

---

## What is the `field` keyword?

You start with an auto-property:

```csharp
public string Email { get; set; }
```

Later you want a null check in the setter. Before C# 14 you had to introduce `_email` and write both accessors. Now you keep the auto-property and only customize the part that needs logic. `field` is the compiler’s hidden backing field.

```csharp
public string Email
{
    get;
    set => field = value ?? throw new ArgumentNullException(nameof(value));
}
```

Same idea for clamping a number:

```csharp
public int Quantity
{
    get;
    set => field = value < 0 ? 0 : value;
}
```

You don’t name a private field. Everything else in the class should go through the property.

If you already have a member named `field`, that can clash — rename it, or use `@field` / `this.field`. Unlikely in new code.

**Interview line:** `field` lets you add a little logic to an auto-property without declaring `_backingField`.

---

## What are extension members?

You already know **extension methods**: `list.Where(...)` is a static method that *looks* like an instance method.

C# 14 extends that idea. Inside an `extension` block you can add:

- extension **properties** (`list.IsEmpty`)
- extension **methods** (the old idea, new syntax)
- **static** extension members (`IEnumerable<int>.Identity`)

```csharp
public static class EnumerableExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        public bool IsEmpty => !source.Any();
    }
}
```

```csharp
int[] data = [1, 2, 3];
if (data.IsEmpty) { }    // looks like a real property on the array
```

Old `this IEnumerable<T> source` methods still work. Use this when a property reads more naturally than a method (`IsEmpty` vs `IsEmpty()`), and you don’t own the type.

**Interview line:** `extension` blocks add properties (and static members) to types you don’t own, not only methods.

---

## What is null-conditional assignment?

You already know `customer?.Name` — if `customer` is null, you don’t crash.

C# 14 lets `?.` sit on the **left** of `=` (and `+=`, `-=`, …). If the object is null, **the assignment is skipped**. The right-hand side is **not even run**.

```csharp
// before
if (customer is not null)
    customer.LastOrder = CreateOrder();

// now
customer?.LastOrder = CreateOrder();
```

If `customer` is null, `CreateOrder()` does **not** execute. That matters if the right side inserts a row or sends an email.

```csharp
cart?.Total += item.Price;   // no-op when cart is null
```

`++` / `--` still don’t work with `?.`.

**Interview line:** `customer?.Order = x` assigns only when `customer` is not null, and it does not evaluate `x` otherwise.

---

## What changed in Minimal APIs in .NET 10?

The one you will actually use: **built-in validation**.

Before, Minimal APIs did not run `[Required]` / `[Range]` the way MVC controllers did. In .NET 10 you opt in and DataAnnotations work on parameters and bodies.

```csharp
builder.Services.AddValidation();

app.MapPost("/users", (CreateUserRequest body) =>
    Results.Created($"/users/{body.Email}", body));

public class CreateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; }

    [Range(18, 120)]
    public int Age { get; set; }
}
```

Invalid input returns a validation problem instead of hitting your handler.

If they ask “anything else?”: OpenAPI 3.1 (YAML too), Server-Sent Events as a result type, JSON Patch. Validation is the day-to-day one.

**Interview line:** .NET 10 Minimal APIs validate with DataAnnotations after `AddValidation()`.

---

## What is new in EF Core 10 that is worth mentioning?

**LeftJoin / RightJoin as real LINQ operators.** The old “group join + `DefaultIfEmpty`” pattern is easy to get wrong on a whiteboard.

```csharp
var rows = customers.LeftJoin(
    orders,
    c => c.Id,
    o => o.CustomerId,
    (c, o) => new { Customer = c, Order = o });   // o is null when there is no order
```

**Named query filters.** Soft-delete and multi-tenant filters can have names, and you can turn **one** off without disabling all of them.

```csharp
modelBuilder.Entity<Order>()
    .HasQueryFilter("SoftDelete", o => !o.IsDeleted)
    .HasQueryFilter("Tenant", o => o.TenantId == tenantId);

context.Orders.IgnoreQueryFilters("SoftDelete");
```

**Interview line:** EF 10 adds `LeftJoin`/`RightJoin` in LINQ, and named global query filters you can ignore one-by-one.

---

# .NET 9 (C# 13) — STS

**Platform headlines:** HybridCache (L1 memory + L2 Redis, stampede-safe). Templates ship **built-in OpenAPI** instead of Swashbuckle.

---

## What is HybridCache?

`IMemoryCache` is fast but dies when the process dies, and it is not shared across servers. `IDistributedCache` (Redis) is shared, but you serialize bytes yourself and 1,000 requests for a cold key can all hit the database (**cache stampede**).

`HybridCache` is one API for both: **L1 in-memory** on this server, **L2 distributed** if you registered Redis. `GetOrCreateAsync` does the get-or-load dance, serializes for you, and **only one caller** fills a missing key — the others wait for that result.

```csharp
builder.Services.AddHybridCache();
// optional: also AddStackExchangeRedisCache(...) and it uses Redis as L2

public class ProductService(HybridCache cache, IProductRepository repo)
{
    public Task<Product> GetAsync(int id, CancellationToken ct) =>
        cache.GetOrCreateAsync(
            $"product:{id}",
            async cancel => await repo.GetByIdAsync(id, cancel),
            cancellationToken: ct);
}
```

You can tag entries and invalidate a whole group (`"products"`) instead of deleting keys one by one.

Your repo already has a caching note — this is the .NET 9 answer when they ask “how would you cache in a multi-server API now?”

**Interview line:** HybridCache is L1 + L2 with stampede protection. Prefer it over hand-rolling `IMemoryCache` + `IDistributedCache`.

---

## What is Guid version 7? Why not `Guid.NewGuid()` as a database key?

`Guid.NewGuid()` is version 4 — random. As a SQL primary key it **fragments indexes** (every insert lands in a random page). Version 7 is **time-ordered**: new ids sort after old ones, so inserts append like a sequence, but you still get a globally unique id (no extra identity column, safe to generate in the app).

```csharp
Guid id = Guid.CreateVersion7();   // .NET 9
```

Use it for new entity ids. Don’t rewrite old v4 rows unless you have a migration plan.

**Interview line:** `Guid.CreateVersion7()` is time-sortable, so it is a much better DB primary key than random `NewGuid()`.

---

## What are params collections?

Until C# 13, `params` meant **`params T[]`** — the compiler always allocated an array.

Now `params` works with the same types as collection expressions: `ReadOnlySpan<T>`, `Span<T>`, `List<T>`, `IEnumerable<T>`, …

```csharp
void Log(params ReadOnlySpan<object> parts)
{
    foreach (var p in parts)
        Console.Write(p);
}

Log(1, " ", "orders");   // no array allocated — a span on the stack
```

Call sites stay `Foo(1, 2, 3)`. The win is on the **callee**: a `params ReadOnlySpan<T>` overload is cheaper, and the BCL started using that. If both `params T[]` and `params ReadOnlySpan<T>` exist, a list of values usually picks the span.

**Interview line:** `params` is no longer arrays-only. `params ReadOnlySpan<T>` avoids the hidden array allocation.

---

## What is `System.Threading.Lock`?

For years `lock (obj)` used a random `object` as the key for `Monitor`. That works, but the object is a heap allocation whose only job is to be a lock.

.NET 9 adds `System.Threading.Lock`. The C# 13 `lock` statement, if the target is a `Lock`, calls `EnterScope()` (a `ref struct` that disposes / exits). Cheaper, and the type **says** “this field is a lock,” so nobody `lock`s the wrong object.

```csharp
private readonly Lock _gate = new();
private int _count;

public void Increment()
{
    lock (_gate)
    {
        _count++;
    }
}
```

Don’t `lock` on `this`, on a `string`, or on a type. Dedicated `Lock` field (or the old dedicated `object` field) is the rule. New code: use `Lock`.

**Interview line:** `System.Threading.Lock` is the dedicated lock type. `lock (_gate)` on a `Lock` uses a faster API than `Monitor` on a dummy object.

---

# .NET 8 (C# 12) — LTS

**Platform headlines:** Native AOT for **ASP.NET Core APIs** (see AOT above). **Keyed DI** — several implementations of one interface, picked by name.

---

## What are primary constructors?

A primary constructor puts the constructor parameters **on the class line**. Those parameters are then in scope for the whole class — you don’t repeat them in a constructor body.

Before:

```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;
    private readonly ILogger<OrderService> _log;

    public OrderService(IOrderRepository repo, ILogger<OrderService> log)
    {
        _repo = repo;
        _log = log;
    }

    public Task SaveAsync(Order order) => _repo.SaveAsync(order);
}
```

With a primary constructor:

```csharp
public class OrderService(IOrderRepository repo, ILogger<OrderService> log)
{
    public Task SaveAsync(Order order)
    {
        log.LogInformation("Saving {Id}", order.Id);
        return repo.SaveAsync(order);
    }
}
```

`repo` and `log` are captured for you. No extra constructor, no `_repo` field unless you want one.

### The interview trap (records vs classes)

Records already had this, and record parameters **become properties**:

```csharp
public record User(string Name, int Age);
// User has public Name and Age properties
```

On a **class**, primary constructor parameters are **not** properties. They are like private fields:

```csharp
public class User(string name, int age);

var u = new User("Ada", 36);
Console.WriteLine(u.name);   // error — name is not a public member
```

If you want a public property, assign it yourself:

```csharp
public class User(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;
}
```

**When extra constructors exist**, they must call the primary one with `this(...)`.

**Interview line:** Primary constructors cut DI boilerplate on classes. Unlike records, class parameters are not properties — they are just in-scope values.

---

## What is Expression? (two different things)

Interviewers say “Expression” and mean one of these. Learn both.

1. **Collection expressions** — C# 12, **new**. The `[1, 2, 3]` syntax.
2. **Expression trees** (`Expression<Func<T>>`) — **not new** (LINQ, C# 3). How EF turns a lambda into SQL.

---

### 1. Collection expressions (C# 12 / .NET 8) — the new syntax

Before C# 12, creating a collection looked different for every type:

```csharp
int[] a = new[] { 1, 2, 3 };
List<int> b = new List<int> { 1, 2, 3 };
Span<int> c = stackalloc int[] { 1, 2, 3 };
```

Now you write the **same** thing. The **left side** (the target type) decides what is created:

```csharp
int[] a = [1, 2, 3];
List<int> b = [1, 2, 3];
Span<int> c = [1, 2, 3];
```

That is a **collection expression**. Brackets, values inside. You can pass it into a method too — the parameter type is the target:

```csharp
void Print(List<int> numbers) { }

Print([1, 2, 3]);   // compiler builds a List<int>
```

**Spread** (`..`) copies another collection into this one, like spreading cards onto the table:

```csharp
int[] extra = [4, 5];
int[] all = [1, 2, 3, ..extra, 6];   // 1, 2, 3, 4, 5, 6
```

Empty is just `[]` when the type is already known (`int[] empty = [];`).

**Interview line:** `[1, 2, 3]` is a collection expression. One syntax for array, list, span. `..other` copies items in. C# 12.

---

### 2. Expression trees — the other “Expression” (learn this)

This is `System.Linq.Expressions`. Same word, totally different idea.

A lambda in C# can be stored two ways. The **compiler looks at the variable type** and chooses:

```csharp
Func<User, bool> fn = u => u.Age > 18;
Expression<Func<User, bool>> expr = u => u.Age > 18;
```

The `=>` looks identical. What you get is not.

#### `Func` = a machine that already runs

`fn` is compiled IL, like any other method. You call it. It returns a bool. The CPU has no idea it came from `Age > 18` — that information is gone.

```csharp
bool ok = fn(someUser);   // just runs. true or false.
```

`IEnumerable.Where(fn)` does this in a loop, **in your process**, on objects already in memory.

#### `Expression` = the recipe, not the meal

`expr` is **not** a method you can call. It is a **tree of objects** that *describe* the code:

```
        >          (GreaterThan)
       / \
     Age   18      (property)  (constant)
      |
      u            (the parameter)
```

You can inspect it at runtime (property name, operator, constant). EF Core / `IQueryable` **reads that tree** and writes SQL:

```sql
WHERE [u].[Age] > 18
```

The database does the filter. Only matching rows come over the network.

If EF only had a `Func`, it would have to **download every user**, then run your C# function in memory. That is the whole point of `IQueryable`.

#### You cannot “call” an expression

```csharp
expr(someUser);            // does not compile

Func<User, bool> fn2 = expr.Compile();  // turn the recipe into a machine
bool ok = fn2(someUser);   // now you can run it in memory
```

`.Compile()` is what you need to run it as C#. EF does **not** compile it — it **translates** it to SQL.

#### Same lambda, different method, different fate

```csharp
IQueryable<User> dbUsers = context.Users;
IEnumerable<User> list = dbUsers.ToList();   // already in memory

dbUsers.Where(u => u.Age > 18);  // Where expects Expression<Func<...>> → SQL
list.Where(u => u.Age > 18);     // Where expects Func<...>           → C# loop
```

You wrote the same `u => u.Age > 18`. Overload resolution picked `Expression` vs `Func` from the collection type.

**The trap:** `context.Users.AsEnumerable().Where(u => u.Age > 18)` forces `Func`. SQL becomes `SELECT *` and the filter runs in C#.

#### Why a tree?

The compiler could have given EF a black-box method. A black box cannot become SQL. A tree can: each node is a known operation (`>`, `Age`, `18`, `And`, `StartsWith`). EF walks node by node, like translating a sentence.

You almost never build the tree by hand. The compiler does it when the target type is `Expression<...>`. Hand-built (just so you see it is real objects):

```csharp
var u = Expression.Parameter(typeof(User), "u");
var body = Expression.GreaterThan(
    Expression.Property(u, nameof(User.Age)),
    Expression.Constant(18));
Expression<Func<User, bool>> expr =
    Expression.Lambda<Func<User, bool>>(body, u);
// same tree as: u => u.Age > 18
```

**Interview line:** `Func` is runnable code. `Expression<Func<>>` is a description of the code (a tree). EF reads the tree and generates SQL. Collection expressions `[1,2,3]` are unrelated — that is only the new C# 12 syntax.

More on IQueryable: [IEnumerable vs IQueryable](IEnumerable-IQueryable.md).

---

## What are keyed services in DI?

The built-in container can register **several implementations of the same interface**, each with a **key**. You ask for the one you want by that key.

Classic problem: Stripe and PayPal both implement `IPaymentGateway`. Before .NET 8 you filtered `IEnumerable<IPaymentGateway>`, wrote a factory, or used a third-party container.

```csharp
builder.Services.AddKeyedSingleton<IPaymentGateway, StripeGateway>("stripe");
builder.Services.AddKeyedSingleton<IPaymentGateway, PaypalGateway>("paypal");
```

Inject the one you need:

```csharp
public class CheckoutService(
    [FromKeyedServices("stripe")] IPaymentGateway stripe)
{
    public Task PayAsync(Money amount) => stripe.ChargeAsync(amount);
}
```

Or resolve at runtime when the key comes from config / the request:

```csharp
var gateway = provider.GetRequiredKeyedService<IPaymentGateway>(providerName);
```

Same lifetimes: `AddKeyedSingleton`, `AddKeyedScoped`, `AddKeyedTransient`.

Don’t use this for “I have one implementation.” Use it when **the interface is shared and the choice is a name** (email vs SMS, S3 vs Azure Blob).

**Interview line:** Keyed DI registers multiple implementations of one interface under a key, and `[FromKeyedServices("stripe")]` picks which one to inject.

---

## What is TimeProvider?

`DateTime.UtcNow` is a static call. You cannot fake it in a unit test. So “this discount expires after 24 hours” becomes painful to test.

`TimeProvider` is an abstraction over “what time is it?” and “wait a bit.” Production uses `TimeProvider.System`. Tests use `FakeTimeProvider` (package `Microsoft.Extensions.Time.Testing`).

```csharp
public class CouponService(TimeProvider time)
{
    public bool IsExpired(Coupon coupon)
        => time.GetUtcNow() > coupon.ExpiresAt;
}
```

```csharp
builder.Services.AddSingleton(TimeProvider.System);

var fake = new FakeTimeProvider();
fake.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
var sut = new CouponService(fake);
```

You can also advance time and run delays without waiting in real life — useful for timeout / retry tests.

**Interview line:** Don’t call `DateTime.UtcNow` in domain logic. Inject `TimeProvider` so tests can freeze or jump the clock.

---

## How do you handle exceptions globally in ASP.NET Core 8?

.NET 8 added `IExceptionHandler`. Instead of a giant `UseExceptionHandler` lambda or a custom middleware class, you write a small handler and register it.

```csharp
public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var body = new { error = exception.Message };
        context.Response.StatusCode = exception is KeyNotFoundException
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsJsonAsync(body, ct);
        return true;   // true = I handled it, stop the pipeline
    }
}
```

```csharp
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

app.UseExceptionHandler();
```

You can register several handlers. The first one that returns `true` wins. Return `false` to say “not mine, try the next.”

**Interview line:** Implement `IExceptionHandler`, register it, call `UseExceptionHandler()`. Cleaner than custom middleware, and it plays with ProblemDetails.

---

## What is FrozenDictionary / FrozenSet?

A dictionary you **fill once**, then only **read**. After freeze, it cannot change. Lookups are optimized for that “read-only forever” case.

Real scenario: country-code map, feature flags loaded at startup, translation table.

```csharp
var builder = new Dictionary<string, string>
{
    ["SE"] = "Sweden",
    ["DE"] = "Germany",
};

FrozenDictionary<string, string> countries = builder.ToFrozenDictionary();

countries.TryGetValue("SE", out var name);   // fast
countries["FR"] = "France";                  // you can't
```

If the data changes at runtime, keep a normal `Dictionary` / `ConcurrentDictionary`. If it is config-like and lives for the app lifetime, freeze it.

**Interview line:** Frozen collections are immutable and tuned for heavy reads after a one-time fill. Startup data, not per-request data.

---

# Short answers to memorize

### .NET 10
**Native AOT?** Compile to native at publish time. Fast start, less RAM, no runtime on the box. Weak reflection. Real for APIs in .NET 8; default on file-based apps in .NET 10.

**File-based apps?** `dotnet run app.cs` — no `.csproj`.

**`field`?** Logic on an auto-property without `_backingField`.

**Extension members?** Extension **properties**, not only methods.

**`?.=` ?** Assign only if not null; skip the right-hand side when null.

**Minimal APIs?** Built-in DataAnnotations (`AddValidation()`).

**EF 10?** `LeftJoin` / `RightJoin`, named query filters.

### .NET 9
**HybridCache?** L1 memory + L2 Redis, stampede protection, one `GetOrCreateAsync`.

**Guid v7?** Time-ordered GUID — better as a SQL PK than `NewGuid()`.

**params collections?** `params` on `ReadOnlySpan<T>` / lists, not only arrays.

**`Lock`?** Dedicated lock type; `lock (_gate)` uses a faster API than `Monitor` on an `object`.

### .NET 8
**Primary constructors?** Parameters on the class line. On classes they are **not** properties.

**Collection expressions / “Expression”?** Two answers. New (C# 12): `[1, 2, 3]`. Old (LINQ): `Expression<Func<T>>` is a tree EF turns into SQL; `Func<T>` is already runnable code.

**Keyed DI?** Several implementations, picked by a key.

**TimeProvider?** Testable clock. Don’t use `DateTime.UtcNow` in domain logic.

**IExceptionHandler?** Built-in global exception handler.

**FrozenDictionary?** Build once, read many, never mutate.
