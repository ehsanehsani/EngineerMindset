<!-- Test file: Farsi (RTL) + English bilingual Bounded Context.
     Open on GitHub to check RTL rendering. Safe to delete later. -->

<div dir="rtl" lang="fa">

# Bounded Context — تست فارسی / انگلیسی (RTL)

> این فایل فقط برای تست نمایش راست‌به‌چپ در GitHub است. محتوای اصلی انگلیسی در `Modular-Monolith.md` است.

## مرز ماژول‌ها را چطور تعیین می‌کنی؟

سوال کلاسیک مصاحبه: *«چه چیزی مشخص می‌کند یک ماژول کجا تمام می‌شود و ماژول بعدی از کجا شروع می‌شود؟»*

پاسخ مورد انتظار معمولاً **Bounded Context** از دنیای Domain-Driven Design (DDD) است — نه لایه‌های تکنیکی (Controller / Service / Repository) و نه «هر Entity = یک ماژول».

> اگر کسی چیزی شبیه «scope entity» بگوید، اغلب منظورش همان **Bounded Context** است (یا محدودهٔ یک Entity / Aggregate داخل یک Context).

### Bounded Context چیست؟

**Bounded Context** یک مرز مفهومی در دامنهٔ کسب‌وکار است. داخل این مرز:
- یک **زبان مشترک** وجود دارد (Ubiquitous Language)
- یک **مدل منسجم** وجود دارد

بیرون از این مرز، *همان کلمه* ممکن است معنی کاملاً متفاوتی داشته باشد.

**مثال کلاسیک مصاحبه — «Customer»:**

| Context | معنی Customer |
|---------|----------------|
| **Sales** | CreditLimit، DiscountTier، SalesRep |
| **Shipping** | DeliveryAddress، PreferredCarrier |
| **Support** | TicketHistory، SLA Level |

اگر این‌ها یک God Entity مشترک باشند، هر تغییر کوچک Sales و Shipping و Support را به هم قفل می‌کند. پس هر ماژول **مدل خودش** از Customer را داخل Bounded Context خودش نگه می‌دارد — همان نام، معنی و قوانین متفاوت.

**قاعدهٔ سرانگشتی:** *«آیا این دو مفهوم در بخش‌های مختلف کسب‌وکار، قوانین و رفتار متفاوتی دارند؟»* اگر بله → Bounded Context / ماژول جدا، حتی اگر اسم‌ها یکی باشد.

### چرا تفکیک فقط بر اساس Entity اشتباه است؟

اگر فقط بر اساس Entity ماژول‌بندی کنی («ماژول Customer»، «ماژول Order» به‌عنوان مدل مشترک سراسری)، به آنتی‌پترن **shared / God Entity** می‌رسی: همه به یک مدل وابسته‌اند و استقلال ماژول‌ها از بین می‌رود.

Entityها *داخل* Bounded Context زندگی می‌کنند. مرز، خود Context است — نه نام Entity.

### معیارهای کمکی (اشارهٔ کوتاه)

| معیار | معنی |
|-------|------|
| **High cohesion, low coupling** | چیزهایی که با هم تغییر می‌کنند در یک ماژول بمانند؛ وابستگی بین ماژول‌ها کم باشد. |
| **Single Responsibility در سطح ماژول** | یک دلیل بیزینسی برای تغییر. اگر یک rule دو ماژول را مجبور به تغییر کند، مرز اشتباه است. |
| **مالکیت تیمی (Conway's Law)** | ساختار سیستم آینهٔ ساختار تیم است — مثلاً تیم Billing → ماژول Billing. |
| **قابلیت استخراج به Microservice** | تست ذهنی: *«فردا می‌توانیم این را جدا کنیم؟»* اگر همیشه به جدول‌های ماژول دیگر query بزند، مرز ضعیف است. |
| **عدم اشتراک مستقیم جدول‌ها** | هر ماژول schema خودش را دارد؛ بقیه فقط از طریق interface / API یا event حرف می‌زنند. |

**جملهٔ یک‌خطی مصاحبه:** «ماژول‌ها را با Bounded Context از DDD جدا می‌کنیم — هر ماژول زبان و مدل خودش را دارد. فقط با نام Entity جدا نمی‌کنیم؛ کلمه‌ای مثل Customer در Sales و Shipping معنی متفاوتی دارد. کمک‌کننده‌ها: cohesion بالا / coupling پایین، SRP در سطح ماژول، و مالکیت schema اختصاصی هر ماژول.»

</div>

---

<div dir="ltr" lang="en">

# Bounded Context — English (LTR)

> Same section as in `Modular-Monolith.md`, kept here for side-by-side comparison with the Farsi RTL block above.

## How do you decide module boundaries?

Classic interview follow-up: *"What defines where one module ends and another begins?"*

Expected answer: **Bounded Context** (DDD) — not technical layers, and not "one Entity = one module."

> "Scope entity" usually means **Bounded Context** (or the scope of an Entity/Aggregate inside a context).

### What is a Bounded Context?

A conceptual boundary in the business domain. Inside it:
- one **shared language** (Ubiquitous Language)
- one **coherent model**

Outside it, the *same word* can mean something different.

**Classic example — "Customer":**

| Context | What "Customer" means |
|---------|------------------------|
| **Sales** | CreditLimit, DiscountTier, SalesRep |
| **Shipping** | DeliveryAddress, PreferredCarrier |
| **Support** | TicketHistory, SLA Level |

Shared God Entity → coupling. Each module keeps **its own** Customer model.

**Rule of thumb:** Different rules/behavior in different parts of the business → separate Bounded Contexts, even if names match.

### Why "split by Entity" is wrong

Leads to shared / God Entity anti-pattern. Entities live *inside* a Bounded Context; the context is the boundary.

### Supporting criteria

| Criterion | Meaning |
|-----------|---------|
| **High cohesion, low coupling** | Change together → same module; minimize cross-module deps. |
| **Single Responsibility (module)** | One business reason to change. |
| **Team ownership (Conway's Law)** | Billing team → Billing module. |
| **Potential independent deployability** | Could we extract this tomorrow? |
| **No shared tables** | Own schema; talk via API/events only. |

**One-liner:** "We split by Bounded Context from DDD — each module owns its language and model, not by Entity name alone."

</div>
