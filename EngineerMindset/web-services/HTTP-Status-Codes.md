# HTTP Status Codes (Interview Essentials)

Quick reference for the status codes most often asked in backend and API interviews.

## 2xx — Success

| Code | Name | Remember |
|------|------|----------|
| **200** | OK | Request succeeded. Typical for GET, PUT, PATCH. |
| **201** | Created | POST created a new resource. Often returns `Location` header. |
| **204** | No Content | Success with no response body. Common for DELETE. |

## 3xx — Redirection

| Code | Name | Remember |
|------|------|----------|
| **301** | Moved Permanently | Resource moved forever. Browsers/clients may cache the new URL. |
| **302** | Found | Temporary redirect. Client should keep using the original URL next time. |
| **304** | Not Modified | Cached version is still valid. Used with conditional requests (`If-None-Match`, `If-Modified-Since`). |
| **307** | Temporary Redirect | Like 302, but **must not change** the HTTP method (POST stays POST). |
| **308** | Permanent Redirect | Like 301, but **must not change** the HTTP method. |

## 4xx — Client Error

| Code | Name | Remember |
|------|------|----------|
| **400** | Bad Request | Malformed request — invalid JSON, missing required field, wrong type. |
| **401** | Unauthorized | **Not authenticated.** Missing or invalid credentials/token. |
| **403** | Forbidden | **Authenticated, but not allowed.** User is known; they lack permission. |
| **404** | Not Found | Resource does not exist (or you hide existence for security). |
| **409** | Conflict | State conflict — duplicate email, version mismatch, concurrent update. |
| **422** | Unprocessable Entity | Syntax is valid, but semantics fail — business rule violation. |
| **429** | Too Many Requests | Rate limit exceeded. Client should retry after backoff/`Retry-After`. |

## 5xx — Server Error

| Code | Name | Remember |
|------|------|----------|
| **500** | Internal Server Error | Unexpected server bug. Don't expose stack traces to clients. |
| **502** | Bad Gateway | Your server got a bad response from an upstream service (proxy/gateway). |
| **503** | Service Unavailable | Server temporarily down or overloaded — maintenance, throttling, dependency outage. |

## Interview favorites

### 301 vs 302

- **301** → permanent move (old URL retired; update bookmarks/links).
- **302** → temporary move (original URL may still be used later).

### 304 — caching

Client sends `If-None-Match: "<etag>"` (or `If-Modified-Since`). If nothing changed, server returns **304** with no body — client uses its cache.

### 401 vs 403

- **401** → "Who are you?" Fix: log in / send a valid token.
- **403** → "I know who you are, but you can't do this." Fix: different role or permission.

### 400 vs 422

- **400** → Request is broken (bad JSON, wrong format).
- **422** → Request is well-formed, but data fails validation or business rules.

### 404 vs 403

Sometimes return **403** instead of **404** when the user shouldn't know a resource exists (e.g. another user's private document).

### 500 vs 502 vs 503

- **500** — your application threw an unhandled exception.
- **502** — upstream (API, DB proxy, load balancer) returned garbage or failed.
- **503** — you're intentionally unavailable or can't handle load right now.
