---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-010 Game Details State Ownership

## Context

`GameDetails.axaml` currently binds to the large shared
`MainWindowViewModel`. A separate callback-only `GameDetailsViewModel` existed
but was not used. Adding metadata projections, progress, cancellation, cache
source, and outcome state directly to the main view model would deepen that
coupling.

The main view model also constructed its own `GameLibraryService`, while the
metadata coordinator used the dependency-injection-managed instance.

## Decision

- Keep `MainWindowViewModel` as the page data context during a staged
  migration.
- Make `GameDetailsViewModel` an active child owned by the main view model.
- Let the child own descriptive metadata projections and refresh-operation
  state.
- Keep existing launch, artwork, favorite, rename, and removal commands on the
  main view model for this increment.
- Synchronize selected game and active library collection into the child.
- Introduce `IMetadataRefreshCoordinator` as the child view model's test seam.
- Use one dependency-injection-managed `GameLibraryService` for the main window
  and metadata coordinator.
- Bind visible metadata controls in a later increment.

## Consequences

- New metadata UI state no longer expands `MainWindowViewModel`.
- Existing Game Details behavior and bindings remain stable.
- Refresh state can be tested without constructing Avalonia views or external
  providers.
- The page temporarily binds main commands and nested child metadata state.
- A full standalone page data-context migration remains optional future work.

## Related

- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
