<div dir="rtl" lang="fa">

# معماری هگزاگونال (Hexagonal / Ports & Adapters) — فارسی / انگلیسی

## اصلاً چه مشکلی را حل می‌کند؟

فرض کن فروشگاه آنلاین داری:

کاربر → API → ثبت سفارش → پرداخت → دیتابیس → ایمیل

در پروژهٔ معمولی، منطق کسب‌وکار کم‌کم به ابزارهای اطرافش وابسته می‌شود:

```
OrderService
 ├── PostgreSQL
 ├── Stripe
 ├── Email
 ├── Redis
 └── Kafka
```

اگر فردا بخواهی Postgres را با Mongo عوض کنی، Stripe را با PayPal، یا REST را با GraphQL — ممکن است مجبور شوی **بخش بزرگی از business logic** را هم عوض کنی.

**قلب Hexagonal:**  
منطق اصلی سیستم نباید بداند بیرون از خودش چه تکنولوژی‌ای استفاده می‌شود.

قانونی مثل «سفارش بالای ۱۰۰ یورو → ۱۰٪ تخفیف» به Postgres، HTTP، Stripe یا Framework هیچ ربطی ندارد — پس باید مستقل بماند.

---

## شکل ذهنی: Port و Adapter

شش‌ضلعی بودن اسم مهم نیست. تصویر ذهنی این است:

```
        REST / Kafka / CLI
                │
             Adapter
                │
              Port
                ▼
          ┌───────────┐
          │   Core    │  ← Domain / Application
          │ (Business)│
          └───────────┘
                ▲
              Port
                │
             Adapter
                │
        Postgres / Stripe / Email
```

| مفهوم | معنی |
|--------|------|
| **Core** | قوانین و use caseهای کسب‌وکار — بدون وابستگی به تکنولوژی |
| **Port** | قرارداد (معمولاً interface) که Core تعریف می‌کند |
| **Adapter** | پیاده‌سازی واقعی بیرون — Stripe، Postgres، Controller و غیره |

### مثال ساده — پرداخت

Core فقط می‌گوید «چیزی می‌خواهم که پول بگیرد»:

```csharp
public interface IPaymentGateway   // ← Port
{
    PaymentResult Pay(Money amount);
}
```

بیرون، Adapter واقعی:

```csharp
public class StripePaymentGateway : IPaymentGateway  // ← Adapter
{
    public PaymentResult Pay(Money amount) { /* call Stripe */ }
}
```

فردا PayPalAdapter می‌گذاری؛ **CheckoutService عوض نمی‌شود** چون فقط به `IPaymentGateway` وابسته است.

### چند ورودی، یک use case

REST، Kafka و CLI همگی می‌توانند به یک کار وصل شوند: `CreateOrder`.

- REST Adapter → `CreateOrderUseCase.Execute(...)`
- Kafka Adapter → همان use case
- CLI Adapter → همان use case

هسته یکی است؛ فقط در ورودی فرق می‌کنند.

---

## اگر Hexagonal نباشد چه می‌شود؟

| مشکل | توضیح |
|------|--------|
| **تست سخت** | برای تست یک قانون ساده باید Postgres، Redis، Stripe و Email را راه بیندازی |
| **تغییر تکنولوژی دردناک** | عوض کردن DB یا payment به لایه‌های business سرایت می‌کند |
| **قاطی شدن با Framework** | Entityهای JPA/EF، annotationها و SQL داخل منطق کسب‌وکار می‌روند |

وابستگی برعکس می‌شود و درستش این است:

```
Framework / DB / Stripe / HTTP  ──►  Application
```

نه برعکس.

---

## مقایسه با Clean Architecture

هر دو تقریباً یک هدف دارند: **business logic از جزئیات تکنولوژیک مستقل باشد.**

| | Hexagonal | Clean Architecture |
|---|-----------|-------------------|
| ایدهٔ اصلی | Ports & Adapters | Dependency Rule + لایه‌ها |
| تمرکز | مرز سیستم و ارتباط با دنیا | جداسازی لایه‌ها؛ وابستگی فقط به سمت داخل |
| شکل معروف | شش‌ضلعی | حلقه‌های تو در تو |
| Domain مستقل؟ | بله | بله |
| تفاوت واقعی | مدل ذهنی Port/Adapter | مدل ذهنی Entities → Use Cases → Adapters |

در عمل خیلی وقت‌ها یک پروژه را هم Hexagonal و هم Clean صدا می‌زنند — ایده‌شان یکی است، تأکیدشان فرق دارد.

> Hexagonal یعنی «حتماً ۲۰ تا interface بنویس» نیست. اصل این است که Core به HTTP، DB، Stripe، Kafka و Framework وابسته نباشد.

---

## تشبیه ساده: رستوران

- **آشپزخانه = Core**
- سفارش از حضوری / Uber / وب / تلفن = **ورودی‌های Adapter**
- تحویل با پیک / مشتری = **خروجی‌های Adapter**

