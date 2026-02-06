# System Design: Rate Limiter

## What is a Rate Limiter?

A **Rate Limiter** limits the number of client requests allowed to be sent over a specified period. If the API request count exceeds the threshold, all excess calls are **blocked**.

### Examples

- A user can only post **10 times per minute**
- A user can only create **10 accounts per day** from the same IP
- An API allows **100 requests per minute** per API key

---

## Why Rate Limiting?

| Benefit                 | Description                                       |
| ----------------------- | ------------------------------------------------- |
| **Prevent DoS Attacks** | Block excess calls (intentional or unintentional) |
| **Reduce Costs**        | Limit 3rd-party API calls (pay-per-call)          |
| **Reduce Server Load**  | Filter requests from bots or misbehaving users    |
| **Fair Usage**          | Distribute resources fairly among users           |

---

## Where to Place It?

```
┌──────────┐     ┌────────────────────┐     ┌──────────┐
│  Client  │────▶│  Rate Limiter      │────▶│  Server  │
│          │     │  (Middleware)      │     │          │
└──────────┘     └─────────┬──────────┘     └──────────┘
                           │
                           ▼
                 ┌────────────────────┐
                 │   Redis Cache      │
                 └────────────────────┘
```

| Location        | Recommendation                    |
| --------------- | --------------------------------- |
| **Client-side** | ❌ Unreliable - can be forged     |
| **Server-side** | ⚠️ Requests still hit your server |
| **Middleware**  | ✅ Best - throttles before server |

---

## Algorithm 1: Token Bucket

**Used by**: Stripe, Amazon

```
┌─────────────────────────────────────┐
│          TOKEN BUCKET               │
│  ┌─────┬─────┬─────┬─────┬─────┐   │
│  │  🪙 │  🪙 │  🪙 │     │     │   │
│  └─────┴─────┴─────┴─────┴─────┘   │
│       Current: 3 tokens             │
│       Max Capacity: 5               │
│       Refill Rate: 1 token/sec      │
└─────────────────────────────────────┘
```

### Parameters

- **Bucket Size**: Maximum tokens allowed
- **Refill Rate**: Tokens added per second

### How It Works

1. Request arrives → Take a token
2. Token available → Process ✅
3. No token → Reject with **HTTP 429** ❌
4. Tokens refill at constant rate

### Example

```
Rule: 3 requests per user per minute

00:00 - Request 1 → Tokens: 3→2 ✅
00:10 - Request 2 → Tokens: 2→1 ✅
00:30 - Request 3 → Tokens: 1→0 ✅
00:55 - Request 4 → Tokens: 0   ❌ HTTP 429
01:00 - Bucket refills to 3 tokens
```

| ✅ Pros              | ❌ Cons                |
| -------------------- | ---------------------- |
| Memory efficient     | Two parameters to tune |
| Allows burst traffic |                        |
| Easy to implement    |                        |

---

## Algorithm 2: Leaky Bucket

```
     Incoming Requests
           │
           ▼
    ┌──────────────┐
    │    QUEUE     │ ← If full, requests "leak" (dropped)
    │ ┌──┬──┬──┬──┐│
    │ │R1│R2│R3│R4││
    │ └──┴──┴──┴──┘│
    └──────┬───────┘
           │ Fixed outflow rate
           ▼
      [ PROCESS ]
```

### Parameters

- **Bucket Size**: Queue capacity
- **Outflow Rate**: Requests processed per second

### How It Works

1. Request arrives → Add to queue
2. Queue full → Drop request
3. Process at fixed rate

| ✅ Pros            | ❌ Cons                  |
| ------------------ | ------------------------ |
| Smooths out bursts | Old requests fill queue  |
| Memory efficient   | Recent requests may wait |
| Stable outflow     |                          |

---

## Algorithm 3: Sliding Window Log

```
Window: 1 minute | Limit: 2 requests

1:00:01        1:00:30        1:00:50        1:01:40
   │              │              │              │
   ▼              ▼              ▼              ▼
[1:00:01]    [1:00:01,      [1:00:01,      [1:00:50,
              1:00:30]       1:00:30,       1:01:40]
                             1:00:50]
                                ↓
  ✅            ✅          ❌ (3>2)          ✅
```

### How It Works

