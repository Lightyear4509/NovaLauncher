---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-003 Placeholder Artwork Policy

## Context

NovaLauncher needs a consistent visual fallback when artwork is absent or
unreadable. Installing fallback files as real game artwork would prevent later
imports from recognizing that provider artwork is still missing and could
inflate download-success counts.

## Decision

Generate deterministic PNG placeholders in memory for cover, hero, logo, and
background types. Apply them at the image-conversion boundary only.

Do not:

- write placeholders into the artwork download cache
- install placeholders into managed game assets
- assign placeholder paths to game records
- count placeholders as provider downloads

If placeholder construction fails, return no image and allow the existing
empty-state controls to render.

## Consequences

- Missing artwork has a consistent NovaLauncher visual.
- Games remain eligible for real artwork downloads later.
- Provider, retry, cache, installation, and import-count behavior is unchanged.
- Current cover and hero views consume placeholders; logo and background
  variants are available for future consumers.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
