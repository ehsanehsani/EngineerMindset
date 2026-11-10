# Percentiles (p50, p90, p95, p99) explained simply

You'll see p50, p90, p95, p99 on every performance dashboard: Aspire, Grafana, Application Insights, Datadog. This page explains what they mean and why the **difference** between them matters, with real situations from software development.

---

## 1. What is a percentile?

Take **100 requests** to your API and **line them up from fastest to slowest**.

```
fastest                                                           slowest
  1   2   3  ...  50  ...  90  ...  95  ...  99  100
                  ↑         ↑        ↑        ↑
                 p50       p90      p95      p99
```

- **p50** = the duration of request number **50**. Half the requests were faster, half slower. Also called the **median**: the "typical" request.
- **p90** = request number **90**. 90% were faster, 10% slower.
- **p95** = request number **95**. 95% were faster, 5% slower.
- **p99** = request number **99**. 99% were faster, **1% were slower**.
- **max** = the single slowest request.

> **"p99 = 800 ms"** in plain words: *"99 out of 100 requests finished within 800 ms, and 1 out of 100 took longer."*

### Tiny example you can do by hand

10 requests, sorted (ms): `40, 42, 45, 45, 48, 50, 55, 60, 300, 2000`

| Measure | Value | What it tells you |
|---|---|---|
| **Average** | (sum 2,685) / 10 = **268 ms** | describes nobody: no request actually took ~268 ms |
| **p50** (the middle) | **~48 ms** | the typical user has a fast experience |
| **p90** | **300 ms** | 1 in 10 users waits 300 ms or more |
| **max** | **2,000 ms** | someone waited 2 seconds |

The average says "268 ms, okay-ish". The truth is "**most users are fast, but a few are very slow**". Only percentiles show that.

---

## 2. Why the average lies

The average mixes everyone together, so **a few very slow requests hide behind many fast ones**, or a few very slow ones make it look like everything is slow.

Two systems with **the same average (100 ms)**:

| | System A | System B |
|---|---|---|
| Average | 100 ms | 100 ms |
| p50 | 95 ms | 20 ms |
| p99 | 130 ms | **3,000 ms** |
| What users feel | everyone gets a steady 100 ms | most are super fast, but **1 in 100 waits 3 seconds** |

Same average, completely different user experience. That's why dashboards show percentiles.

---

## 3. What each percentile is good for

| Percentile | Think of it as | Use it for |
|---|---|---|
| **p50** | "the typical user" | is the app **normally** fast? |
| **p90 / p95** | "almost everyone" | a common target for **SLOs** (promises like "95% under 300 ms") |
| **p99** | "the unlucky 1 in 100" | finding **hidden problems** that only some requests hit |
| **p99.9** | "1 in 1,000" | very large or critical systems (payments, trading) |
| **max** | one single request | usually too noisy (one weird outlier); don't make decisions on it alone |

---

## 4. "It's only 1%, why should I care about p99?"

This is the most common question, and interviewers like it. Four reasons:

### Reason 1: 1% is a lot of people
1,000,000 requests per day × 1% = **10,000 slow requests every day**. Those are real people waiting.

### Reason 2: one page = many requests
A web page or app screen often makes **20 or more** requests (data, images, API calls). The chance that **at least one** of them is in the slow 1%:

```
1 − 0.99²⁰ ≈ 18%
```

So roughly **1 in 5 page loads** feels slow, even though "only 1% of requests" are slow. **p99 of requests ≈ what many users feel per page.**

### Reason 3: microservices multiply it
If your API calls 5 other services and waits for all of them, **the slowest one decides** the total time. Every service's p99 matters, and the more services you call, the more often you hit someone's slow tail.

### Reason 4: the slow ones are often your best customers
Who has the slowest requests? Often the users with **the most data**: the biggest shopping cart, the longest order history, the most active account. Those are frequently your most valuable users.

---

## 5. Reading the gap between p50 and p99

The **gap** tells you more than either number alone.

| What you see | What it means | Typical causes |
|---|---|---|
| p50 low, p99 low (close together) | ✅ fast and **consistent** | healthy |
| p50 high, p99 high (close together) | everything is slow, **all the time** | not enough CPU, slow code on the main path, too little capacity → profile it, scale up or out |
| p50 low, **p99 much higher** | ⚠️ **usually fine, sometimes very slow** | something only **some** requests hit: a slow query for certain inputs, a cache miss, GC pauses, lock waits, retries, a slow external call, one bad server |
| p50 stable, p99 **climbing over time** | ⚠️ getting worse at the edges | growing data, a memory leak (more GC), a queue building up, load approaching capacity |

> 💡 A big gap between p50 and p99 means "**find out what's different about the slow requests**". Traces are perfect for this: filter by duration and open the slowest ones.

---

## 6. Real scenarios

### Scenario 1: the coffee shop (from the training)
`http.server.request.duration` for coffeeshop: **p50 = 250 ms, p99 = 2.4 s**.
- p50 is low: most orders are lattes, which are fast.
- p99 is high: cappuccinos (slow barista) and mochas (retries).
- **Action:** open the slowest traces → they're all cappuccinos → the barista span is slow.

