---
type: requirements
status: canonical
version: "1.1"
created: 2026-08-13
updated: 2026-08-13
---

# NovaLauncher Alpha Release Specification

## Release promise

The alpha is a dependable, offline-first Windows launcher for importing,
organizing, searching, and launching Steam and manually added games. It is not a
store, downloader, DRM bypass, ROM source, cloud-sync client, or general-purpose
plugin host.

## Supported environment

- Windows 10 version 22H2 or newer and Windows 11
- x64 desktop systems
- Per-user installation; administrator rights are not required
- Keyboard and mouse; controller-first navigation is deferred
- Internet is optional except for metadata and artwork refresh

## In-scope user journeys

### First run

1. The app creates its data directories under the user's local application-data
   directory, never beside the executable unless portable mode is explicitly
   selected in a future release.
2. The app starts with an empty-library explanation and actions to import Steam
   or add a game manually.
3. Failure to create or read storage produces a diagnostic screen and a safe
   retry/exit choice; it must not loop or discard data.

### Steam import

- Detect Steam from supported registry entries and allow manual folder choice.
- Parse installed application manifests defensively.
- Show a preview before committing additions or updates.
- Preserve user edits, favorites, collections, and artwork overrides on reimport.
- Skip malformed or missing entries individually and show an import summary.
- Never require Steam credentials or read authentication tokens.

### Manual game management

- Add, edit, and remove a game with name, executable/URI, arguments, working
  directory, platform, and optional artwork.
- Validate executable paths and URI schemes before save and again before launch.
- Removal affects only NovaLauncher records and managed cached artwork. It never
  deletes a game installation or arbitrary user files.

### Library experience

- Grid/list library with virtualized scrolling, favorites, collections, and
  deterministic search/sort.
- Search covers title, platform, source, genres, developers, and publishers.
- Game details show provenance-aware metadata, launch action, editable fields,
  and refresh/cancel controls.
- All empty, loading, offline, partial-failure, and no-result states are explicit.

### Metadata and artwork

- Steam public metadata is the default metadata provider.
- Steam CDN is the no-key artwork provider; SteamGridDB is enabled only when a
  user supplies an API key through settings or an environment variable.
- Network calls have cancellation, bounded timeout, retry only transient
  failures with jittered backoff, response-size limits, and clear attribution.
- Cached or placeholder content remains usable offline.
- Manual values have absolute merge precedence until the user clears protection.

### Launch

- Display the exact target in game details before it can be launched.
- Prefer argument-list APIs; never concatenate untrusted text into a shell command.
- Executables use shell execution disabled. Explicit allowlisted URI schemes may
  use the OS registered handler.
- Launch failure is reported without changing library state or crashing the app.

### Settings, themes, backup

- Five built-in themes with a validated resource contract and safe default.
- Settings writes are atomic and rollback in-memory state on persistence failure.
- Export creates a documented archive containing library, collections, settings,
  and user overrides; secrets and disposable caches are excluded.
- Restore validates into staging, previews changes, and creates a pre-restore
  backup before atomically committing.

### Achievements

- Achievements are a first-party, read-only library feature for storefronts
  with documented, authorized APIs or locally available user-owned data.
- Show locked/unlocked state, unlock time when supplied, completion percentage,
  provider attribution, last refresh time, and explicit unavailable/offline/
  stale/error states.
- Never fabricate unlocks, write achievement state to a storefront, scrape an
  undocumented endpoint, or require a public NovaLauncher account.
- Account linking or API credentials are explicit opt-in and stored using the
  secrets boundary; achievement data is cached locally and excluded from logs.
- Refresh is cancellable, bounded, rate-limit aware, and does not block game
  launching or mutate the core library when it fails.

## Explicitly deferred

- Plugins, plugin SDKs, plugin hosts, extension loading, and marketplaces
- ROM/BIOS acquisition and emulator automation
- Cloud synchronization
- Game/mod acquisition and installation
- Playtime tracking, auto-update, telemetry, and NovaLauncher user accounts
- Cross-platform packages
- Steam-inspired Home/Library/Saves navigation redesign and cross-device save linking

The later UI Design and Enhancement increment may add the Home, Library, and
Saves page structure and local save-folder mapping. A cross-device link button
must remain visibly unavailable until a separately approved cloud-save increment
defines transport, authentication, encryption, conflict resolution, quotas,
recovery, and privacy consent. The interface must never claim a save is linked or
synchronized when no verified transport exists.

The repository may contain a separately approved, experimental post-alpha
Tailscale implementation behind explicit manual-game configuration. Its
presence does not expand the alpha release promise or close release-readiness
gates. It must remain opt-in, visibly experimental, hard-exclude Steam games,
and must not be called production-ready without a real two-device safety matrix.

Existing plugin documents are historical research only. Plugins are removed
from the product plan; the application must not load third-party assemblies or
execute downloaded code.

## Data contract

Alpha persistence uses schema-versioned JSON documents:

- `games.json`
- `collections.json`
- `settings.json`

Each store uses same-directory temporary writes, read-back validation, atomic
replacement when supported, and a last-known-good `.bak`. Corrupt input is
preserved as `.invalid-<UTC timestamp>` before any replacement. Unknown future
schema versions are opened read-only or rejected with recovery guidance; they
are never silently downgraded. Writes are serialized per store and guarded
against concurrent process access.

SQLite is a future migration, not the alpha source of truth. A future migration
must be idempotent, backed up, checksummed, and rollback-tested.

## Privacy and security

- No telemetry, analytics, crash upload, account, or background network activity
  without separate explicit consent.
- Logs redact API keys, tokens, user names in paths where practical, environment
  values, and launch arguments marked sensitive.
- Secrets use Windows Credential Manager or DPAPI; they are not written to JSON,
  logs, diagnostics exports, or source control.
- Remote images are decoded with byte, pixel, dimension, redirect, scheme, and
  content-type bounds, then stored under generated names inside the cache root.
- Archive extraction, if later introduced, rejects absolute paths, traversal,
  reparse points, alternate data streams, duplicates, excessive counts/sizes,
  and compression bombs.

## Accessibility and performance acceptance

- Complete primary journeys by keyboard with visible focus and meaningful
  automation names.
- Text and controls meet WCAG 2.2 AA contrast; UI remains usable at 200% scale.
- Cold start target: under 2 seconds on the documented reference machine; the
  release gate is a measured p95 under 3 seconds.
- Search target: p95 under 100 ms for 10,000 generated games after warm-up.
- Idle working-set target: under 500 MB on the reference machine.
- Library scrolling remains responsive with 10,000 generated entries and lazy
  artwork loading.

## Definition of done

A journey is done only when its implementation, automated tests, failure and
cancellation paths, accessible UI, user documentation, and release checklist
items are complete. Generated screenshots, mocks, or passing unit tests alone do
not constitute a usable release.
