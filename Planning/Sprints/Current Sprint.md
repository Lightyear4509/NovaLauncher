---
type: sprint
status: superseded
goal: Historical plugin work; no longer active
created: 2026-07-29
updated: 2026-07-30
---

# Current Sprint

> Superseded historical plugin sprint. Do not continue plugin implementation.
> Active sequencing is maintained in
> [[AI/Google AI Studio Build Guide|Google AI Studio Build Guide]].

## Goal

Establish the Plugin SDK contract and package-safety foundation without
loading third-party code

---

## Tasks

- [x] [[Product/Features/SteamGridDB Artwork Provider|SteamGridDB Provider]]
- [x] [[Product/Features/Artwork System Hardening|Artwork Hardening Increment 1]]
- [x] [[Product/Features/Artwork System Hardening|Artwork Hardening Increment 2]]
- [x] [[Product/Features/Artwork System Hardening|Artwork Hardening Increment 3]]
- [x] [[Product/Features/Artwork System Hardening|Artwork Hardening Increment 4]]
- [x] [[Product/Features/Artwork System Hardening|Artwork Hardening Increment 5]]
- [x] [[Product/Features/Artwork System Hardening|Artwork Hardening Increment 6]]
- [ ] Cache Improvements
- [x] [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Increment 1]]
- [x] [[Product/Features/Metadata Provider Contracts|Metadata provider contracts]]
- [x] [[Product/Features/Steam Metadata Provider|Steam metadata provider]]
- [x] [[Product/Features/Metadata Merge and Override Policy|Metadata merge and override policy]]
- [x] [[Product/Features/Metadata Refresh Coordination|Metadata refresh coordination]]
- [x] [[Product/Features/Metadata Cache Policy|Metadata cache policy]]
- [x] [[Product/Features/Game Details Metadata Experience|Game Details Increment 1 state foundation]]
- [x] [[Product/Features/Game Details Metadata Experience|Game Details Increment 2 metadata presentation]]
- [x] [[Product/Features/Game Details Metadata Experience|Game Details Increment 3 theme and responsive polish]]
- [x] [[Product/Features/Game Details Metadata Experience|Game Details Increment 4 manual editing and provenance]]
- [x] Unit Tests
- [x] [[Product/Features/Library Search|Search Increment 1 shared query service]]
- [x] [[Product/Features/Library Search|Search Increment 2 result-state UX]]
- [x] [[Product/Features/Collection Management|Collections Increment 1 persistence]]
- [x] [[Product/Features/Collection Management|Collections Increment 2 coordination]]
- [x] [[Product/Features/Collection Management|Collections Increment 3 page integration]]
- [x] [[Product/Features/Theme Reliability|Themes Increment 1 startup and persistence]]
- [x] [[Product/Features/Theme Reliability|Themes Increment 2 UI and resource validation]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 1 contracts and package validation]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 2 lifecycle safety]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 3 developer experience]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 4 catalog security]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 5 runtime-host foundation]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 6 verified managed loading]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 7 launcher command center]]
- [x] [[Product/Features/Plugin SDK and Catalog|Plugin SDK Increment 8 broker policy foundation]]

---

## Risks

- SteamGridDB API limits and availability
- Invalid remote artwork can fail during installation
- Artwork selection currently uses the first acceptable result
- Plugin SDK `1.0.0-alpha.1` is a preview and may change before its stable
  compatibility policy is declared.
- A normal child process is a crash/dependency boundary, not a permission
  sandbox.
- Archive validation reduces package risk but does not establish publisher
  trust or make plugin execution safe.
- SHA-256 detects package changes but does not authenticate a publisher.
- Inventory coordination is process-local; a future launcher integration needs
  a single owner or cross-process lock.
- The template targets an unpublished alpha SDK package and currently requires
  a local NuGet source.
- Package reproducibility has been tested for identical inputs under the
  current .NET runtime, not across runtime versions.
