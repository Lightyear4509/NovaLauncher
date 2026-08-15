---
type: architecture
status: active
related_epic: "[[Product/Epics/Artwork|Artwork]]"
created: 2026-07-29
updated: 2026-07-29
---

# Artwork System

---

## Goal

Automatically retrieve high-quality artwork for every game.

---

## Current Providers

- SteamGridDB — priority 200; enabled only when its API key is configured
- Steam — priority 100; always available as the Steam CDN fallback

---

## Planned Providers

- Local Files
- IGDB
- LaunchBox
- Plugin Providers

---

## Flow

```text
Game
  ↓
ArtworkService
  ↓
ArtworkProviderManager
  ├─ SteamGridDB (200, when configured)
  └─ Steam CDN (100)
  ↓
ArtworkCache
  ↓
ArtworkInstaller
  ↓
Managed assets and UI
```

## SteamGridDB behavior

- Initial scope is Steam games with numeric Steam app IDs.
- Successful Steam-to-SteamGridDB ID mappings are cached in memory.
- Cover requests use grids.
- Hero requests use heroes.
- Logo requests use logos.
- Background requests use heroes.
- Known SteamGridDB API or network failures return no candidates, allowing the manager to continue to Steam.
- Cancellation propagates to the caller.
- Missing API configuration does not prevent NovaLauncher from starting.

## Composition

The artwork cache, installer, providers, manager, and service are registered in the application dependency-injection container. The container owns and disposes their lifetimes.

## Artwork resilience

Increment 1 added validated artwork settings plus shared retry, exception, and
progress contracts. Increment 2 applies the retry policy and typed failure
context to SteamGridDB lookup and artwork download boundaries.

Network failures, non-caller timeouts, HTTP 408/429, and HTTP 5xx responses are
transient. Permanent responses and invalid content are not retried. Caller
cancellation propagates immediately. Exhausted SteamGridDB failures continue
to the Steam provider, and exhausted download failures preserve the existing
null-result behavior.

Provider candidate counts, cache hits, download outcomes, retries, and final
failures use structured logging.

## Cache lifecycle

Increment 3 enforces the settings introduced in Increment 1. A cache hit is
valid until its download timestamp exceeds `CacheExpirationDays`. Lazy cleanup
runs on artwork access at most once per `CacheCleanupIntervalHours`, removes
expired artwork and `.download` files older than
`TemporaryFileExpirationHours`, and applies the `MaximumCacheSizeMegabytes`
limit.

Size eviction uses least-recent access. Cache hits update access metadata
without changing the download timestamp. Post-download enforcement protects
the returned artwork path. `ArtworkCacheCleanupResult` makes manual or future
maintenance cleanup observable.

## Placeholder artwork

Increment 4 adds deterministic in-memory PNG placeholders at cover, hero,
logo, and background dimensions. `CoverImageConverter` selects the placeholder
type; cinematic hero bindings explicitly request the wide hero variant.

Placeholders are presentation-only. They do not enter provider results, the
download cache, managed game assets, or import counts. Missing games therefore
remain eligible for later provider downloads. If placeholder creation fails,
the converter returns no image and the existing empty-state UI remains visible.

## Progress reporting

Increment 5 connects the existing `ArtworkProgress` contract through provider,
retry, cache, service, installer, and Steam-import UI boundaries. Events report
stage, message, game, artwork type, provider, item counts, and retry attempt
where applicable.

Progress is optional and observational. The original provider method remains
the required contract; a default progress-aware overload preserves existing
providers. No progress reporter owns or cancels a token.

## UI cancellation

Increment 6 gives `ArtworkOperationController` sole ownership of the active
artwork cancellation source. `MainWindowViewModel` starts one owned operation
for Steam import or manual cover installation, passes its token through every
lower boundary, exposes a cancel command, and completes/disposes ownership in
`finally`.

The status bar displays the cancel action while active. Import and cover
mutation commands are disabled to prevent conflicting writes. Cancellation is
cooperative and preserves completed changes. Progress callbacks ignore updates
after cancellation has been requested.

See [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
for defaults and [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
for increment status.

---

## Related

- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Product/Epics/Metadata|Metadata epic]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
- [[Product/Features/SteamGridDB Artwork Provider|SteamGridDB Artwork Provider]]
- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
- [[Decisions/ADR-003 Placeholder Artwork Policy|ADR-003 Placeholder Artwork Policy]]
- [[Decisions/ADR-004 Artwork Progress Contract|ADR-004 Artwork Progress Contract]]
- [[Decisions/ADR-005 Artwork Cancellation Ownership|ADR-005 Artwork Cancellation Ownership]]
