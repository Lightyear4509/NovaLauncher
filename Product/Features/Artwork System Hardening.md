---
type: feature
status: implemented
priority: high
release: Alpha
owner: unassigned
progress: 100
related_epic: "[[Product/Epics/Artwork|Artwork]]"
created: 2026-07-29
updated: 2026-07-29
---

# Artwork System Hardening

## Goal

Make artwork retrieval observable, cancellable, resilient to transient
failures, and bounded in its use of local storage.

## Increment 1 — resilience foundation

- [x] Add validated retry and cache-lifecycle settings
- [x] Add cancellation-aware retry policy
- [x] Add typed artwork exception context
- [x] Add progress event contracts
- [x] Register the foundation through dependency injection
- [x] Add focused automated tests
- [x] Preserve existing runtime artwork behavior

## Increment 2 — provider and download resilience

- [x] Apply retry policy to SteamGridDB lookups
- [x] Apply retry policy to artwork downloads
- [x] Retry network failures, timeouts, HTTP 408/429, and HTTP 5xx
- [x] Keep permanent HTTP and invalid-content failures single-attempt
- [x] Preserve immediate caller cancellation
- [x] Preserve Steam fallback after SteamGridDB retry exhaustion
- [x] Add typed final-failure context
- [x] Add structured provider and download logging
- [x] Add focused integration tests

## Increment 3 — cache lifecycle

- [x] Enforce download-age cache expiration
- [x] Run lazy cleanup at the configured interval
- [x] Expose explicit cache cleanup with a result summary
- [x] Remove expired artwork
- [x] Remove stale `.download` files
- [x] Enforce the configured maximum cache size
- [x] Evict least-recently-used artwork first
- [x] Protect the newly downloaded file during size enforcement
- [x] Log cleanup results and non-fatal deletion failures
- [x] Add focused cache-lifecycle tests

## Increment 4 — placeholder artwork

- [x] Generate deterministic placeholders for every artwork type
- [x] Use type-appropriate cover and hero aspect ratios
- [x] Fall back for missing and unreadable displayed artwork
- [x] Keep placeholders out of the download cache and managed assets
- [x] Keep missing games eligible for later real artwork downloads
- [x] Preserve existing empty-state UI as a final fallback
- [x] Add codec and determinism tests

## Increment 5 — progress reporting

- [x] Report provider resolution and queries
- [x] Report cache hits and downloads
- [x] Report retry attempts with context
- [x] Report candidate failures and provider fallback
- [x] Report cache cleanup
- [x] Report validation and installation
- [x] Surface progress in Steam import and manual cover installation
- [x] Preserve providers that implement the original contract
- [x] Add focused and end-to-end progress tests

## Increment 6 — UI cancellation

- [x] Own one cancellation source per artwork operation
- [x] Propagate the token through Steam import and manual cover installation
- [x] Add a global status-bar cancel action
- [x] Disable conflicting artwork actions while active
- [x] Preserve completed changes on cooperative cancellation
- [x] Suppress stale progress after cancellation
- [x] Cancel active work during view-model disposal
- [x] Add controller and end-to-end cancellation tests

## Follow-up opportunities

- [ ] Retry the next candidate after post-download image validation fails
- [ ] Honor provider `Retry-After` headers
- [ ] Consider background cache cleanup independent of artwork access
- [ ] Run a live hardening smoke test

## Increment 1 defaults

- 3 total HTTP attempts
- 250 ms base delay, 2 second cap, 20% jitter
- 30-day cache expiration
- 1 GB maximum cache size
- 24-hour cleanup interval
- 24-hour temporary-file expiration

## Related

- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
- [[Product/Features/SteamGridDB Artwork Provider|SteamGridDB Artwork Provider]]
- [[Decisions/ADR-003 Placeholder Artwork Policy|ADR-003 Placeholder Artwork Policy]]
- [[Decisions/ADR-004 Artwork Progress Contract|ADR-004 Artwork Progress Contract]]
- [[Decisions/ADR-005 Artwork Cancellation Ownership|ADR-005 Artwork Cancellation Ownership]]