- Catalog root-key distribution and rotation are not implemented.
- RSA signatures authenticate configured publishers but do not sandbox future
  plugin execution.
- GitHub provenance is informational; catalog trust comes from the configured
  signing key.
- Brokered capabilities and OS-level sandboxing remain unimplemented.
- Production catalog ownership, root keys, rotation, and emergency revocation
  delivery require separate operational approval.
- Broker policy does not restrict direct worker OS access; a proven Windows
  restricted identity remains mandatory before resource operations.

---

## Sprint Notes

- SteamGridDB provider implemented with optional API-key registration.
- Steam CDN fallback retained.
- Provider ordering, type mapping, failure behavior, cancellation, and ID caching covered by automated tests.
- Steam artwork was confirmed working.
- Artwork hardening Increment 1 added configuration and resilience contracts without changing runtime behavior.
- Artwork hardening Increment 2 applies classified retries, typed final failures, and structured logging to provider/download boundaries.
- Artwork hardening Increment 3 enforces expiration, stale-file cleanup, and least-recently-used size limits.
- Artwork hardening Increment 4 adds presentation-only placeholders without contaminating managed artwork.
- Artwork hardening Increment 5 adds end-to-end optional progress reporting and status updates.
- Artwork hardening Increment 6 adds owned UI cancellation, command guards, and preserved partial progress.
- Metadata Increment 1 adds the normalized domain model, compatibility adapter, versioned library document, legacy loading, and backup recovery.
- Metadata Increment 2 adds ordered provider contracts, read-only orchestration,
  failure isolation, progress, cancellation, and fake-provider tests.
- Metadata Increment 3 adds Steam storefront retrieval, normalization, typed
  failures, identity validation, and cancellation.
- Metadata Increment 4 adds field-level provenance, deterministic merging,
  manual override protection, last-known-good preservation, and deep-copy
  safety.
- Metadata Increment 5 adds staged retrieve/merge/save coordination, typed
  persistence outcomes, cancellation, and atomic live-model updates.
- Metadata Increment 6 adds provider-neutral snapshot caching, freshness,
  stale fallback, forced bypass, deep-copy isolation, and bounded cleanup.
- Game Details Increment 1 adds one active child metadata state owner, refresh
  test seam, shared persistence, progress, cancellation, and outcome state
  without changing the visible page.
- Game Details Increment 2 adds read-only descriptive metadata, empty states,
  refresh, force-refresh, cancel, progress, source, and outcome controls while
  preserving existing page actions.
- Game Details Increment 3 moves reusable styles onto active theme resources,
  adds compact/wide layouts, and improves action accessibility.
- Game Details Increment 4 adds validated manual editing, atomic persistence,
  field provenance, and provider-control reset actions.
- Debug tests pass: 120 tests.
- Release tests pass: 120 tests.
- Release solution build succeeds with four pre-existing nullable warnings in `CinematicHero.axaml.cs`.
- Search Increment 1 centralizes provider-neutral scope, multi-token matching,
  and deterministic sorting across both library view-model paths.
- Search Increment 1 Debug and Release tests pass: 130 tests.
- Search Increment 2 adds query-aware counts, explicit empty/no-match states,
  clear actions, accessibility, and tested presentation wording.
- Collections Increment 1 adds an isolated versioned store, atomic writes,
  valid-backup recovery, typed load status, and eight persistence tests.
- Collections Increment 2 adds staged CRUD/membership coordination and an
  attached child view model; 150 Debug and Release tests pass.
- Collections Increment 3 replaces the placeholder with theme-aware CRUD and
  membership UI, explicit states, accessible actions, and four page
  view-model tests; 154 tests pass.
- Themes Increment 1 loads persisted settings before window construction,
  centralizes the built-in catalog, rolls back failed saves, and adds six
  focused tests; 160 tests pass.
- Themes Increment 2 moves Settings onto dynamic theme resources, adds
  accessible operation status, and validates the 24-brush contract across all
  five themes; 163 tests pass.