### Scenario 2: search in an online shop
**p50 = 80 ms, p99 = 4 s.**
- Most searches are simple ("shoes") and fast.
- The slow 1% are searches with **many filters** (size + colour + brand + price), which hit a query **without a good index**.
- **Action:** open the slow traces → the slow DB query → add an index. The p99 drops to 300 ms, and the p50 doesn't change.

### Scenario 3: "the deploy looks fine… or does it?"
After a release the **average and p50 are unchanged**, but **p99 doubled**.
- The new code added a database call **per order item** (an "N+1 query"). Small orders (most) barely notice, but big orders with 100 items now make 100 extra queries.
- **Lesson:** if you only watched p50 or the average, you'd have missed it. **Always compare p95/p99 before and after a deploy.**

### Scenario 4: a cache made the average better but the p99 worse
After adding a cache: **average went from 120 ms to 40 ms 🎉, but p99 went from 400 ms to 1.5 s 😬.**
- Cache **hits** are super fast, which pulls the average down.
- Cache **misses** now do extra work (check the cache, load from the DB, write to the cache), and every few minutes the cache expires for everyone at once, so many requests miss together.
- **Lesson:** an optimization can help the typical case and hurt the tail. Look at both.

### Scenario 5: real-time messages (e.g. live game or chat)
Measure "time from server send → client receive": **p50 = 30 ms, p99 = 900 ms.**
- Most messages arrive quickly. The slow 1% are clients on bad mobile networks, or moments when the server pauses for garbage collection.
- **Action:** check whether the p99 spikes line up with GC pauses (runtime metrics) or come from specific clients/regions.

### Scenario 6: one bad server
5 servers behind a load balancer: **p50 fine, p99 bad.** Split the metric by server (instance): **4 servers are fine, 1 has a p99 of 3 s** (a noisy neighbour, a bad disk, a stuck thread). 20% of the traffic hits it, so the overall p99 is bad.
- **Action:** take the bad instance out of rotation and replace it.

---

## 7. Percentiles in promises: SLI, SLO, SLA

| Term | Simple meaning | Example |
|---|---|---|
| **SLI** (indicator) | what you measure | p95 latency of `/orders` |
| **SLO** (objective) | your internal goal | "p95 of `/orders` < 300 ms, over 30 days" |
| **SLA** (agreement) | a promise to customers, with penalties | "99.9% of requests succeed within 1 s, or you get a refund" |

Percentiles are the natural language for these promises: "95% of requests under X" is a percentile.

---

## 8. Common mistakes

| Mistake | Why it's wrong | Do instead |
|---|---|---|
| Looking only at the average | hides the slow tail | look at p50 **and** p95/p99 |
| **Averaging percentiles** (e.g. the average of 5 servers' p99) | that isn't the overall p99; the math doesn't work that way | compute percentiles from the **combined** data (histograms make this possible) |
| Trusting p99 with very little traffic | with 20 requests, "p99" is basically the single slowest one | look at longer time windows, or at p90 |
| Panicking about **max** | one outlier (e.g. the first request after a restart) | watch p99/p99.9 trends instead |
| Celebrating a better p50 only | the tail might have got worse (scenario 4) | compare all percentiles before and after |

> 💡 **Why dashboards show "P50, P90, P99" from a histogram:** the app doesn't store every request's duration. It counts how many fell into **buckets** (0–5 ms, 5–10 ms, …, 1–2.5 s, …). The dashboard estimates percentiles from those buckets. So they're **close approximations**, which is good enough for decisions.

---

## 9. Interview questions

**1. What does p99 latency mean?**
99% of requests completed within that time; 1% took longer.

**2. Why use percentiles instead of the average?**
The average hides the distribution. A few very slow requests can hide behind many fast ones (or distort the number). Percentiles show both the typical experience (p50) and the slow tail (p95/p99).

**3. Why care about p99 if it's only 1% of requests?**
At scale, 1% is many people. One page makes many requests, so many users hit the tail (20 requests → ~18% of page loads). In microservices the slowest dependency decides the total. And slow requests often belong to the heaviest, most valuable users.

**4. p50 is 50 ms but p99 is 3 s. What do you do?**
Something affects only some requests. Find what's different about them: open the slowest traces, and split the metric by endpoint, server, customer type or input. Typical causes: a missing index for certain queries, cache misses, GC pauses, lock contention, retries, a slow dependency, one bad instance.

**5. After a deploy p50 is unchanged but p99 doubled. Explanation?**
A change that only affects some requests, e.g. an N+1 query that only hurts large orders, a new external call on some code path, or more allocations causing GC pauses. Compare traces of slow requests before and after.

**6. Can you average the p99 of several servers to get the overall p99?**
No. Percentiles can't be averaged. Combine the underlying data (histogram buckets) and compute the percentile from that.

**7. Which percentile would you use for an SLO?**
Usually p95 or p99, depending on how critical the service is (p99.9 for payments and similar), measured over a fixed window, e.g. "p95 < 300 ms over 30 days".

**8. What's the difference between latency and throughput?**
Latency = how long one request takes (shown as percentiles). Throughput = how many requests per second you handle. When a system nears its limit, throughput flattens and latency, especially the p99, shoots up.
