---
type: report
status: complete
scope: Artwork Hardening Increment 2
created: 2026-07-29
updated: 2026-07-29
---

# Artwork Hardening Increment 2 Report

## Outcome

Increment 2 is complete. The Increment 1 retry and typed-exception foundation
is now active at SteamGridDB lookup and artwork-download boundaries. Existing
Steam provider fallback, cache-hit behavior, null-result failure behavior, and
caller cancellation semantics are preserved.

## Runtime behavior

- Retry network failures without an HTTP status.
- Retry non-caller timeouts.
- Retry HTTP 408, HTTP 429, and HTTP 5xx responses.
- Do not retry permanent HTTP responses such as 401 and 404.
- Do not retry invalid content types or local file/permission failures.
- Propagate caller cancellation immediately.
- Continue to Steam after SteamGridDB retries are exhausted.
- Return no cache path after download retries are exhausted, as before.
- Log cache hits, candidate counts, download outcomes, retries, and final
  failures through structured `ILogger` events.
- Attach operation, provider, game, and artwork-type context to final failures
  with `ArtworkException`.

## Source changes

### Created

- `NovaLauncher.Tests/Artwork/ArtworkCacheTests.cs`

### Modified

- `NovaLauncher/Services/Artwork/ArtworkCache.cs`
- `NovaLauncher/Services/Artwork/SteamGridDbArtworkProvider.cs`
- `NovaLauncher.Tests/Artwork/SteamGridDbArtworkProviderTests.cs`
- `NovaLauncher/docs/ArtworkHardening.md`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`
- `NovaLauncher/docs/CodeReview.md`
- `NovaLauncher/docs/SteamGridDB.md`

## Vault changes

### Created

- `AI/Artwork Hardening Increment 2 Report.md`

### Modified

- `AI/AI Context.md`
- `Dashboard/Development Dashboard.md`
- `Decisions/ADR-002 Artwork Resilience Policy.md`
- `Engineering/Architecture/Artwork System.md`
- `Engineering/Services/Artwork Service.md`
- `Planning/Releases/Changelog.md`
- `Planning/Roadmap/Master Roadmap.md`
- `Planning/Sprints/Current Sprint.md`
- `Planning/Sprints/Sprint Board.md`
- `Product/Features/Artwork System Hardening.md`
- `Product/Features/SteamGridDB Artwork Provider.md`

## Verification

- Debug test suite: 23 passed, 0 failed
- Full serial Release rebuild: succeeded, 0 errors
- Release test suite: 23 passed, 0 failed
- Application configuration JSON parse: passed
- Git whitespace check: passed
- Vault link audit: 68 notes checked, 0 unresolved wikilinks

The Release rebuild reports four existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. They
are outside this increment and were not modified.

Focused coverage verifies:

- SteamGridDB recovery after two transient failures
- SteamGridDB retry exhaustion and fallback-compatible empty results
- no SteamGridDB retry for HTTP 401
- immediate caller cancellation with no provider request
- artwork download recovery after HTTP 503 and HTTP 429
- no artwork download retry for HTTP 404

## Increment boundary

The following remain intentionally unimplemented:

- cache expiration enforcement
- maximum cache-size enforcement
- scheduled cache cleanup
- stale `.download` cleanup
- placeholder artwork
- service-to-UI progress reporting
- UI cancellation controls and token ownership
- retrying another candidate after post-download image validation fails
- honoring provider `Retry-After` response headers

## Remaining risks

- A transient outage can produce up to three requests per operation, increasing
  latency and provider usage.
- Retry delay currently follows local exponential-backoff configuration and
  does not honor `Retry-After`.
- Image validation still occurs after caching, so a downloaded invalid image
  can prevent trying the next candidate in the current service flow.

## Commit readiness

The Increment 2 paths above are independently scoped and verified. The source
repository still contains earlier uncommitted provider, Increment 1, and
repository-cleanup work. A focused commit should stage the listed Increment 2
source paths deliberately rather than staging the entire working tree.

## Recommended next step

Implement the cache-lifecycle increment using the existing expiration,
maximum-size, cleanup-interval, and stale temporary-file settings.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
