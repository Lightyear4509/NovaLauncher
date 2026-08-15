---
type: report
status: complete
scope: Artwork Hardening Increment 3
created: 2026-07-29
updated: 2026-07-29
---

# Artwork Hardening Increment 3 Report

## Outcome

Increment 3 is complete. The artwork cache now enforces the lifecycle settings
introduced in Increment 1 while preserving Increment 2 retry behavior,
provider fallback, cache paths, and caller cancellation.

## Runtime behavior

- Treat cached artwork as expired after its configured download age.
- Redownload an expired requested entry.
- Keep fresh cache hits and update their access time for eviction ordering.
- Run cleanup lazily on artwork access, no more than once per configured
  cleanup interval.
- Expose `ArtworkCache.CleanupAsync` for explicit maintenance.
- Remove expired artwork and stale `.download` files.
- Evict least-recently-used artwork until the configured maximum size is met.
- Enforce the size limit after downloads while protecting the path returned to
  the current caller.
- Log cleanup summaries and non-fatal deletion failures.
- Return `ArtworkCacheCleanupResult` with deletion counts, freed bytes,
  remaining bytes, and failure count.

## Source changes

### Created

- `NovaLauncher/Services/Artwork/ArtworkCacheCleanupResult.cs`

### Modified

- `NovaLauncher/Services/Artwork/ArtworkCache.cs`
- `NovaLauncher/Services/Artwork/ArtworkOptions.cs`
- `NovaLauncher.Tests/Artwork/ArtworkCacheTests.cs`
- `NovaLauncher.Tests/Artwork/ArtworkOptionsTests.cs`
- `NovaLauncher/docs/ArtworkHardening.md`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`
- `NovaLauncher/docs/CodeReview.md`

## Vault changes

### Created

- `AI/Artwork Hardening Increment 3 Report.md`

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

- Debug test suite: 29 passed, 0 failed
- Full serial Release rebuild: succeeded, 0 errors
- Release test suite: 29 passed, 0 failed
- Application configuration JSON parse: passed
- Git whitespace check: passed
- Vault link audit: 69 notes checked, 0 unresolved wikilinks

The Release rebuild reports four existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. They
are outside this increment and were not modified.

Focused coverage verifies:

- fresh cache hits avoid network requests
- expired requested artwork is replaced
- expired artwork and stale temporary files are removed
- least-recently-used files are evicted to the size limit
- post-download enforcement preserves the returned path
- oversized cache configuration is rejected before byte conversion can overflow
- all Increment 1 and Increment 2 behavior remains covered

## Increment boundary

The following remain intentionally unimplemented:

- placeholder artwork
- service-to-UI progress reporting
- UI cancellation controls and token ownership
- retrying another candidate after post-download image validation fails
- honoring provider `Retry-After` response headers
- background cleanup independent of artwork access

## Remaining risks

- Cleanup uses synchronous filesystem enumeration within the calling
  operation. A cache containing an unusually large number of files may add
  latency to the first artwork request after the cleanup interval.
- A newly downloaded file larger than the entire configured limit is protected
  for its current caller and can temporarily leave the cache over limit.
- File access timestamps are used for eviction ordering and depend on the
  filesystem accepting metadata updates.

## Commit readiness

The Increment 3 paths above are independently scoped and verified. The source
repository still contains earlier uncommitted provider, Increment 1,
Increment 2, and repository-cleanup work. A focused commit should stage the
listed Increment 3 source paths deliberately rather than staging the entire
working tree.

## Recommended next step

Implement placeholder artwork as a separate increment. Keep service-to-UI
progress and UI cancellation as later, independently testable changes.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
