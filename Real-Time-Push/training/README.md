# Trainings

| Training | What you build | Topic |
|---|---|---|
| **SignalR** (this page, below) | LiveChat | real-time push, hubs, singleton, background service, Redis backplane |
| [**Observability with Aspire**](observability/README.md) | Coffee Shop (2 small services) | logs, traces, metrics, correlation ids, finding problems |

---

# Training: build a SignalR app yourself

A hands-on course. You build a tiny **LiveChat** app from an empty Visual Studio project, step by step, then scale it to two servers with Redis. It's **not** the roulette project: it's deliberately small, so you can focus on the concepts.

| Part | You build | You learn |
|---|---|---|
| [Part 1: SignalR basics](part-1-signalr-basics.md) | a chat server + web page + console client | hub, `invoke` / `on`, `Clients.All` / `Caller` / `Others` / `Client`, a **singleton** service, a **background service** with `IHubContext`, a .NET client |
| [Part 2: Scaling with Redis](part-2-scaling-with-redis.md) | the same app on **two servers** connected by Redis | the scaling problem, installing Redis, pub/sub by hand, the **backplane**, groups across servers, what the backplane does *not* fix |

Time: Part 1 ≈ 1.5–2 hours, Part 2 ≈ 1 hour.

## How to use this training

- **Type the code yourself.** Don't copy-paste whole files. Typing is how it sticks. (Copying a line you've already understood is fine.)
- Every step ends with a **✅ Checkpoint**: what you should see before moving on.
- 🧠 boxes explain **why**. Read them, they're the actual lesson.
- Each part ends with **self-check questions** (answers hidden; try first) and optional exercises.
- Stuck? Check the **Troubleshooting** section at the end of each part, or ask me.

## The app

```
┌──────────────────────────── LiveChat ────────────────────────────┐
│ Server time: 13:34:53 (2 online)      Online: alice, carol        │
│ [Your name   ] [Join]                                             │
│ [Message     ] [Send to all]  [Recipient ] [Send privately]       │
│ ┌───────────────────────────────────────────────────────────────┐ │
│ │ (private) carol: this is private                              │ │
│ │ carol: hello all                                              │ │
│ │ carol joined the chat                                         │ │
│ └───────────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────────┘
```

All the code in this training was built and tested before it was written down. The outputs you see in the checkpoints are real.
