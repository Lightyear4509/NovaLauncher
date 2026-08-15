---
type: dashboard
area: development
status: active
aliases:
  - Developer Dashboard
created: 2026-07-29
updated: 2026-07-29
---

# Development Dashboard

## Currently Working On

Game Details Increment 2 complete

---

## Current Branch

main

---

## Open Files

- `GameDetailsViewModel.cs`
- `GameDetails.axaml`
- `GameDetailsViewModelTests.cs`
- `GameDetailsMetadata.md`
- `Game Details Metadata Experience.md`

---

## Coding Checklist

- [x] Build passes
- [x] Architecture updated
- [x] Documentation updated
- [ ] Commit created

---

## Current Problems

Game Details Increment 2 is complete. The page now exposes read-only metadata,
empty states, refresh controls, progress, source, and outcomes through the
active child state.

---

## Future Refactors

- Retry the next URI/provider when downloaded artwork fails image validation.
- Retry the next candidate when post-download image validation fails.
- Honor provider `Retry-After` guidance where available.
- Add user-facing provider configuration and status.
- Consider non-Steam matching only after ambiguity and selection behavior are designed.
- Complete the staged migration from `Game` to canonical `LibraryItem`.
- Add manual-edit UI only after value edits and override marking can be
  performed atomically.
- Move Game Details hard-coded colors and reusable styles into theme-aware
  resources in Increment 3.
- Add adaptive layout and keyboard/accessibility verification in Increment 3.
- Migrate whole-library synchronous persistence to an asynchronous item-aware
  boundary after the canonical `LibraryItem` transition.

---

## Work queues

- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Planning/Sprints/Sprint Board|Sprint Board]]
- [[Planning/Backlog|Backlog]]
- [[Operations/Bugs/Bug Index|Bug Index]]

## Engineering references

- [[Engineering/Architecture/Architecture Index|Architecture Index]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Engineering/Standards/Coding Standards|Coding Standards]]
- [[Guides/Developer Handbook|Developer Handbook]]
- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-009 Metadata Cache Freshness and Stale Fallback|ADR-009 Metadata Cache Freshness and Stale Fallback]]
- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Decisions/ADR-010 Game Details State Ownership|ADR-010 Game Details State Ownership]]

> The branch, file, build, and blocker fields above are a preserved working snapshot. They should be updated deliberately or replaced by automation later.
