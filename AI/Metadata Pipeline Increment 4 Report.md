---
type: implementation-report
status: complete
area: metadata
increment: 4
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Increment 4 Report

## Outcome

Metadata Increment 4 implements deterministic field-level merge policy,
accepted-source provenance, and non-destructive manual override protection.
The services are registered and tested but are not connected to an automatic
retrieve/merge/save workflow or metadata UI.

## Source changes

### Created

- `Domain/Metadata/MetadataField.cs`
- `Domain/Metadata/MetadataSourceKind.cs`
- `Domain/Metadata/MetadataFieldProvenance.cs`
- `Services/Metadata/MetadataMerger.cs`
- `Services/Metadata/MetadataMergeResult.cs`
- `Services/Metadata/MetadataOverrideService.cs`
- `NovaLauncher.Tests/Metadata/MetadataMergerTests.cs`
- `docs/MetadataMergePolicy.md`

### Modified

- `Domain/Metadata/GameMetadata.cs`
- `Infrastructure/Library/GameLibraryItemAdapter.cs`
- `Services/GameLibraryService.cs`
- `Core/Bootstrap/AppBootstrapper.cs`
- `NovaLauncher.Tests/Library/GameLibraryItemAdapterTests.cs`
- `NovaLauncher.Tests/Library/GameLibraryServiceTests.cs`
- `docs/MetadataProviders.md`
- `docs/Architecture.md`
- `docs/Changelog.md`

## Implemented policy

- Manual provenance has absolute precedence.
- Otherwise, the first valid value from ordered snapshots wins per field.
- Lower-priority snapshots fill fields omitted by higher-priority snapshots.
- Missing or invalid provider values preserve the last-known-good value.
- Accepted fields record provider name, provider item ID, and retrieval time.
- The aggregate refresh time advances only when provider data is accepted and
  never moves backward.
- Lists and ratings are deep-copied at the merge and adapter boundaries.
- Clearing manual protection does not delete or blank the current value.
- Provenance survives schema-versioned library serialization.

## Verification

- Debug solution build: succeeded.
- Debug tests: 80 passed, 0 failed.
- Release solution build: succeeded.
- Release tests: 80 passed, 0 failed.
- Compiler result: 0 errors and 4 existing nullable warnings in
  `CinematicHero.axaml.cs`.
- Vault link audit: 85 Markdown notes checked, 0 unresolved internal links.

## Documentation updated

- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
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

- No workflow currently invokes retrieve, merge, and save as one operation.
- No metadata UI performs a value edit and manual marking atomically.
- Clearing an override intentionally leaves the current value in place until a
  later valid provider value replaces it.
- Provider snapshots remain mutable compatibility objects; accepted collection
  and rating values must continue to be copied.
- Enum-keyed provenance is additive to the current persistence schema. Future
  field renames require an explicit migration.
- Metadata cache expiration, retries, rate limiting, and bulk refresh remain
  outside this increment.
- The repository contains prior uncommitted artwork and metadata work; this
  increment preserves that existing dirty-worktree state.

## Recommended next step

Implement a small refresh coordinator that retrieves ordered snapshots, invokes
the merger, and persists successful changes. Add focused atomicity and failure
tests. Define metadata cache expiration as a separate increment, and keep UI
and bulk refresh out of the coordinator increment.

## Related

- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
