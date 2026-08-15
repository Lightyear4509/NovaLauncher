---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-006 Metadata Domain and Persistence Boundary

## Context

NovaLauncher has an active `Game` UI/persistence model and a newer
`LibraryItem` domain model. Adding provider metadata independently to both
would deepen duplication. Asset-folder metadata already has a separate
filesystem responsibility and must not become the general game record.

The active `games.json` format was an unversioned root array. A malformed or
temporarily unreadable file was silently interpreted as an empty library, and
saves replaced the file directly.

## Decision

- Treat `LibraryItem` as the long-term canonical library entity.
- Own normalized descriptive metadata in `GameMetadata`.
- Let the current `Game` UI model carry that same domain metadata object during
  migration.
- Use `GameLibraryItemAdapter` as the explicit compatibility boundary.
- Keep `AssetMetadata` limited to managed asset-folder inventory and state.
- Persist the active library inside a schema-versioned document.
- Continue reading legacy root-array libraries.
- Write through a temporary sibling file.
- Preserve the previous valid library as `games.json.bak`.
- Fall back to a valid backup and surface a warning when the primary cannot be
  read.
- Preserve an unreadable primary as `games.json.invalid` when a later save
  replaces it.

## Consequences

- Provider work has one normalized metadata target.
- Existing UI, import, artwork, launch, playtime, and save-folder behavior
  remains unchanged.
- The complete UI migration can occur incrementally.
- Library schema evolution now has an explicit version boundary.
- A valid previous library can recover an unreadable primary.
- Provider snapshots, merge rules, manual overrides, and UI presentation
  remain later increments.

## Related

- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Engineering/Services/Library Service|Library Service]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Product/Epics/Metadata|Metadata epic]]
