---
type: service
status: active
priority: critical
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Service

## Responsibility

Coordinate normalized game metadata through reviewed first-party providers.
Plugins are not part of the product plan.

## Current source implementation

The active Increment 4 source uses `GameEnrichmentService`, ordered
`IMetadataProvider` implementations, `MetadataMerger`, bounded `ProviderCache`
instances, and `LibraryCoordinator.ApplyEnrichmentAsync`. Steam provider JSON is
bounded to 2 MiB and depth 32; text/list/rating values are normalized and bounded
before persistence. Manual provenance always wins. Provider, cache, and
persistence failures return typed outcomes and never partially publish live
state.

## Implemented foundation

- `GameMetadata` is the provider-neutral descriptive metadata shape.
- `LibraryItem` is the long-term canonical owner.
- The active `Game` model carries the same metadata type through the migration.
- `GameLibraryItemAdapter` is the explicit compatibility boundary.
- General game metadata remains separate from asset-folder `AssetMetadata`.
- The versioned library document persists normalized metadata.

## Provider orchestration

- `MetadataRequest` isolates providers from mutable library state.
- `MetadataSnapshot` carries one normalized, unmerged provider response.
- `MetadataProviderResult` records success, no match, or typed failure.
- `MetadataProviderManager` filters and orders eligible providers.
- `MetadataService` queries providers without mutating or persisting metadata.
- One provider failure is logged and does not stop later providers.
- Caller cancellation propagates immediately.
- Progress reports retrieval, provider outcomes, and completion.
- `SteamMetadataProvider` is registered at priority 100.

## Steam provider

- Requires a Steam source and positive numeric Steam app ID.
- Uses `ISteamStoreMetadataClient` to isolate storefront HTTP and JSON.
- Normalizes descriptions, developers, publishers, genres, release date, and
  explicitly scaled Metacritic score.
- Returns no match for missing or empty data.
- Preserves caller cancellation.
- Returns an unmerged snapshot without modifying the library.
- Uses the public storefront `appdetails` endpoint, which is not part of the
  documented partner Web API and remains a compatibility risk.

## Merge and override policy

- `MetadataMerger` applies the first valid ordered snapshot value per field.
- Lower-priority providers fill higher-priority gaps.
- Missing and invalid values preserve last-known-good metadata.
- `MetadataFieldProvenance` records accepted provider source and time.
- Manual provenance blocks provider replacement.
- `MetadataOverrideService` marks or clears manual protection without deleting
  values.
- Accepted mutable values are deep-copied.
- The merger is invoked explicitly by the refresh coordinator, not directly by
  retrieval or UI code.

## Refresh coordination

- `MetadataRefreshCoordinator` refreshes one active game.
- Retrieval and merge run against a staged `LibraryItem`.
- `IGameLibraryPersistence` isolates whole-library persistence.
- No accepted fields means no save.
- The live `Game.Metadata` changes only after a successful save.
- False-return and thrown persistence failures leave the live game unchanged.
- Cancellation propagates during retrieval and before persistence.
- Progress covers merge, save, completion, and persistence failure.
- The coordinator is invoked through the active Game Details child view model.

## Cache policy

- `MetadataCache` stores successful normalized snapshots in memory.
- Fresh entries bypass provider retrieval.
- Stale entries trigger live retrieval and serve only as no-success fallback.
- Forced refresh bypasses reads and stale fallback.
- Successful live results replace cache data.
- Empty and failed results are not cached.
- Snapshots are deep-copied on store and lookup.
- Lazy cleanup removes expired entries and enforces an LRU entry bound.
- Validated defaults are 24-hour freshness, 7-day stale retention, 6-hour
  cleanup interval, and 1,000 entries.

## Manual editing

- `IMetadataEditCoordinator` isolates edit UI from persistence.
- Draft values are normalized and compared field by field.
- Changed fields receive manual provenance.
- A staged whole-library copy is saved before the live metadata changes.
- Failed saves leave the live game unchanged.
- Clearing manual protection retains the value and is also atomic.

## Game Details integration

- `IMetadataRefreshCoordinator` is the UI-facing refresh contract.
- `GameDetailsViewModel` owns metadata projections and refresh-operation state.
- The main view model synchronizes selected game and active library collection.
- Normal and forced refresh, cancellation, progress, overlap prevention, and
  typed outcome wording are tested.
- Main-window and coordinator persistence share one DI-managed
  `GameLibraryService`.
- Game Details binds read-only metadata, empty states, normal and forced
  refresh, cancel, progress, source, and outcomes through the child state.
- Game Details follows active theme tokens and supports compact/wide layouts.
- Game Details binds validated manual editing and field-source controls.

## Deferred

- Persistent metadata cache
- Bulk refresh

## Related

- [[Product/Epics/Metadata|Metadata epic]]
- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-009 Metadata Cache Freshness and Stale Fallback|ADR-009 Metadata Cache Freshness and Stale Fallback]]
- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Decisions/ADR-010 Game Details State Ownership|ADR-010 Game Details State Ownership]]
- [[Decisions/ADR-011 Atomic Manual Metadata Editing|ADR-011 Atomic Manual Metadata Editing]]
