---
type: feature
status: complete
priority: critical
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Refresh Coordination

## Outcome

Metadata Increment 5 adds an atomic single-game retrieve, merge, and persist
workflow without adding metadata UI, caching, retries, or bulk refresh.

## Implemented

- `MetadataRefreshCoordinator`
- `MetadataRefreshResult`
- `MetadataRefreshStatus`
- `IGameLibraryPersistence`
- Staged `Game`/`LibraryItem` compatibility conversion
- No-save behavior when no fields are accepted
- Live metadata replacement only after successful persistence
- Typed false-return and thrown-exception persistence failures
- Cancellation propagation before persistence
- Merge, save, completion, and persistence-failure progress
- Dependency-injection registration
- Focused atomicity, failure, cancellation, progress, and validation tests

## Runtime boundary

The coordinator is available through dependency injection but is not connected
to the UI. `GameLibraryService.SaveGames` remains synchronous and writes the
complete collection.

## Not included

- Metadata refresh or editing UI
- Metadata cache and expiration
- Provider retry or rate limiting
- Bulk refresh
- Asynchronous persistence migration

## Related

- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