آشپزخانه برای هر کانال یک غذای متفاوت نمی‌پزد — همه به مفهوم مشترک **Order** تبدیل می‌شوند.

---

## مزیت واقعی (یک جمله)

هگزاگونال **هزینهٔ تغییر و تست business logic** را کم می‌کند، چون آن را از جزئیات بیرونی جدا می‌کند.

Postgres→Mongo، REST→GraphQL، Stripe→PayPal، DB واقعی→Fake — نباید باعث بازنویسی قوانین کسب‌وکار شوند.

---

## همیشه لازم نیست

برای CRUD ساده (User / Product / Category) بدون منطق پیچیده، ممکن است over-engineering شود (UseCase، Port، Adapter، Mapper زیاد).

**ارزش دارد وقتی:** منطق کسب‌وکار جدی است و integrationها زیاد عوض می‌شوند — Payment، Order، Booking، Banking، Logistics، E-commerce.

**هدف اصلی «کد قشنگ» نیست** — هدف این است که تغییرات بیرونی، هسته را خراب نکنند.

**جملهٔ مصاحبه:** «Hexagonal یعنی Ports و Adapters — هسته فقط قرارداد می‌بیند، نه Stripe یا Postgres. هدفش جدا کردن business logic از تکنولوژی است تا تست و تعویض ابزار ارزان شود. با Clean Architecture هم‌هدف است؛ Clean بیشتر روی لایه‌ها و Dependency Rule تأکید دارد.»

</div>

---

<div dir="ltr" lang="en">

# Hexagonal Architecture (Ports & Adapters) — English

## What problem does it solve?

In a typical shop flow (API → order → payment → DB → email), business logic often becomes tied to surrounding tools:

```
OrderService → PostgreSQL, Stripe, Email, Redis, Kafka...
```

Swap Postgres for Mongo, Stripe for PayPal, or REST for GraphQL — and you may have to rewrite large parts of the **business logic**.

**Core idea:** Business logic must not know which technologies sit outside it.

A rule like “orders over €100 get 10% off” has nothing to do with Postgres, HTTP, or Stripe — keep it independent.

---

## Mental model: Ports & Adapters

The hexagon shape is just a metaphor:

```
        REST / Kafka / CLI
                │
             Adapter
                │
              Port
                ▼
          ┌───────────┐
          │   Core    │  ← Domain / Application
          └───────────┘
                ▲
              Port
                │
             Adapter
                │
        Postgres / Stripe / Email
```

| Term | Meaning |
|------|---------|
| **Core** | Business rules and use cases — no tech dependencies |
| **Port** | Contract (usually an interface) owned by the core |
| **Adapter** | Real-world implementation — Stripe, Postgres, HTTP controller, etc. |

### Payment example

```csharp
public interface IPaymentGateway   // Port
{
    PaymentResult Pay(Money amount);
}

public class StripePaymentGateway : IPaymentGateway  // Adapter
{
    public PaymentResult Pay(Money amount) { /* call Stripe */ }
}
```

Tomorrow: `PayPalPaymentGateway`. `CheckoutService` only depends on `IPaymentGateway` — unchanged.

### Many inputs, one use case

REST, Kafka, and CLI can all call the same `CreateOrder` use case. Different adapters, one core.

---

## Without Hexagonal

| Problem | What happens |
|---------|----------------|
| **Hard to test** | Testing “10% off over €100” needs Postgres, Redis, Stripe mocks… |
| **Painful tech swaps** | DB/payment changes leak into business layers |
| **Framework pollution** | EF/JPA annotations and SQL mix into domain logic |

Dependencies should point **inward** toward the application — not from the core outward to frameworks.

---

## vs Clean Architecture

Same goal: **business logic independent of tech details.**

| | Hexagonal | Clean Architecture |
|---|-----------|-------------------|
| Main idea | Ports & Adapters | Dependency Rule + layers |
| Focus | System boundary & how the world connects | Layer separation; deps only inward |
| Famous shape | Hexagon | Concentric circles |
| Real difference | Port/Adapter mental model | Entities → Use Cases → Adapters |

In real projects people often mean almost the same thing by both names.

> Hexagonal is not “create 20 interfaces.” It means the core does not depend on HTTP, DB, Stripe, Kafka, or the framework.

---

## Restaurant analogy

Kitchen = **Core**. Orders from walk-in / Uber / web / phone = **inbound adapters**. Delivery via courier / pickup = **outbound adapters**. Kitchen doesn’t cook differently per channel — everything becomes an **Order**.

---

## Real benefit (one line)

Hexagonal **lowers the cost of changing and testing business logic** by isolating it from external details.

---

## When not to use it

Simple CRUD with little logic → can become over-engineering.

**Worth it when:** real business rules + changing integrations (payments, orders, booking, banking, logistics, e-commerce).

**Interview one-liner:** "Hexagonal is Ports and Adapters — the core depends on contracts, not on Stripe or Postgres. Same goal as Clean Architecture; Clean emphasizes layers and the Dependency Rule more explicitly."

</div>
