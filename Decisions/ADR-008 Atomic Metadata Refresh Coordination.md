---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-008 Atomic Metadata Refresh Coordination

## Context

Metadata retrieval is read-only, merging mutates a target metadata object, and
the active persistence service writes the complete `Game` collection. Directly
merging into the live game before saving would expose values that were never
persisted when a save fails.

The active UI model also remains separate from the long-term `LibraryItem`
domain model.

## Decision

- Coordinate refresh for exactly one active `Game`.
- Require the target reference exactly once and its ID uniquely in the supplied
  library collection.
- Convert the target to a cloned `LibraryItem`.
- Retrieve ordered snapshots through the read-only metadata service.
- Merge provider values only into staged metadata.
- Skip persistence when no provider fields are accepted.
- Persist a staged collection with the target replaced by a staged `Game`.
- Replace live `Game.Metadata` only after persistence succeeds.
- Return a typed persistence failure and preserve the live game when saving
  returns false or throws.
- Propagate caller cancellation during retrieval and before persistence.
- Keep synchronous persistence non-cancellable once it begins.

## Consequences

- Failed saves cannot leak unpersisted provider values into the active game.
- The coordinator explicitly owns the compatibility adapter boundary.
- Provider outcomes and merge details remain available to callers.
- The whole-library save remains a temporary migration constraint.
- Future UI code must pass the active collection and handle typed outcomes.
- Cache, retries, bulk refresh, and metadata UI remain separate decisions.

## Related

- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
