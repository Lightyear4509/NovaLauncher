---
type: decision
status: accepted
created: 2026-07-30
updated: 2026-07-30
---

# ADR-012 Unified Library Query Boundary

## Context

The active launcher uses `Game`, while the migration-stage library state uses
`LibraryItem`. Both view models independently implemented search, scope, and
sorting, allowing behavior to drift.

## Decision

Use one pure `ILibrarySearchService` over provider-neutral
`LibrarySearchEntry` projections. View models retain model adaptation and UI
state; the service owns token matching, scopes, deterministic sorting, and
stable tie-breaking.

## Consequences

- Both model paths produce the same search behavior.
- Query logic is testable without Avalonia.
- The compatibility migration does not need to be completed first.
- Projection cost remains synchronous and may require indexing at much larger
  library sizes.

## Related

- [[Product/Features/Library Search|Library Search]]
- [[Engineering/Services/Library Service|Library Service]]
