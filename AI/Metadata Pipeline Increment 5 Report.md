---
type: implementation-report
status: complete
area: metadata
increment: 5
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Increment 5 Report

## Outcome

Metadata Increment 5 adds an atomic single-game coordinator that retrieves
ordered provider snapshots, merges them into staged metadata, persists a staged
game collection, and updates the live game only after persistence succeeds.

## Source changes

### Created

- `Services/IGameLibraryPersistence.cs`
- `Services/Metadata/MetadataRefreshCoordinator.cs`
- `Services/Metadata/MetadataRefreshResult.cs`
- `Services/Metadata/MetadataRefreshStatus.cs`
- `NovaLauncher.Tests/Metadata/MetadataRefreshCoordinatorTests.cs`
- `docs/MetadataRefresh.md`

### Modified

- `Services/GameLibraryService.cs`
- `Services/Metadata/MetadataProgressStage.cs`
- `Core/Bootstrap/AppBootstrapper.cs`
- `docs/MetadataMergePolicy.md`
- `docs/MetadataProviders.md`
- `docs/Architecture.md`
- `docs/Changelog.md`

## Implemented behavior

- Refresh operates on exactly one active game.
- The target must appear by reference once and have a unique ID in the supplied
  collection.
- Retrieval remains read-only.
- Merge runs against a cloned `LibraryItem`.
- No accepted fields returns `NoChanges` without saving.
- A staged `Game` is supplied to whole-library persistence.
- The live `Game.Metadata` is replaced only after a successful save.
- A false save result or thrown persistence exception returns
  `PersistenceFailed` and leaves the live target unchanged.
- Caller cancellation propagates during retrieval and before persistence.
- Progress includes merging, saving, refresh completion, and persistence
  failure.
- Result invariants prevent inconsistent status, merge, and failure
  combinations.

## Verification

- Debug solution build: succeeded.
- Debug tests: 86 passed, 0 failed.
- Release solution build: succeeded.
- Release tests: 86 passed, 0 failed.
- Compiler result: 0 errors and 4 existing nullable warnings in
  `CinematicHero.axaml.cs`.
- Vault link audit: 88 Markdown notes checked, 0 unresolved internal links.

## Documentation updated

- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
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

- The coordinator is registered but is not invoked by the UI.
- `GameLibraryService.SaveGames` is synchronous and writes the complete game
  collection.
- Cancellation can prevent persistence before it starts, but cannot interrupt a
  synchronous save safely once it begins.
- Whole-library persistence can normalize non-target metadata using the
  existing save behavior.
- Collection membership is validated before retrieval and again before save,
  but the caller remains responsible for UI-thread collection ownership.
- Metadata cache expiration, provider retries, rate limiting, metadata UI, and
  bulk refresh remain outside this increment.
- The repository contains prior uncommitted artwork and metadata work; this
  increment preserves that existing dirty-worktree state.

## Recommended next step

Implement metadata cache expiration as a separate provider-neutral layer.
Define cache keys, freshness timestamps, stale fallback behavior, explicit
refresh bypass, and cleanup ownership before adding a UI refresh command.

## Related

- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
