---
type: changelog
status: active
created: 2026-07-29
updated: 2026-07-30
---

# Changelog

## Unreleased — experimental Tailscale save synchronization

- Added manual-game-only save-folder mapping and launch-lifecycle synchronization; Steam imports remain hard-excluded.
- Added stable device identities, Credential Manager pairing secrets, peer-ID pinning, AES-256-GCM transport, replay rejection, bounded Tailscale-only listening, and wrong-secret isolation.
- Replaced timeless pairing codes with versioned, single-use 24-hour invitations. The first authenticated device consumes the invitation and is permanently pinned until explicitly revoked.
- Added immutable SHA-256 snapshots, changed-file deltas, offline retry, quiet-period checks, backup-before-restore, rollback, and keep-local/use-remote/keep-both conflict controls.
- This is an unsigned preview: real two-device, interruption, firewall, disk-full, scale, and independent security-review gates remain open.

## Unreleased — cover UI and guarded Tailscale foundation

- Library and Home cards render bounded managed cover artwork with aspect-preserving crop and high-quality interpolation.
- Manual games expose Add Cover and Remove Cover actions; imports are validated, atomically copied, and removable without touching the source image.
- Buttons use opaque per-theme normal, hover, and pressed colors with a short hover transition.
- Settings can validate and persist a Tailscale-range peer IP. Device pairing, save-folder mapping, transfer, restore, conflict resolution, and automatic sync remain disabled; Steam games are explicitly out of scope.

Record user-visible changes under the release in which they ship.

## Unreleased

### Added

- Five atomic trusted themes, Settings & Diagnostics, dynamic shell resources,
  compact-layout scrolling, accessibility contracts, and a 10,000-game search gate.
- Independently branded Home, Library, and Downloads & Saves navigation with
  game highlights, local backup controls, and an honest disabled cross-device
  linking state.
- Opt-in, first-party, read-only Steam achievements using documented APIs,
  stable identities, atomic local cache, stale/offline states, completion
  summaries, and secret-free persistence/logging.
- Bounded PNG/JPEG/WebP artwork materialization with signature/content-type,
  decoded dimension/pixel, generated-name, managed-root, rollback, cleanup, and
  cancellation safeguards.
- Safe local cover rendering with an attractive hero treatment and explicit
  placeholder/corrupt-cache fallback.
- A later UI Design and Enhancement increment for independently branded Home,
  Library, and Saves navigation; cross-device save transport remains deferred.
- First-party metadata/artwork provider contracts, deterministic ordering,
  provenance-aware merge, atomic enrichment coordination, bounded fresh/stale
  caches, and deterministic placeholders.
- Bounded retrying HTTPS client, normalized Steam storefront metadata, Steam CDN
  artwork candidates, and disabled-by-default SteamGridDB cover lookup.
- Accessible metadata refresh/force-refresh controls with explicit cache,
  partial-provider, offline, and failure status.

- Read-only Steam registry/manual-root discovery and bounded defensive VDF/ACF
  parsing with per-file failures.
- Revision-bound Steam import preview and explicit atomic commit using stable
  App-ID-derived identities while preserving user-owned library state.
- Accessible Steam import preview controls plus cancellation, stale-preview,
  persistence-failure, malformed-input, and 10,000-game tests.

- Isolated, dependency-free `NovaLauncher.PluginSdk` contract package at
  `1.0.0-alpha.1`.
- Cancellable plugin lifecycle, structured logging, progress, manifest,
  semantic-version, compatibility, capability, and permission contracts.
- Read-only `.novaplugin` validation for schema compatibility, declared entry
  assemblies, unsafe paths, symbolic links, duplicate paths, and resource
  limits.
- Plugin contract test harness and 34 focused SDK test cases.
- Isolated plugin-management layer with schema-versioned persistent inventory.
- Staged local-package installation with exact-byte validation, SHA-256
  integrity, immutable version/hash conflict detection, and disabled-by-default
  updates.
- Persisted enable/disable eligibility, health tracking, automatic and manual
  quarantine, explicit restore, retained-version rollback, and conservative
  uninstall.
- 30 focused plugin lifecycle, persistence, recovery, integrity, rollback, and
  cancellation test cases.