1. Remove timestamps older than window
2. Add current timestamp to log
3. Log size ≤ limit → Allow ✅
4. Log size > limit → Reject ❌

**Storage**: Redis Sorted Sets

| ✅ Pros            | ❌ Cons          |
| ------------------ | ---------------- |
| Very accurate      | Memory intensive |
| No boundary issues |                  |

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   RATE LIMITER SYSTEM                   │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌────────────┐       ┌───────────────────────────┐    │
│  │  Config    │       │   Rate Limiter Middleware │    │
│  │  Store     │◀──────│                           │    │
│  │ (Rules DB) │       │  1. Fetch rules from cache│    │
│  └─────┬──────┘       │  2. Get counter/timestamp │    │
│        │              │  3. Check limit           │    │
│        │ pull         │  4. Allow or reject       │    │
│        ▼              └─────────────┬─────────────┘    │
│  ┌────────────┐                     │                  │
│  │  Workers   │                     │ read/write       │
│  └─────┬──────┘                     ▼                  │
│        │              ┌───────────────────────────┐    │
│        │ cache        │        Redis Cache        │    │
│        ▼              │  • Token counts           │    │
│  ┌────────────┐       │  • Timestamps             │    │
│  │Rules Cache │◀──────│  • Rate limiting rules    │    │
│  └────────────┘       └───────────────────────────┘    │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## Handling Rate Limited Requests

### Option 1: Reject Immediately

```http
HTTP/1.1 429 Too Many Requests
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1609459200
Retry-After: 60
```

### Option 2: Queue for Later

```
┌─────────┐     ┌───────────────┐     ┌─────────┐
│ Request │────▶│ Message Queue │────▶│ Process │
│(blocked)│     │  (RabbitMQ)   │     │  Later  │
└─────────┘     └───────────────┘     └─────────┘
```

---

## Distributed Challenges

### 1. Race Conditions

```
Thread 1: READ counter = 3
Thread 2: READ counter = 3
Thread 1: WRITE counter = 4
Thread 2: WRITE counter = 4  ← Should be 5!
```

**Solution**: Redis Lua Scripts (atomic operations)

```lua
local current = redis.call('GET', KEYS[1])
if current and tonumber(current) >= tonumber(ARGV[1]) then
    return 0  -- Rate limited
else
    redis.call('INCR', KEYS[1])
    return 1  -- Allowed
end
```

### 2. Multi-Server Synchronization

```
    ┌─────────────────┐
    │ Rate Limiter 1  │
    └────────┬────────┘
             │
             ▼
    ┌─────────────────┐
    │Centralized Redis│ ← Single source of truth
    └─────────────────┘
             ▲
             │
    ┌────────┴────────┐
    │ Rate Limiter 2  │
    └─────────────────┘
```

**Solution**: Centralized Redis (not sticky sessions)

---

## Response Headers

| Header                  | Description               |
| ----------------------- | ------------------------- |
| `X-RateLimit-Limit`     | Max requests per window   |
| `X-RateLimit-Remaining` | Requests left in window   |
| `X-RateLimit-Reset`     | When window resets (Unix) |
| `Retry-After`           | Seconds to wait           |

---

## Interview Questions to Ask

1. Client-side, server-side, or middleware?
2. Throttle by IP, user ID, or API key?
3. What scale? (startup vs. enterprise)
4. Distributed environment?
5. Separate service or in application?
6. Inform throttled users?

---

## Key Trade-offs

| Decision               | Trade-off                     |
| ---------------------- | ----------------------------- |
| Hard vs. Soft limits   | Protection vs. UX             |
| Token vs. Leaky Bucket | Burst traffic vs. Smooth rate |
| Local vs. Distributed  | Performance vs. Accuracy      |
| Reject vs. Queue       | Simplicity vs. Reliability    |

---

## Algorithm Selection Guide

| Use Case                | Best Algorithm        |
| ----------------------- | --------------------- |
| Allow traffic bursts    | Token Bucket          |
| Smooth, consistent rate | Leaky Bucket          |
| Precise counting        | Sliding Window        |
| Memory constrained      | Token or Leaky Bucket |

---

## References

- _System Design Interview_ - Alex Xu
- Stripe Engineering Blog: Rate Limiters
- Redis Sorted Sets & Lua Scripting
