---
type: feature
status: complete
priority: critical
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Foundation

## Outcome

Increment 1 defines normalized descriptive metadata and makes active library
persistence versioned, recoverable, and backward-compatible.

## Implemented

- `GameMetadata` with descriptions, developers, publishers, genres, release
  date, explicitly scaled rating, and refresh timestamp.
- Metadata ownership on canonical `LibraryItem`.
- Compatibility metadata storage on the active `Game` UI model.
- Explicit bidirectional `GameLibraryItemAdapter`.
- Version 1 library document envelope.
- Legacy root-array loading.
- Temporary-file save replacement.
- Previous-valid-library backup.
- Backup recovery with a visible load warning.
- Preservation of unreadable primary data during a later save.
- Focused persistence, compatibility, recovery, and adapter tests.

## Not included

- Provider contracts
- Steam metadata retrieval
- Provider merge rules
- Manual override policy
- Metadata cache expiration
- Metadata UI
- Bulk refresh

## Related

- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Engineering/Services/Library Service|Library Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
