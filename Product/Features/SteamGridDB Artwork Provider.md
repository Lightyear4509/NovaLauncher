---
type: feature
status: implemented
priority: critical
release: alpha
owner:
progress: 100
verification: live
created: 2026-07-29
updated: 2026-07-29
related_epic: "[[Product/Epics/Artwork|Artwork]]"
---

# SteamGridDB Artwork Provider

## Summary

NovaLauncher can now prefer SteamGridDB artwork for Steam games while retaining the existing Steam CDN provider as an automatic fallback.

## Implemented behavior

- Implements the shared `IArtworkProvider` contract.
- Accepts Steam games with a numeric Steam app ID.
- Resolves Steam app IDs to SteamGridDB game IDs.
- Caches successful ID mappings in memory.
- Maps covers to grids, heroes to heroes, logos to logos, and backgrounds to heroes.
- Uses priority `200`; Steam CDN remains priority `100`.
- Registers only when `NOVALAUNCHER_STEAMGRIDDB_API_KEY` is configured.
- Converts known SteamGridDB failures into an empty result so fallback can continue.
- Preserves cancellation rather than treating it as a provider failure.
- Uses dependency injection for the artwork pipeline and disposes owned services at application exit.
- Retries classified transient lookup failures with bounded exponential backoff and jitter.
- Logs exhausted lookup failures with typed artwork context before continuing to Steam.

## Verification

- Debug test run: 40 passed, 0 failed after Artwork Hardening Increment 6.
- Release build: succeeded with 0 errors.
- Release test run: 40 passed, 0 failed after Artwork Hardening Increment 6.
- Steam artwork behavior was confirmed working by the user.

## Acceptance

- [x] Confirm downloaded Steam artwork in the application.

## Related

- [[Product/Epics/Artwork|Artwork epic]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