- Generic plugin provider-operation contracts with success, no-result, typed
  failure, retry, progress, and cancellation semantics.
- Compatibility and deprecation metadata with validation policy.
- Deterministic `.novaplugin` package builder with completed-archive validation
  and SHA-256 output.
- Local developer CLI for package validation, packaging, and disabled install.
- Installable `dotnet new` plugin template and compile-tested sample presence
  provider.
- Developer handbook, alpha compatibility policy, and migration guidance.
- 31 focused developer tooling and contract tests.
- Isolated canonical signed-catalog and publisher-verification layer.
- Purpose-scoped RSA-PSS/SHA-256 catalog and exact-package trust keys with
  validity and revocation policy.
- Bounded GitHub Release catalog retrieval with explicit provenance.
- Bounded HTTPS package downloads with progress and cancellation.
- Size, checksum, publisher signature, package structure, SDK compatibility,
  and signed manifest-disclosure validation before exact permission consent.
- Disabled catalog install/update handoff, failed-persistence preservation,
  previous-version retention, and explicit rollback.
- 30 focused catalog security and coordination tests.
- Dependency-free versioned and bounded plugin-host control protocol.
- Separate non-executing plugin-host worker with authenticated handshake,
  health checks, graceful shutdown, and strict message allowlisting.
- Trusted process supervisor with session/correlation validation, deadlines,
  cancellation, typed failures, and process-tree termination.
- Safe age-based cleanup for exact lifecycle and catalog staging files.
- 29 focused protocol, host, supervisor, timeout, cancellation, and cleanup
  tests.
- Enabled inventory-owned package runtime coordination with trusted-side
  SHA-256 revalidation.
- Typed package handoff and independent worker-side package, SDK, hash,
  manifest, and identity validation.
- Native permission, native binary, and declared P/Invoke rejection.
- Session-scoped extraction and one collectible managed load context per
  worker.
- Deadline-bound initialization and shutdown with persisted health/quarantine
  integration and process-termination unload fallback.
- Compile-tested runtime failure probe and 14 focused managed-runtime tests.
- Launcher-owned plugin catalog, trust, inventory, lifecycle, runtime, and
  shutdown coordination.
- Theme-aware Plugin command center with signed refresh, exact consent,
  install/update, enable/start, stop/disable, quarantine recovery, rollback,
  and confirmed uninstall.
- Plugin progress, cancellation, safe failure presentation, and opt-in startup
  disabled by default.
- Complete launcher-side worker deployment and nine focused integration and
  deployment tests.
- Isolated non-operational plugin broker policy project.
- Typed file, HTTPS, process-profile, credential-handle, and save-data request
  contracts.
- Deny-by-default exact identity, permission, scope, quota, timeout, and
  short-lived per-operation consent evaluation.
- Canonical local-path, exact host, named process parameter, opaque credential,
  redacted audit, and cancellation safeguards.
- 32 focused broker-policy security tests.
- Normalized game-metadata domain model with descriptions, people, genres, release date, rating scale, and refresh time.
- Explicit compatibility adapter between the active `Game` UI model and canonical `LibraryItem`.
- Versioned library document, legacy-array reader, valid backup recovery, and focused library tests.
- Metadata provider requests, snapshots, outcomes, progress, typed failures, deterministic orchestration, and fake-provider tests.
- Steam metadata provider, isolated storefront client, normalized descriptive fields, and HTTP/provider tests.
- Deterministic field-level metadata merge, provenance, manual override protection, and persistence tests.
- Atomic single-game metadata refresh coordination with typed persistence
  outcomes and failure/cancellation tests.
- Provider-neutral metadata cache with freshness, stale fallback, forced
  bypass, deep-copy isolation, and bounded cleanup.
- Game Details metadata state foundation with a refresh interface, shared
  persistence, cancellation, outcome state, and view-model tests.
- Game Details read-only metadata presentation, empty state, refresh controls,
  progress, cache/live source, and result messages.
- Theme-aware and adaptive Game Details presentation with accessible metadata
  actions.
- Atomic manual metadata editing with field provenance and provider-control
  reset actions.
