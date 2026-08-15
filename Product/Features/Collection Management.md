---
type: feature
status: complete
priority: high
related_epic: "[[Product/Epics/Library|Library]]"
created: 2026-07-30
updated: 2026-07-30
progress: 100
---

# Collection Management

## Goal

Let users organize games into durable custom groups without destabilizing the
working game-library format.

## Increment 1 complete

- Provider-neutral collection record with stable identity
- Stable game-ID membership
- UTC creation/update timestamps
- Separate versioned `collections.json`
- Temporary-file write and read-back validation
- Previous-valid-document backup
- Invalid-primary preservation
- Typed not-found, loaded, recovered, and unrecoverable status
- Case-insensitive name uniqueness
- Membership normalization
- Eight focused persistence and recovery tests

## Increment 2 complete

- Collection state owner
- Create, rename, delete
- Add/remove game membership
- No partial in-memory mutation on failed persistence
- Focused service/view-model tests

## Increment 3 complete

- Replaced the placeholder with a functional Collections page
- Theme-aware collection list and membership management
- Empty, no-selection, recovery-warning, status, and failure states
- Accessible collection and membership commands
- Selection continuity after successful create, rename, membership, and delete
- Four focused page view-model tests; 154 total tests pass

## Risks

- Collection files are local and not synchronized.
- Removing a game from the library may leave an orphaned membership until
  cleanup is coordinated.
- The store is currently designed for small local documents.

## Related

- [[Decisions/ADR-013 Separate Collection Persistence|ADR-013 Separate Collection Persistence]]
- [[Engineering/Services/Library Service|Library Service]]
- [[AI/Collections Increment 1 Report|Collections Increment 1 Report]]
- [[AI/Collections Increment 2 Report|Collections Increment 2 Report]]
- [[AI/Collections Increment 3 Report|Collections Increment 3 Report]]
