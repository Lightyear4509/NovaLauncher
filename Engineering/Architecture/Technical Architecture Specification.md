---
type: architecture
status: active
role: canonical-technical-specification
created: 2026-07-29
updated: 2026-07-29
---

# NovaLauncher Technical Architecture Specification

---

# Architecture Overview

NovaLauncher follows Clean Architecture using MVVM.

```text
Presentation

↓

ViewModels

↓

Services

↓

Managers

↓

Providers

↓

Infrastructure

↓

External APIs
```

---

# Dependency Rules

Views know only ViewModels.

ViewModels know only Services.

Services know Managers.

Managers know Providers.

Providers know external APIs.

Nothing skips layers.

---

# Services

ArtworkService

MetadataService

LibraryService

SettingsService

AchievementService

ThemeService

ImportService

NotificationService

DownloadService

EmulatorService

---

# Managers

ArtworkProviderManager

MetadataProviderManager

AchievementProviderManager

ImportManager

ThemeManager

CacheManager

---

# Providers

Steam

SteamGridDB

RAWG

IGDB

Local Files

First-party achievement providers

---

# Metadata Retrieval

LibraryItem

↓

MetadataRequest

↓

MetadataProviderManager

↓

IMetadataProvider

↓

MetadataProviderResult

↓

Unmerged MetadataSnapshot values

Current provider:

SteamMetadataProvider

↓

ISteamStoreMetadataClient

↓

Steam public storefront appdetails endpoint

Retrieval is read-only. Merge, override, persistence, and cache policy are
separate layers.

---

# Metadata Merge

Ordered MetadataSnapshot values

↓

MetadataMerger

↓

GameMetadata + field-level provenance

Manual edits use MetadataOverrideService to protect individual fields.
Manual provenance has absolute precedence; otherwise the first valid ordered
provider value wins. Lower-priority snapshots fill gaps, and missing provider
data preserves the last-known-good value.

---

# Metadata Refresh

Game + active game collection

↓

MetadataRefreshCoordinator

↓

MetadataCache

↓ fresh hit or live/stale policy

Staged LibraryItem retrieval and merge

↓

IGameLibraryPersistence

↓ successful save only

Live Game.Metadata replacement

The coordinator preserves atomicity across the current compatibility boundary:
failed persistence leaves the active game unchanged. Retrieval remains
read-only. The coordinator is registered but is not connected to metadata UI.

---

# Metadata Caching

Normalized MetadataRequest identity

↓

In-memory MetadataCache

↓ fresh: use cached snapshots

↓ stale: query providers, then fallback only if none succeed

↓ expired: remove and query providers

Successful live snapshots replace the cache entry. Forced refresh bypasses
reads and stale fallback. Lazy cleanup enforces stale retention and a
least-recently-used entry bound. Persistent SQLite caching remains future work.

---

# Game Details State

MainWindowViewModel

↓ selected game + active game collection

GameDetailsViewModel

↓

IMetadataRefreshCoordinator

↓

MetadataRefreshCoordinator

The main view model remains the page data context during a staged migration.
The active child owns metadata projections, refresh and force-refresh commands,
progress, cancellation, concurrency guards, and outcome wording. Game Details
binds descriptive metadata and refresh controls through this child. Existing
launch, artwork, favorite, rename, and removal commands remain on the main view
model.

The main window and coordinator share the dependency-injection-managed
`GameLibraryService`.

GameDetails

↓ active Nova theme resources + tested width policy

Wide or compact presentation

Manual edit draft

↓

IMetadataEditCoordinator

↓ staged LibraryItem + manual field provenance

IGameLibraryPersistence

↓ successful save only

Live Game.Metadata replacement

The compact layout stacks management content and wraps statistics below 900
pixels. Manual edits and manual-protection removal both preserve atomicity.

---

# Alpha persistence

The alpha source of truth is schema-versioned local JSON: `games.json`,
`collections.json`, and `settings.json`. Stores use validated staging, atomic
replacement, last-known-good backups, corruption preservation, and typed recovery
outcomes as defined in the Alpha Release Specification.

SQLite is a future migration candidate, not current alpha persistence. Any such
migration requires an ADR, pre-migration backup, idempotence, validation, and a
tested rollback path.

The implemented Increment 1 boundary uses independent typed document stores over
an injectable atomic filesystem. Each store owns validation, same-directory
staging, durable flush, read-back verification, replacement, backup recovery,
corruption preservation, schema refusal, and a cancellable exclusive lock.

Backup export and restore coordinate only the three canonical documents. Restore
validates the complete archive before mutation, creates a pre-restore archive,
commits from staging under a launcher-wide lock, and rolls back previously
replaced documents after an I/O failure.

---

# Threading

Never block UI.

All networking async.

Large imports background tasks.

Artwork downloads parallel.

## Increment 2 runtime flow

The Avalonia window delegates all mutable library actions to one workspace view
model. Library and collection coordinators validate and stage replacements,
invoke the typed document stores, and update their published snapshots only when
the save result is `Saved`. A semaphore serializes each coordinator's mutation
boundary. Reads remain side-effect free.

