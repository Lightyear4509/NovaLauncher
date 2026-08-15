---
type: feature
status: planned
priority: high
related_epic: "[[Product/Epics/Achievements|Achievements]]"
created: 2026-08-13
updated: 2026-08-13
---

# Achievements

## User outcome

Show trustworthy achievement progress beside a game without requiring a
NovaLauncher account or changing the provider's source-of-truth state.

## Alpha slice

- First-party Steam provider after stable Steam game identity is implemented
- Per-game total, unlocked count, percentage, locked/unlocked entries, optional
  provider-supplied icon, description, and unlock timestamp
- Provider attribution and last successful refresh time
- Explicit not-linked, unavailable, loading, stale, offline, partial, and error
  states
- Manual refresh and cancellation; cached display remains usable offline

## Safety and privacy

- Use only documented and authorized APIs or local user-owned data.
- Credentials are opt-in and stored through the secrets boundary.
- Do not log credentials, account identifiers, achievement payloads, or game
  launch arguments.
- Bound response bytes, item counts, text lengths, image dimensions, redirects,
  timeouts, retries, and cache size.
- Never infer, simulate, unlock, or write achievement state.
- Provider failure cannot modify the game library or prevent launching.

## Acceptance criteria

- Stable provider/game/achievement identity prevents duplicates.
- Deterministic mapping and progress calculation have positive, malformed,
  partial, cancellation, rate-limit, offline, and stale-cache tests.
- Refresh publishes new state only after validation and atomic cache persistence.
- Keyboard and screen-reader users can discover progress and refresh status.
- No normal automated test requires live credentials or network access.

## Deferred

- Cross-provider aggregation
- Social comparison, leaderboards, or NovaLauncher accounts
- Achievement writing or unlock simulation
- Undocumented API scraping
