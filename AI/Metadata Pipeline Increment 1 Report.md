---
type: report
status: complete
scope: Metadata Pipeline Increment 1
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Increment 1 Report

## Outcome

Increment 1 is complete. NovaLauncher now has a normalized descriptive
metadata domain, an explicit compatibility boundary between its two library
models, and versioned, backward-compatible, recoverable active-library
persistence.

Existing launch, Steam import, artwork, favorites, playtime, save-folder, and
game-details behavior remains on the current `Game` UI path.

## Runtime behavior

- Write `games.json` as a version 1 document containing `schemaVersion` and
  `games`.
- Continue reading the existing root-array format without data loss.
- Normalize missing metadata objects and collections from older files.
- Write a temporary sibling file before replacing the primary.
- Preserve the previous valid primary as `games.json.bak`.
- Fall back to a valid backup when the primary is missing or unreadable.
- Surface recovery or unrecoverable-load warnings through application status.
- Preserve an unreadable primary as `games.json.invalid` when a later
  successful save replaces it.
- Reject unsupported future schema versions instead of silently downgrading
  them.

## Domain boundary

- `LibraryItem` is the long-term canonical library entity.
- `GameMetadata` owns provider-neutral descriptive fields.
- The active `Game` model carries the same `GameMetadata` type for
  compatibility.
- `GameLibraryItemAdapter` maps identity, installation, artwork, save activity,
  playtime, source, and descriptive metadata in both directions.
- Adapter metadata collections are cloned so changes do not alias across
  models.
- Asset-folder `AssetMetadata` remains a separate filesystem inventory.

## Source changes

### Created

- `NovaLauncher/Domain/Metadata/GameMetadata.cs`
- `NovaLauncher/Infrastructure/Library/GameLibraryItemAdapter.cs`
- `NovaLauncher.Tests/Library/GameLibraryServiceTests.cs`
- `NovaLauncher.Tests/Library/GameLibraryItemAdapterTests.cs`
- `NovaLauncher/docs/MetadataFoundation.md`

### Modified

- `NovaLauncher/Domain/Library/LibraryItem.cs`
- `NovaLauncher/Domain/Library/SaveProfile.cs`
- `NovaLauncher/Models/Game.cs`
- `NovaLauncher/Services/GameLibraryService.cs`
- `NovaLauncher/ViewModels/MainWindowViewModel.cs`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`

## Vault changes

### Created

- `Decisions/ADR-006 Metadata Domain and Persistence Boundary.md`
- `Product/Features/Metadata Pipeline Foundation.md`
- `AI/Metadata Pipeline Increment 1 Report.md`

### Modified

- `AI/AI Context.md`
- `Dashboard/Development Dashboard.md`
- `Engineering/Services/Library Service.md`
- `Engineering/Services/Metadata Service.md`
- `Planning/Releases/Changelog.md`
- `Planning/Roadmap/Master Roadmap.md`
- `Planning/Sprints/Current Sprint.md`
- `Planning/Sprints/Sprint Board.md`
- `Product/Epics/Metadata.md`

## Verification

- Early Debug solution build: succeeded
- Debug test suite: 47 passed, 0 failed
- Release solution build: succeeded, 0 errors
- Release test suite: 47 passed, 0 failed
- Git whitespace check: passed
- Vault link audit: 78 notes checked, 0 unresolved wikilinks

The builds report four pre-existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. That
file is outside this increment and was not modified for metadata work.

Focused coverage verifies:

- versioned document serialization
- descriptive metadata round-trip
- legacy root-array compatibility
- valid-backup recovery
- previous-valid-document preservation
- explicit unrecoverable-load reporting
- bidirectional compatibility mapping
- non-aliased metadata collections

## Increment boundary

The following are not implemented:

- metadata provider contracts
- provider manager or external API calls
- Steam metadata retrieval
- provider snapshots or merge precedence
- manual overrides
- metadata cache or expiration
- metadata UI
- bulk refresh

## Remaining risks

- `Game` and `LibraryItem` remain parallel models until the staged UI migration
  is complete.
- The active `GameLibraryService` remains synchronous and is constructed by
  `MainWindowViewModel`; moving persistence fully behind the domain repository
  remains later work.
- A backup becomes available only after a valid primary already exists and a
  later save occurs.
- `games.json.invalid` is a single recovery slot and can be replaced by a later
  unreadable primary.
- Live loading of an actual user library should still be smoke-tested before
  release packaging.

## Commit readiness

Increment 1 is compile-safe and test-verified. The repository still contains
earlier uncommitted artwork increments and cleanup work. Stage the source paths
listed in this report deliberately instead of staging the entire working tree.

## Recommended next step

Implement Metadata Increment 2 contracts and orchestration using a fake
provider first. Do not contact Steam or add metadata UI in that increment.

## Related

- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Engineering/Services/Library Service|Library Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