Launching is an infrastructure adapter behind `IGameLauncher`. It validates the
target kind and allowlist before constructing `ProcessStartInfo`. Executables use
`UseShellExecute = false` plus `ArgumentList`; launcher URIs use shell execution
only after exact scheme validation. Process-start failures return typed outcomes
for accessible UI status instead of escaping into the UI event loop.

## Increment 3 Steam import flow

`SteamCatalogSource` performs read-only discovery from supported Windows
registry values or an explicit absolute local Steam root. It parses bounded
`libraryfolders.vdf` and `appmanifest_*.acf` inputs with a dependency-free VDF
parser, canonicalizes library roots, rejects network/device-style roots and
unsafe install-directory values, and reports malformed or missing entries per
file without aborting valid discoveries. It never reads Steam account or
authentication data and never writes beneath a Steam root.

`SteamImportCoordinator` requests a catalog scan and asks `LibraryCoordinator`
for a revision-bound preview. Preview generation and commit share the library
mutation gate. Commit rejects a stale revision, deterministically merges by
Steam App ID, saves one complete `games.json` replacement, and publishes only
after a successful save. Steam App IDs map to deterministic namespaced `GameId`
values. Reimport retains stable identity, favorites, collections, metadata, and
manually changed names; the most recent provider name is retained separately.

## Increment 4 enrichment flow

`GameEnrichmentService` orders typed metadata and artwork providers by numeric
priority and stable provider ID. Provider payloads normalize into snapshots and
candidates before reaching the domain. `MetadataMerger` preserves manual
provenance, takes the first valid ordered value per field, and retains
last-known-good values when providers omit data.

Bounded in-memory caches distinguish fresh, stale, and expired entries and use
least-recently-accessed eviction. Normal refresh uses fresh cache without a
provider call; stale data is fallback-only when live retrieval fails. Forced
refresh bypasses both fresh and stale reads. Enrichment is staged and published
through `LibraryCoordinator` only after a complete document save succeeds.

Steam metadata uses an isolated, bounded client for the public storefront
`appdetails` response. This is not a formally documented Steamworks method and
is a recorded compatibility risk. Steam CDN artwork references are the no-key
fallback. SteamGridDB is higher priority only when
`NOVALAUNCHER_STEAMGRIDDB_API_KEY` is present; the key is sent in an authorization
header and never persisted or logged. Provider URLs are never used as local
paths. The materialization boundary permits only HTTPS, caps encoded bytes,
requires declared and detected PNG/JPEG/WebP agreement, rejects animation,
enforces decoded dimension/pixel ceilings, completes a pixel decode, and writes
under generated names in the NovaLauncher artwork cache. Persistence stores only
opaque `managed-artwork` references or internal placeholders. A failed library
commit rolls back newly created files; successful replacement removes only
obsolete non-manual files inside the managed root. The Avalonia view resolves
only those opaque references and falls back safely if a cached file is missing
or corrupt.

---

# Logging

Structured logging.

Error reporting.

Performance metrics.

Achievement refresh results with account identifiers and payloads redacted.

---

# Future Architecture

Additional reviewed first-party providers

Cloud sync

Multi-device support

Remote API

Web dashboard

## Experimental post-alpha save synchronization

`SaveSyncCoordinator` owns the provider-neutral correctness boundary. Manual
games may persist one explicit local save directory. It scans bounded,
non-reparse traversal into SHA-256 manifests, creates immutable local generations,
waits for a two-scan quiet period after process exit, and queues only changed
file bytes plus deletions. Before launch it requests the peer head and restores
only when the local working set still equals its last observed baseline.

`TailscaleTcpTransport` binds only to an active Tailscale-range local address on
TCP 47471. Tailscale supplies reachability, while NovaLauncher supplies a
single-use 24-hour six-digit invitation accepted only over the Tailscale-bound
listener, a persisted three-attempt lockout, and a salted PBKDF2-SHA256 verifier.
The code authorizes bootstrap but is never an encryption key: successful
redemption transfers a separate Credential-Manager-backed 256-bit secret and
atomically pins the first device identity. Subsequent traffic uses AES-256-GCM message
authentication/encryption, stable peer-ID pinning, timestamp and request-ID
replay rejection, bounded frames, and connection limits. It never reads a
Tailscale credential or administrator API token.

Incoming saves are hash-verified in managed staging. Restore creates a versioned
backup before mutation and rolls back copied files after failure. Divergent
local/remote heads block launch until the user selects Keep local, Use remote,
or Keep both. Steam-imported games are rejected at both mapping and sync
boundaries. Live two-device and power-loss qualification remains a release gate.

## Related

- [[Engineering/Architecture/Architecture Overview|Architecture Overview]]
- [[Engineering/Architecture/System Map|System Map]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Decisions/Decision Index|Decision Index]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-009 Metadata Cache Freshness and Stale Fallback|ADR-009 Metadata Cache Freshness and Stale Fallback]]
- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Decisions/ADR-010 Game Details State Ownership|ADR-010 Game Details State Ownership]]
- [[Decisions/ADR-011 Atomic Manual Metadata Editing|ADR-011 Atomic Manual Metadata Editing]]
