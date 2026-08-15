---
type: decision
status: accepted
created: 2026-07-30
updated: 2026-07-30
---

# ADR-013 Separate Collection Persistence

## Context

`games.json` is working, versioned, backup-protected, and backward compatible.
Adding early collection data to that document would expand the migration and
regression surface before collection behavior is proven.

## Decision

Persist collections in a separate versioned `collections.json` document under
the NovaLauncher data directory. Reference games only by stable game ID. Use
temporary-file validation, last-valid backup recovery, and typed load status.

## Consequences

- Collection changes cannot corrupt the game-library document.
- Collections can evolve independently.
- Cross-file referential integrity is coordinated by the collection service,
  not a database transaction.
- Orphan membership cleanup must be explicit.

## Related

- [[Product/Features/Collection Management|Collection Management]]
- [[Engineering/Services/Library Service|Library Service]]
