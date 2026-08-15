---
type: report
status: complete
scope: Artwork Hardening Increment 4
created: 2026-07-29
updated: 2026-07-29
---

# Artwork Hardening Increment 4 Report

## Outcome

Increment 4 is complete. NovaLauncher now renders deterministic,
type-appropriate placeholder artwork when a displayed artwork path is missing
or unreadable. Placeholders are presentation-only, so provider downloads,
managed assets, game records, and download counts retain their existing
behavior.

## Runtime behavior

- Generate PNG placeholders for cover, hero, logo, and background artwork.
- Use dimensions appropriate to each artwork type.
- Use a consistent NovaLauncher gradient and controller mark.
- Generate each encoded placeholder lazily and cache it in memory.
- Render a cover placeholder when a normal converted artwork path is absent.
- Render the wide hero placeholder in cinematic hero views.
- Preserve valid existing artwork unchanged.
- Return no image if placeholder construction fails, retaining the existing
  empty-state UI as the final fallback.

## Persistence policy

Placeholders are not:

- downloaded from a provider
- written into the artwork download cache
- installed into managed game assets
- assigned to game artwork paths
- counted as downloaded artwork

This ensures a game with missing artwork remains eligible for a real provider
download later.

## Source changes

### Created

- `NovaLauncher/Services/Artwork/ArtworkPlaceholderService.cs`
- `NovaLauncher.Tests/Artwork/ArtworkPlaceholderServiceTests.cs`

### Modified

- `NovaLauncher/Converters/CoverImageService.cs`
- `NovaLauncher/Views/Controls/CinematicHero.axaml`
- `NovaLauncher/docs/ArtworkHardening.md`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`
- `NovaLauncher/docs/CodeReview.md`

## Vault changes

### Created

- `Decisions/ADR-003 Placeholder Artwork Policy.md`
- `AI/Artwork Hardening Increment 4 Report.md`

### Modified

- `AI/AI Context.md`
- `Dashboard/Development Dashboard.md`
- `Engineering/Architecture/Artwork System.md`
- `Engineering/Services/Artwork Service.md`
- `Planning/Releases/Changelog.md`
- `Planning/Roadmap/Master Roadmap.md`
- `Planning/Sprints/Current Sprint.md`
- `Planning/Sprints/Sprint Board.md`
- `Product/Features/Artwork System Hardening.md`
- `Product/Features/SteamGridDB Artwork Provider.md`

## Verification

- Debug test suite: 35 passed, 0 failed
- Full serial Release rebuild: succeeded, 0 errors
- Release test suite: 35 passed, 0 failed
- Application configuration JSON parse: passed
- Git whitespace check: passed
- Vault link audit: 71 notes checked, 0 unresolved wikilinks

The Release rebuild reports four existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. They
are outside this increment and were not modified.

Focused coverage verifies:

- every artwork type produces a valid PNG
- cover dimensions are 600 × 900
- hero dimensions are 1600 × 650
- logo dimensions are 800 × 300
- background dimensions are 1600 × 900
- repeated requests produce deterministic bytes
- unsupported artwork types are rejected
- all Increment 1 through Increment 3 behavior remains covered

## Increment boundary

The following remain intentionally unimplemented:

- service-to-UI progress reporting
- UI cancellation controls and token ownership
- retrying another candidate after post-download image validation fails
- honoring provider `Retry-After` response headers
- background cache cleanup independent of artwork access

## Remaining risks

- Unit tests validate encoded placeholder images through Skia. Avalonia bitmap
  creation requires a running Avalonia platform backend and is therefore
  validated by application/XAML compilation rather than headless unit tests.
- A live visual smoke test is still recommended to assess placeholder contrast
  and scaling across card, details, and cinematic hero layouts.
- The converter creates a bitmap for each binding conversion, matching its
  existing behavior for normal file-backed artwork.

## Commit readiness

The Increment 4 paths above are independently scoped and verified. The source
repository still contains earlier uncommitted provider, Increment 1 through
Increment 3, and repository-cleanup work. A focused commit should stage the
listed Increment 4 source paths deliberately rather than staging the entire
working tree.

## Recommended next step

Connect the existing `ArtworkProgress` contract through the artwork service
before adding UI cancellation ownership.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-003 Placeholder Artwork Policy|ADR-003 Placeholder Artwork Policy]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
