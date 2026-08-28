# Modular Monolith

Quick reference for interviews: what a modular monolith is, where it sits between a tangled monolith and microservices, and how Clean Architecture fits in.

## What is it?

A **modular monolith** is a single deployable application — one codebase, one build, one deployment — but internally split into clearly separated modules with strict boundaries (e.g. Orders, Users, Payments).

Each module has its own logic and exposes only a defined interface/API to other modules. Other modules must not reach into its internals directly.

## Where it sits

```
Tangled monolith          Modular monolith              Microservices
(everything coupled)  →   (one deploy, clean modules)  →  (many deploys, network calls)
```

| | Tangled monolith | Modular monolith | Microservices |
|---|---|---|---|
| Deployments | One | One | Many |
| Coupling | High — any class can call any class | Low — modules talk through interfaces | Lowest — separate services |
| Communication | In-process | In-process | Network (HTTP, messaging) |
| Operational complexity | Low | Low | High |
| Split later? | Hard | Easier — boundaries already exist | Already split |

**One-liner:** "A modular monolith is one deployable app, but internally split into independent modules with strict boundaries — easier to maintain than a tangled monolith, without the operational overhead of microservices."

## Key points

- Each module exposes a **public API** — other modules use only that, not internal classes or repositories.
- Modules ideally **don't share database tables directly** — they communicate through interfaces or events (even if it's one physical database).
- You get **one deployment** — no network calls between modules, easy local dev and testing.
- Boundaries are clean enough that a module **could become a microservice later** with minimal changes to callers.

## Clean Architecture (brief)

A way of organizing code in **layers**, where dependencies only point **inward** toward business logic — never outward.

```
┌─────────────────────────────────────┐
│  Frameworks & Drivers (outer)       │  DB, web framework, external APIs, UI
│  ┌───────────────────────────────┐  │
│  │  Interface Adapters           │  │  Controllers, gateways, presenters
│  │  ┌─────────────────────────┐  │  │
│  │  │  Use Cases / Application │  │  │  PlaceOrder, RegisterUser
│  │  │  ┌───────────────────┐  │  │  │
│  │  │  │  Domain / Entities │  │  │  │  Core business rules (center)
│  │  │  └───────────────────┘  │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
         dependencies point inward →
```

| Layer | Role |
|-------|------|
| **Domain / Entities** | Core business rules and models. No dependency on DB, frameworks, or UI. |
| **Application / Use cases** | Orchestrates business logic. Depends only on domain. |
| **Interface adapters** | Translates between use cases and the outside world (HTTP → use case input). |
| **Frameworks & drivers** | Database, ASP.NET, external APIs — replaceable details. |

**Core rule:** outer layers depend on inner layers, never the reverse. Business logic doesn't know if you use PostgreSQL, MongoDB, REST, or GraphQL.

**Why it matters:**
- Swap database or framework without touching business logic.
- Unit test business logic without a database or web server.
- Keep business rules free of framework-specific code.

**One-liner:** "Clean Architecture puts business logic in a core with no dependency on frameworks or databases — everything else depends inward on that core, making logic testable and framework-independent."

## How they relate

You can combine them: a **modular monolith** where each module internally follows **Clean Architecture**. That's a common, strong pattern in real systems.

---

## Example: e-commerce app

Three modules — **Orders**, **Users**, **Payments**. One deployable app, one codebase — but separated internally.

### Folder structure

```
/src
  /Orders
    /Domain
      Order.cs
      IOrderRepository.cs
    /Application
      PlaceOrderUseCase.cs
    /Infrastructure
      SqlOrderRepository.cs
    OrdersModuleApi.cs          ← ONLY public entry point for this module

  /Users
    /Domain
      User.cs
    /Application
      GetUserUseCase.cs
    /Infrastructure
      SqlUserRepository.cs
    UsersModuleApi.cs

  /Payments
    /Domain
      Payment.cs
    /Application
      ChargePaymentUseCase.cs
    PaymentsModuleApi.cs

  Program.cs                    ← wires everything together; one app, one deploy
```

### The key rule in code

Orders must **not** reach into another module's internals:

```csharp
// ❌ BAD — reaching directly into another module's data
var user = _db.Users.FirstOrDefault(u => u.Id == userId);
```

Instead, go through the other module's public API:

```csharp
// ✅ GOOD — Orders talks to Users only through its public interface
public class PlaceOrderUseCase
{
    private readonly IUsersModuleApi _usersApi;
    private readonly IPaymentsModuleApi _paymentsApi;
    private readonly IOrderRepository _orderRepository;

    public void Execute(PlaceOrderRequest request)
    {
        var user = _usersApi.GetUserById(request.UserId);
        if (user == null) throw new Exception("User not found");

        var paymentResult = _paymentsApi.Charge(user.Id, request.Amount);

        if (paymentResult.Success)
        {
            var order = new Order(user.Id, request.Items);
            _orderRepository.Save(order);
        }
    }
}
```

`UsersModuleApi` and `PaymentsModuleApi` are contracts — mini public APIs, even though everything runs in the same process and deployment:

```csharp
public interface IUsersModuleApi
{
    UserDto GetUserById(Guid id);
}
```

### What this buys you

- Orders can't accidentally break Users internals — it only sees what `UsersModuleApi` exposes.
- Each module can have its own tables/schema (even on one physical database) — no hidden coupling through shared tables.
- If Users later becomes its own microservice, swap `UsersModuleApi`'s implementation from in-process call to HTTP — Orders barely changes, because it never talked to Users directly.

**Interview one-liner:** "In a modular monolith, each module — Orders, Users, Payments — only talks to others through a defined interface, never by touching internal classes or database tables. It's still one deployable app, but boundaries are enforced in code, so it stays maintainable and can be split into services later if needed."
