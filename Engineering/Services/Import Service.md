---
type: service
status: planned
priority: high
related_epic: "[[Product/Epics/Library|Library]]"
created: 2026-07-29
updated: 2026-07-29
---

# Import Service

## Responsibility

Coordinate game discovery and import through reviewed first-party integrations.
Plugins are removed from the active product plan.

## Increment 3 boundary

- Registry discovery is read-only and checks user plus 32/64-bit machine views.
- An absolute local Steam root can be supplied manually; relative, UNC, and
  device-style roots are rejected.
- `libraryfolders.vdf` and `appmanifest_*.acf` parsing has character, nesting,
  token, token-length, library-count, and manifest-count limits.
- Missing or malformed manifests become per-file preview failures.
- Preview performs no persistence. Commit is explicit, revision-bound, and one
  atomic library-document save.
- Reimport preserves user-owned fields and collection membership.
- The importer reads no credentials, login files, tokens, or account data and
  never modifies Steam files.

## Foundation notes

- Platform-specific discovery belongs in import providers.
- Large imports run as background tasks.
- Imports should be repeatable without creating duplicate games.

## Related

- [[Product/Epics/Library|Library epic]]
- [[Engineering/Services/Library Service|Library Service]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
