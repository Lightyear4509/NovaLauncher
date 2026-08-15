---
type: feature
status: complete
priority: high
related_epic: "[[Product/Epics/Library|Library]]"
created: 2026-07-30
updated: 2026-07-30
progress: 100
---

# Library Search

## Goal

Provide immediate, predictable library search across useful game identity and
metadata fields without coupling the query engine to either library model.

## Increment 1 complete

- Provider-neutral `LibrarySearchEntry` projection
- Immutable search request with explicit scope and sort
- One `ILibrarySearchService` used by both library view-model paths
- Case-insensitive multi-token AND matching
- Name, platform, source, provider ID, developer, publisher, and genre fields
- All, favorites, and recently-added scopes
- Six deterministic sort modes
- Stable tie-breaking
- Dependency-injection registration
- Ten focused automated tests

## Increment 2 complete

- Result count and query-aware status
- Clear-search command
- Explicit empty/no-match presentation
- Accessible search labels and keyboard behavior
- Tested presentation wording

## Risks

- Reprojection is synchronous and should be profiled for very large libraries.
- Search text is intentionally literal; fuzzy matching and ranking are
  separately scoped.
- Recently Added retains the existing ten-game scope.

## Related

- [[Engineering/Services/Library Service|Library Service]]
- [[Decisions/ADR-012 Unified Library Query Boundary|ADR-012 Unified Library Query Boundary]]
- [[AI/Search Increment 1 Report|Search Increment 1 Report]]
- [[AI/Search Increment 2 Report|Search Increment 2 Report]]
