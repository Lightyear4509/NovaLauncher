---
type: implementation-report
status: complete
area: game-details
increment: 1
created: 2026-07-29
updated: 2026-07-29
---

# Game Details Increment 1 Report

## Outcome

Game Details Increment 1 establishes one active, testable child owner for
descriptive metadata state while preserving the existing visible Game Details
page and all launch, artwork, favorite, rename, and library-management
behavior.

## Source changes

### Created

- `Services/Metadata/IMetadataRefreshCoordinator.cs`
- `NovaLauncher.Tests/ViewModels/GameDetailsViewModelTests.cs`
- `docs/GameDetailsFoundation.md`

### Reworked

- `ViewModels/GameDetailsViewModel.cs`
  - Replaced the unused callback wrapper with active child state.
  - Added metadata projections and fallbacks.
  - Added normal and forced refresh commands.
  - Added progress, cancellation, concurrency guards, result state, and outcome
    wording.

### Modified

- `Services/Metadata/MetadataRefreshCoordinator.cs`
  - Implements the UI-facing interface without changing runtime refresh
    behavior.
- `ViewModels/MainWindowViewModel.cs`
  - Receives the DI-managed `GameLibraryService`.
  - Owns and synchronizes the child Game Details state.
  - Disposes the child operation owner.
- `Core/Bootstrap/AppBootstrapper.cs`
  - Registers the interface mapping and child view model.
- `docs/Architecture.md`
- `docs/MetadataRefresh.md`
- `docs/CodeReview.md`
- `docs/Changelog.md`

## Implemented behavior

- The child receives the selected game and active game collection.
- Refresh commands are disabled unless that exact game belongs to the
  collection.
- Normal refresh honors cache policy.
- Force refresh requests cache bypass.
- Only one refresh can run at a time.
- Selection changes cancel active metadata work.
- Cancellation remains distinct from failure.
- Metadata display projections normalize empty values safely.
- Outcome wording distinguishes:
  - live refresh
  - fresh cache
  - stale fallback
  - no eligible provider
  - no match
  - provider failure
  - no changes
  - persistence failure
  - cancellation
- Unexpected command exceptions become page state instead of escaping.
- UI-thread synchronization context is preserved for progress when available.
- Main-window and coordinator persistence now share one
  `GameLibraryService`.

## Existing behavior preserved

- `GameDetails.axaml` was not changed.
- `NavigationHost` still supplies `MainWindowViewModel` as page data context.
- Existing launch, Steam launch, artwork, favorite, rename, remove-cover, and
  remove-game commands remain on `MainWindowViewModel`.
- Artwork operation ownership and cancellation are unchanged.
- Metadata merge, persistence, cache, and provider policies are unchanged.

## Verification

- Debug solution build: succeeded.
- Debug tests: 104 passed, 0 failed.
- Release solution build: succeeded.
- Release tests: 104 passed, 0 failed.
- Compiler result: 0 errors and 4 existing nullable warnings in
  `CinematicHero.axaml.cs`.
- Vault link audit: 94 Markdown notes checked, 0 unresolved internal links.

## Documentation updated

- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Decisions/ADR-010 Game Details State Ownership|ADR-010 Game Details State Ownership]]
- [[Product/Epics/User Interface|User Interface epic]]
- [[Product/Epics/Metadata|Metadata epic]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Planning/Sprints/Sprint Board|Sprint Board]]
- [[Planning/Roadmap/Master Roadmap|Master Roadmap]]
- [[Planning/Releases/Changelog|Changelog]]
- [[Dashboard/Development Dashboard|Development Dashboard]]
- [[AI/AI Context|AI Context]]

## Boundaries and remaining risks

- The visible page does not bind the child metadata state yet.
- `MainWindowViewModel` remains the page data context and retains existing page
  actions during the staged migration.
- The active child and main view model temporarily divide page ownership by
  concern.
- Synchronous whole-library persistence cannot be cancelled after it begins.
- This increment has view-model tests but no Avalonia headless visual tests.
- A live multi-theme Game Details smoke test remains necessary after visible
  controls are added.
- The repository contains prior uncommitted artwork and metadata work; this
  increment preserves that existing dirty-worktree state.

## Recommended next step

Implement Game Details Increment 2 only: bind read-only metadata presentation,
empty states, refresh, force-refresh, cancel controls, progress, and result
messages to the active child state. Do not add manual editing or broad visual
polish in that increment.

## Related

- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
