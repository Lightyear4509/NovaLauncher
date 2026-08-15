---
type: service
status: active
priority: critical
related_epic: "[[Product/Epics/Artwork|Artwork]]"
architecture: "[[Engineering/Architecture/Artwork System|Artwork System]]"
created: 2026-07-29
updated: 2026-07-29
---

# Artwork Service

## Responsibility

Coordinate artwork retrieval without coupling the UI to individual artwork providers.

## Current design

The active Increment 4 source exposes ordered first-party `IArtworkProvider`
contracts. SteamGridDB is tried only when its environment key is present; Steam
CDN supplies deterministic no-key HTTPS candidates. Candidate count and response
size are bounded, unsafe/non-HTTPS SteamGridDB URLs are discarded, manual artwork
provenance is preserved, and missing kinds receive deterministic internal
placeholders. Only validated references are persisted atomically with metadata.

Remote image byte download, bounded decode, managed-file installation, and
visual artwork rendering are not yet active in this source foundation and must
not be inferred from historical reports below.

`ArtworkService` communicates with `ArtworkProviderManager`, which filters eligible providers and orders them by priority. SteamGridDB is tried first when configured; Steam CDN remains the fallback. Results flow through `ArtworkCache` and `ArtworkInstaller` before becoming managed assets.

Known SteamGridDB failures are handled at the provider boundary so they do not prevent fallback. Cancellation is not swallowed.

The artwork pipeline is registered through dependency injection rather than being constructed inside `MainWindowViewModel`.

Increment 2 uses the configured retry policy at the SteamGridDB lookup and
artwork-download boundaries. Only classified transient failures retry. Caller
cancellation propagates, while exhausted provider failures continue through
the existing fallback path. Structured logs carry provider, game, artwork
type, attempt, status, and URI context where applicable.

Increment 3 makes `ArtworkCache` responsible for download-age expiration,
interval-based cleanup, stale temporary-file removal, and least-recently-used
size eviction. Cache cleanup failures are logged and do not change the
service's null-result/fallback contract.

Increment 4 does not change `ArtworkService`. Placeholder artwork is applied
only when the presentation layer converts a missing or unreadable image path.
Provider, cache, installation, and download-count behavior therefore remains
unchanged.

Increment 5 adds optional `IProgress<ArtworkProgress>` flow. `ArtworkService`
reports provider resolution, queries, candidates, fallback, retrieval outcome,
and forwards lower-level retry/cache progress. `ArtworkInstaller` reports
validation, installation, and completion. Steam import and manual cover
installation map messages to the existing status text.

Increment 6 leaves lower service contracts unchanged and gives the UI explicit
token ownership. The owned token flows through the existing service, provider,
cache, retry, validation, and installation parameters. Caller cancellation
continues to propagate rather than becoming a fallback or failure result.

## Related documents

- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
- [[Product/Epics/Artwork|Artwork epic]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Product/Features/SteamGridDB Artwork Provider|SteamGridDB Artwork Provider]]
- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]

## Current limitations

- Automatic Steam import currently requests covers and heroes.
- Invalid downloaded image handling occurs after caching and may require a later retry-across-candidates improvement.
- Candidate retry after post-download validation remains a separate follow-up.
