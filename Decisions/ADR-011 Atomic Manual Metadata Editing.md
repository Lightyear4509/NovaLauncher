---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-011 Atomic Manual Metadata Editing

## Context

Manual edits must coexist with provider refreshes and the current synchronous
whole-library persistence boundary. Mutating the live game before a failed save
would create UI/disk divergence.

## Decision

`MetadataEditCoordinator` stages edits through `GameLibraryItemAdapter`,
normalizes and compares every managed field, and marks only changed fields with
manual provenance. It persists a staged game collection and replaces the live
`Game.Metadata` only after a successful save.

Clearing manual protection follows the same staged-save rule and retains the
current field value. The next valid provider refresh may replace that value.

## Consequences

- Persistence failure cannot partially mutate the active game.
- No-op edits avoid unnecessary writes.
- Changed manual fields survive provider refresh.
- The synchronous save cannot be cancelled once it begins.
- Edit history and durable undo remain future work.

## Related

- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
