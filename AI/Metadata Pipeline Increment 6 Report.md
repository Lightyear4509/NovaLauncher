---
type: implementation-report
status: complete
area: metadata
increment: 6
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Increment 6 Report

## Outcome

Metadata Increment 6 adds a provider-neutral in-memory snapshot cache with
validated freshness, stale fallback, explicit bypass, deep-copy isolation, and
bounded lazy cleanup.

## Source changes

### Created

- `Services/Metadata/MetadataOptions.cs`
- `Services/Metadata/MetadataCacheKey.cs`
- `Services/Metadata/MetadataCache.cs`
- `Services/Metadata/MetadataCacheLookup.cs`
- `Services/Metadata/MetadataCacheLookupStatus.cs`
- `Services/Metadata/MetadataCacheCleanupResult.cs`
- `Services/Metadata/MetadataRefreshSource.cs`
- `NovaLauncher.Tests/Metadata/MetadataCacheTests.cs`
- `docs/MetadataCache.md`

### Modified

- `Core/Bootstrap/NovaLauncherOptions.cs`
- `Core/Bootstrap/AppBootstrapper.cs`
- `Services/Metadata/MetadataRefreshCoordinator.cs`
- `Services/Metadata/MetadataRefreshResult.cs`
- `Services/Metadata/MetadataProgressStage.cs`
- `NovaLauncher.Tests/Metadata/MetadataRefreshCoordinatorTests.cs`
- `appsettings.json`
- `docs/MetadataRefresh.md`
- `docs/MetadataProviders.md`
- `docs/Architecture.md`
- `docs/Changelog.md`

## Implemented policy

- Fresh entries skip provider retrieval.
- Stale entries trigger live retrieval.
- Stale snapshots are used only when no live provider returns a successful
  snapshot.
- Entries beyond stale retention are removed and treated as misses.
- Explicit bypass skips cache reads and stale fallback.
- Successful normal or forced live retrieval replaces the cache entry.
- Empty and failed retrieval is never cached.
- Refresh results report live, fresh-cache, or stale-cache source.
- Live provider outcomes remain visible when stale fallback is used.
- Cached snapshots are deep-copied on store and every lookup.
- Cancellation is checked before live results enter the cache.
- Manual provenance remains authoritative during cached merges.
- Lazy cleanup removes expired entries and enforces least-recently-used
  capacity.

## Configuration

- Freshness: 1,440 minutes
- Stale retention: 7 days
- Cleanup interval: 6 hours
- Maximum entries: 1,000

Configuration is bound from `NovaLauncher:Metadata` and validated at startup.

## Verification

- Debug solution build: succeeded.
- Debug tests: 96 passed, 0 failed.
- Release solution build: succeeded.
- Release tests: 96 passed, 0 failed.
- Compiler result: 0 errors and 4 existing nullable warnings in
  `CinematicHero.axaml.cs`.
- Vault link audit: 91 Markdown notes checked, 0 unresolved internal links.

## Documentation updated

- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-009 Metadata Cache Freshness and Stale Fallback|ADR-009 Metadata Cache Freshness and Stale Fallback]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Product/Epics/Metadata|Metadata Epic]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Planning/Sprints/Sprint Board|Sprint Board]]
- [[Planning/Roadmap/Master Roadmap|Master Roadmap]]
- [[Planning/Releases/Changelog|Changelog]]
- [[Dashboard/Development Dashboard|Development Dashboard]]
- [[AI/AI Context|AI Context]]

## Boundaries and remaining risks

- Cache entries are process-local and disappear at application exit.
- Provider registration changes do not replace a fresh entry until it expires
  or the caller bypasses cache.
- Provider item IDs are preserved case-sensitively; source and platform are
  normalized case-insensitively.
- Cache cleanup is lazy and has no background timer.
- A fresh cache hit can still persist metadata because merge provenance is
  evaluated through the normal atomic refresh path.
- Metadata refresh remains disconnected from UI.
- Provider retries, rate limiting, persistent cache, and bulk refresh remain
  outside this increment.
- The repository contains prior uncommitted artwork and metadata work; this
  increment preserves that existing dirty-worktree state.

## Recommended next step

Design and implement a single-game metadata refresh UI with explicit force
refresh, cancellation, progress, and user-visible live/cache/no-change/failure
outcomes. Keep bulk refresh and provider resilience separate.

## Related

- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