- Theme startup recovery removed a UI-thread deadlock found through Visual
  Studio. Debug and Release now require a responsive, titled top-level window,
  not process liveness alone.
- Plugin SDK Increment 1 adds an isolated, dependency-free contract assembly,
  semantic-version compatibility, a strict manifest, capability/permission
  disclosure, read-only package validation, and a contract harness.
- The launcher has no SDK reference and performs no plugin discovery,
  extraction, installation, loading, or execution.
- 34 focused SDK cases raise the suite total to 197; Debug and Release builds,
  tests, package creation, and responsive-window startup gates pass.
- Plugin SDK Increment 2 adds a separate lifecycle-management project with
  atomic inventory persistence, staged package validation, SHA-256 integrity,
  health/quarantine state, retained-version rollback, and conservative
  uninstall.
- The launcher still references neither plugin project and no assembly is
  loaded or executed.
- 30 focused lifecycle cases raise the suite total to 227.
- Plugin SDK Increment 3 adds typed provider operations, compatibility policy,
  deterministic package creation, a validate/pack/install CLI, a real
  `dotnet new` template, and a compile-tested sample presence plugin.
- The real template engine successfully installed and generated a renamed
  project with consistent manifest/runtime identity.
- 31 focused developer-experience cases raise the suite total to 258.
- Plugin SDK Increment 4 adds canonical signed catalogs, purpose-scoped catalog
  and publisher keys, GitHub Release provenance, bounded staged downloads,
  checksum/signature/package/disclosure verification, exact permission
  consent, disabled updates, and rollback.
- 30 focused catalog-security cases raise the suite total to 288.
- The launcher still references no plugin project and executes no downloaded
  code.
- Plugin SDK Increment 5 adds strict bounded control messages, authenticated
  host sessions, health checks, deadlines, cancellation, process-tree
  termination, and safe stale-stage cleanup.
- The worker accepts no package path and supports no discovery, extraction,
  loading, initialization, or provider operation.
- 29 focused runtime-boundary cases raise the suite total to 317.
- Plugin SDK Increment 6 restricts startup to enabled inventory-owned packages,
  revalidates on both sides, safely extracts, rejects declared/detected native
  code, and loads one managed plugin per worker.
- Initialization and shutdown are deadline-bound; success resets health and
  failures feed the existing quarantine threshold.
- 14 focused managed-runtime cases raise the suite total to 331.
- Plugin SDK Increment 7 composes catalog, trust, inventory, lifecycle, and
  runtime behind one launcher-owned service and adds the Plugin command center.
- Signed refresh, exact consent, enable/start separation, stop/disable,
  quarantine restore, rollback, confirmed uninstall, progress, and
  cancellation are visible without exposing provider operations.
- Complete host deployment and nine focused integration cases raise the suite
  total to 340.
- Plugin SDK Increment 8 adds isolated typed broker requests, deny-by-default
  policy, exact consent, quotas, bounded timeouts, and redacted audit without
  connecting an executor or protocol.
- 32 focused broker-policy cases raise the suite total to 372.

## Related

- [[Planning/Sprints/Sprint Board|Sprint Board]]
- [[Product/Epics/Artwork|Artwork epic]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Product/Features/Library Search|Library Search]]
- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[AI/Plugin SDK Increment 1 Report|Plugin SDK Increment 1 Report]]
- [[AI/Plugin SDK Increment 2 Report|Plugin SDK Increment 2 Report]]
- [[AI/Plugin SDK Increment 3 Report|Plugin SDK Increment 3 Report]]
- [[AI/Plugin SDK Increment 4 Report|Plugin SDK Increment 4 Report]]
- [[AI/Plugin SDK Increment 5 Report|Plugin SDK Increment 5 Report]]
- [[AI/Plugin SDK Increment 6 Report|Plugin SDK Increment 6 Report]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/Plugin SDK Increment 8 Report|Plugin SDK Increment 8 Report]]
