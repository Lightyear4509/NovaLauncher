---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-002 Artwork Resilience Policy

## Context

Artwork retrieval crosses remote-provider, download, validation, cache, and
installation boundaries. Hardening these paths requires shared policy and
diagnostic contracts before behavior is changed.

## Decision

Use a validated `ArtworkOptions` object with these initial defaults:

- 3 total HTTP attempts
- exponential retry starting at 250 ms, capped at 2 seconds
- 20% jitter
- 30-day cache expiration
- 1 GB maximum cache size
- 24-hour cleanup interval
- 24-hour stale temporary-file expiration

Use `ArtworkRetryPolicy` for future transient-operation retries. Caller
cancellation must propagate immediately. Retry classification remains the
caller's responsibility so validation errors and other permanent failures are
not retried accidentally.

Use `ArtworkException` for operation context and `ArtworkProgress` for
provider/service/UI progress boundaries.

## Increment boundary

Increment 1 registers and tests the policy and contracts but does not apply
them to active runtime operations.

Increment 2 applies the policy to SteamGridDB lookup and artwork download
boundaries. Transient classification includes network failures, non-caller
timeouts, HTTP 408, HTTP 429, and HTTP 5xx. Permanent responses and invalid
content are not retried. Exhausted failures retain the established fallback
and null-result behavior.

Increment 3 activates cache lifecycle. Expiration uses file write time as the
download timestamp. Cache hits update access time for least-recently-used
eviction without extending expiration. Lazy cleanup runs at the configured
interval, removes expired artwork and stale temporary files, then enforces the
maximum size. Post-download enforcement protects the returned file.

## Consequences

- Later hardening changes share one configurable policy.
- Invalid configuration fails during application startup.
- Current SteamGridDB and Steam fallback behavior remains unchanged.
- Provider and download resilience is active as of Increment 2.
- Cache lifecycle is active as of Increment 3.
- A single protected download larger than the configured maximum may
  temporarily exceed the limit so the current operation can return a valid path.
- Placeholder and UI adoption remain deferred.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