- Shared provider-neutral library query service with multi-token metadata
  matching and deterministic sorting.
- Query-aware library result counts, clear actions, accessible search controls,
  and explicit empty/no-match states.
- Separate versioned collection persistence with atomic replacement, valid
  backup recovery, and corrupt-primary preservation.
- Atomic collection CRUD and membership coordination with post-save live-state
  publication.
- Functional, theme-aware Collections page with CRUD, membership management,
  explicit states, accessible actions, and focused view-model tests.
- Persisted built-in theme loading before window creation, one canonical
  catalog, failure-safe apply/save coordination, and focused service tests.
- Theme-aware Settings surfaces, accessible theme status, and automated
  24-brush resource-contract validation for all five built-in themes.
- SteamGridDB artwork provider for Steam games with cover, hero, logo, and background request mapping.
- Automated artwork-provider tests and the first NovaLauncher test project.
- Optional SteamGridDB configuration through `NOVALAUNCHER_STEAMGRIDDB_API_KEY`.
- Artwork-hardening configuration, retry policy, typed exception context, and progress contracts.
- Automated tests for hardening defaults, retry classification, and cancellation.
- Automated provider/download tests for transient recovery and permanent-failure handling.
- Artwork cache cleanup results and cache-lifecycle tests.
- Deterministic in-memory placeholders for cover, hero, logo, and background artwork.
- End-to-end artwork progress reporting with retry and item context.
- Single-operation UI cancellation ownership and a global cancel action.

### Changed

- Removed plugins, plugin SDKs, plugin hosts, marketplaces, and downloaded-code
  extensions from the active product plan. Historical plugin documents are
  retained only as non-authoritative research.
- Added first-party, read-only achievements to the planned alpha sequence with
  authorized-source, privacy, offline-cache, cancellation, and failure-isolation
  requirements.

- Library saves now use temporary-file replacement and preserve the previous valid document as `games.json.bak`.
- Unreadable primary libraries now fall back to a valid backup and surface a warning instead of failing silently.
- Metadata provider failures are isolated into ordered results while caller cancellation propagates.
- Steam metadata retrieval now returns normalized, unmerged snapshots for numeric Steam app IDs.
- Ordered snapshots can now be merged safely without replacing manual or last-known-good values.
- Refreshed metadata now reaches the live game only after library persistence
  succeeds.
- Fresh metadata cache entries now avoid provider calls; stale entries are used
  only when live retrieval has no successful snapshots.
- Game Details now has one synchronized metadata state owner while retaining
  its existing visible behavior.
- Game Details now exposes safe normal and forced single-game metadata refresh
  without changing its existing launch and management actions.
- Game Details now follows active Nova theme resources and stacks safely on
  narrow windows.
- Manual metadata changes now reach the live game only after persistence
  succeeds.
- Active and migration-stage library view models now share one search
  implementation.
- Artwork services and providers are now composed through dependency injection.
- SteamGridDB is tried before the Steam CDN provider when configured.
- Known SteamGridDB failures now allow automatic Steam CDN fallback.
- Successful Steam-to-SteamGridDB ID mappings are cached for the application session.
- Artwork hardening foundation is registered through dependency injection without changing active artwork behavior.
- SteamGridDB lookups and artwork downloads retry classified transient failures.
- Artwork provider/cache operations use structured logs and typed final-failure context.
- Artwork cache entries now expire by download age.
- Lazy cleanup removes expired artwork and stale temporary files and enforces the configured maximum size using least-recently-used eviction.
- Missing or unreadable displayed artwork now uses presentation-only placeholders while remaining eligible for later provider downloads.
- Steam import and manual cover installation now surface provider, download, retry, validation, installation, fallback, cleanup, and completion progress.
- Steam import and manual cover installation can now be cancelled cooperatively without discarding completed changes.
- Theme choices now roll back visually and in memory when settings persistence
  fails.

### Fixed

- Provider cancellation is preserved instead of being treated as a fallback condition.
- Theme settings initialization no longer blocks the Avalonia UI thread and
  prevents main-window creation.

### Removed

-

## Related

- [[Planning/Releases/Versions|Versions]]
- [[Dashboard/Release Dashboard|Release Dashboard]]
