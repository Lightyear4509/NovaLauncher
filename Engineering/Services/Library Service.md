---
type: service
status: active
priority: critical
related_epic: "[[Product/Epics/Library|Library]]"
created: 2026-07-29
updated: 2026-08-13
---

# Library Service

## Responsibility

Own the unified game-library business logic independently of individual storefronts and launchers.

## Foundation notes

- Games retain a stable identity independent of their source launcher.
- Import providers discover games; the Library Service manages the unified result.
- Search, collections, favorites, tags, and hidden state belong to the library domain.
- `LibraryItem` is the long-term canonical library entity.
- `GameLibraryItemAdapter` isolates the active `Game` UI model during migration.
- `games.json` uses a schema-versioned document and still reads the legacy root array.
- Valid pre-save state is retained as `games.json.bak`.
- An unreadable primary falls back to the last valid backup and surfaces a warning.
- `ILibrarySearchService` is the shared read-only query boundary for both the
  active `Game` UI path and canonical `LibraryItem` migration path.
- Search supports multi-token identity/metadata matching, explicit scopes, and
  deterministic sorting without mutating library state.
- Collections use stable game IDs and a separate versioned
  `collections.json` store with atomic saves and backup recovery.
- `CollectionService` stages CRUD and membership changes, persists the staged
  document, and publishes live state only after success.

## Increment 2 implementation

- `LibraryCoordinator` owns manual add, edit, removal, favorite toggles, search,
  and deterministic name/platform/recently-updated sorting.
- `CollectionCoordinator` serializes collection read-modify-write operations so
  concurrent mutations cannot publish unpersisted or lost state.
- Both coordinators stage a replacement document and publish it only after a
  successful durable save.
- `LibraryWorkspaceViewModel` is the single UI state owner for first-run,
  editor, search, favorites, collections, launch status, and backup/restore.
- Removal is scoped to the NovaLauncher record and requires a second explicit
  action; installed files are never deleted.
- Restore first validates and previews the archive, then requires a second
  explicit action before the persistence service creates a pre-restore backup.
- `SafeGameLauncher` accepts only absolute existing `.exe` targets or the
  `steam`, `goggalaxy`, and `com.epicgames.launcher` URI schemes. Executable
  arguments use `ProcessStartInfo.ArgumentList`; no shell or command-line string
  interpolation is used.

## Related

- [[Product/Epics/Library|Library epic]]
- [[Engineering/Services/Import Service|Import Service]]
- [[Product/Requirements/Product Requirements Document|Product Requirements Document]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
- [[Product/Features/Library Search|Library Search]]
- [[Decisions/ADR-012 Unified Library Query Boundary|ADR-012 Unified Library Query Boundary]]
- [[Product/Features/Collection Management|Collection Management]]
- [[Decisions/ADR-013 Separate Collection Persistence|ADR-013 Separate Collection Persistence]]
