---
type: feature
status: complete
priority: high
related_epic: "[[Product/Epics/User Interface|User Interface]]"
created: 2026-07-29
updated: 2026-07-29
progress: 100
---

# Game Details Metadata Experience

## Goal

Present normalized metadata, provide safe single-game refresh and editing
controls, and remain usable across Nova themes and window sizes.

## Increment 1 — state foundation

- Active `GameDetailsViewModel` child state owner
- Selected-game and library synchronization
- Refresh/force/cancel commands, progress, typed outcomes, and overlap guards
- Shared dependency-injection-managed persistence
- Focused view-model tests

## Increment 2 — metadata presentation

- Descriptions, genres, developers, publishers, release date, rating, and
  refresh time
- Empty states and safe fallbacks
- Refresh source badge and outcome messaging
- Existing launch, artwork, favorite, rename, and removal bindings preserved

## Increment 3 — theme and adaptive polish

- Reusable Game Details styles backed by active `Nova*Brush` resources
- Compact/wide layout policy with a 900-pixel threshold
- Compact two-row statistics and stacked management content
- Accessible names and tooltips on metadata workflow actions
- Focused layout-policy tests

## Increment 4 — manual editing and provenance

- Validated edit form for all seven managed metadata fields
- Atomic staged persistence before live-model replacement
- Manual provenance on changed fields only
- Field source presentation
- Persisted “Use Provider” controls that retain the current value
- No-op and persistence-failure safety
- Coordinator and edit-state tests

## Deferred

- Edit history and cross-session undo
- Bulk metadata editing
- Asynchronous library persistence
- Automated provider conflict resolution

## Related

- [[Decisions/ADR-010 Game Details State Ownership|ADR-010 Game Details State Ownership]]
- [[Decisions/ADR-011 Atomic Manual Metadata Editing|ADR-011 Atomic Manual Metadata Editing]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Product/Epics/Metadata|Metadata epic]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[AI/Game Details Increment 3 Report|Game Details Increment 3 Report]]
- [[AI/Game Details Increment 4 Report|Game Details Increment 4 Report]]
