---
type: roadmap
status: superseded
version: "1.0"
created: 2026-07-30
updated: 2026-07-30
---

# Alpha Ecosystem and Cloud Roadmap

> Superseded for plugin-related planning by
> [[docs/decisions/ADR-0004-remove-plugins-add-first-party-achievements|ADR-0004]].
> Plugin sections below are retained as historical research and are not active
> implementation scope. Use [[Planning/Roadmap/Master Roadmap|Master Roadmap]].

## Product direction

NovaLauncher should become an extensible launcher first, then use that
extension system to deliver emulator support, service integrations, seamless
cloud saves, and lawful game acquisition without coupling every provider to
the core executable.

The work is divided into four pillars:

1. [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
2. [[Product/Epics/Emulator Support|Emulator Support]]
3. [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
4. [[Product/Features/Authorized Acquisition and Installation|Authorized Acquisition and Installation]]

## Release strategy

Do not make all integrations prerequisites for the first alpha. The alpha
should prove the contracts, trust model, installation flow, and rollback
behavior with a small set of reference plugins. Additional integrations can
then ship independently without destabilizing the launcher.

## Phase 0 — Alpha stabilization

### Required

- [x] Library, metadata, artwork, search, collections, themes
- [x] Debug and Release automated test gates
- [x] Responsive-window startup verification
- [ ] Live artwork smoke matrix
- [ ] Crash boundary and global unhandled-exception logging
- [ ] Alpha data-backup/export command
- [ ] Alpha release checklist and upgrade notes

### Exit gate

- Clean Debug and Release builds
- Full test suite passes
- Main window is responding with a nonzero handle
- Existing library and settings survive upgrade
- No secret or local user data enters source control

## Phase 1 — Plugin SDK foundation

### Increment 1: contracts — complete

- [x] SDK contract assembly independent from launcher internals
- [x] Plugin manifest, namespaced ID, semantic version, author, license, and
  homepage
- [x] Supported SDK version range
- [x] Capability declarations:
  - metadata provider
  - artwork provider
  - library importer
  - game action
  - presence
  - achievements
  - save-location discovery
  - cloud transport
  - mod integration
  - emulator profile
  - settings panel
- [x] Cancellation, progress, and logging contracts
- [x] Read-only package and compatibility validation
- [x] Contract test harness
- [ ] Provider-operation configuration and typed failure contracts

The implemented `1.0.0-alpha.1` SDK is a contract preview. Runtime discovery,
installation, and execution remain disabled until lifecycle safety is built.

### Increment 2: lifecycle and safety foundation — complete

- [x] Schema-versioned persistent inventory
- [x] Staged local-package install and update
- [x] Manifest and SDK compatibility validation
- [x] SHA-256 package integrity verification
- [x] Enable and disable eligibility state
- [x] Consecutive-failure health tracking and quarantine
- [x] Explicit quarantine restoration
- [x] Retained-version rollback
- [x] Conservative uninstall
- [x] Persist-before-publish state coordination
- [x] Package signatures and publisher identity
- [ ] Dependency resolution
- [ ] Cross-process inventory locking
- [ ] Runtime directory discovery
- [ ] Isolated managed load context inside one worker per plugin
- [x] Non-executing out-of-process host and supervision foundation

No third-party code is loaded or executed. The launcher application remains
independent from both plugin projects.

### Increment 3: developer experience — complete

- [x] Installable `dotnet new` plugin template
- [x] Packable SDK NuGet project
- [x] Compile-tested sample presence plugin
- [x] Local validate, pack, and disabled-install commands
- [x] Manifest validator and deterministic package builder
- [x] Plugin and provider compatibility test harness
- [x] Typed provider-operation outcomes and failures
- [x] Versioning and deprecation policy
- [x] Developer handbook and migration guide

The template was installed and instantiated through an isolated real .NET
template hive. Runtime discovery and plugin execution remain disabled.

### Increment 4: catalog security foundation — complete

- [x] Canonical curated signed index
- [x] Purpose-scoped catalog and publisher trust keys
- [x] Exact package signatures and SHA-256
- [x] GitHub Release source adapter with provenance
- [x] Bounded staged package download with progress and cancellation
- [x] Exact manifest, capability, and permission disclosure
- [x] Consent only after cryptographic and structural validation
- [x] Disabled local install and update
- [x] Preserve previous state after failed update persistence
- [x] Hash-verified previous-version rollback
- [x] Never execute downloaded packages
- [ ] In-launcher catalog browsing and consent UI
- [ ] Background refresh, key rotation, and emergency revocation delivery

The security and coordination layer is complete in isolation. It is not yet
registered with the launcher application.

### Increment 5: non-executing runtime-host foundation — complete

- [x] Versioned, length-prefixed, strict, bounded control protocol
- [x] Unique session and per-message correlation IDs
- [x] Private random handshake token and fixed-time comparison
- [x] Separate non-executing host process
- [x] Trusted process supervisor and explicit session state
- [x] Startup, health-operation, and shutdown deadlines
- [x] Caller cancellation and forced process-tree termination
- [x] Typed startup, timeout, exit, and protocol failures
- [x] Non-recursive age-based lifecycle and catalog staging cleanup
- [x] Reparse-point refusal and observable cleanup outcomes
- [x] No package handoff, extraction, assembly loading, or launcher reference
- [ ] Verified managed-package loading inside one worker per plugin
- [ ] Lifecycle failure/quarantine connection
- [ ] Brokered resource capabilities or proven OS sandbox

The host currently permits only authentication, readiness, health, shutdown,
and bounded errors. A normal child process is not treated as a permission
sandbox.

### Increment 6: verified managed loading — complete

- [x] Runtime start restricted to enabled inventory-owned packages
- [x] Trusted-side exact SHA-256 revalidation
- [x] Typed package identity, entry-point, hash, and session-root handoff
- [x] Independent worker package, SDK, hash, manifest, and identity checks
- [x] Reject native permission, native binaries, and declared P/Invoke
- [x] Session-scoped safe extraction
- [x] One collectible managed load context per worker
- [x] Exact runtime/manifest identity
- [x] Deadline-bound initialization and shutdown
- [x] Health reset on success
- [x] Persisted failure counting and quarantine threshold
- [x] Process termination as definitive unload fallback
- [x] No launcher reference or provider-operation protocol
- [ ] Brokered resource capabilities or OS sandbox
- [ ] Catalog, consent, recovery, and runtime UI

Native inspection is defense in depth. It does not make managed plugin code an
OS-sandboxed principal.

### Increment 7: launcher command center — complete

- [x] One launcher owner for inventory, catalog, trust, lifecycle, and runtime
- [x] Complete worker payload deployed beside the launcher
- [x] Catalog refresh locked without real source and trusted keys
- [x] Exact signed install/update consent
- [x] Separate install, enable, and start transitions
- [x] Stop, disable, quarantine recovery, rollback, and confirmed uninstall
- [x] Progress, cancellation, logging, and visible failure states
- [x] Automatic startup opt-in and false by default
- [x] Real deployed-host startup verification
- [x] No third-party assembly loaded into the launcher
- [x] No provider-operation protocol or resource authority
- [ ] Brokered resource capabilities or proven OS sandbox
- [ ] Root-key rotation and emergency revocation delivery
- [ ] First curated reference plugin

Increment 7 makes the isolated foundations operable without weakening their
trust boundary. An empty default catalog is intentional until production trust
roots and release ownership are approved.

### Increment 8: broker policy foundation — complete

- [x] Isolated trusted policy project
- [x] Separate typed file, HTTPS, process-profile, credential-handle, and
  save-data requests
- [x] Exact identity, manifest permission, scope, quota, timeout, and validity
  binding
- [x] Disabled-by-default policies
- [x] Per-operation consent only
- [x] Short-lived exact receipts and unambiguous SHA-256 fingerprints
- [x] Canonical local path and relative save path validation
- [x] UNC/device and alternate-stream rejection
- [x] Exact public DNS hosts, HTTPS port, and method
- [x] Trusted process profiles instead of plugin executable paths/raw commands
- [x] Opaque credential handles and redacted audit
- [x] Explicit final-path verification requirement
- [x] 32 focused security-policy tests
- [x] No executor, transport, protocol message, or resource operation
- [ ] Restricted Windows worker identity
- [ ] Adversarial filesystem, network, process, registry, and credential denial
  harness

### Increment 9: Windows restriction proof — next

- AppContainer or equivalently enforceable restricted identity prototype
- Self-contained worker launch under that identity
- Authenticated private broker IPC
- No worker internet capability
- Kill-on-close job and bounded process/resources
- Direct-access denial tests for user files, saves, launcher data, credentials,
  registry, arbitrary processes, and network
- No provider operations

### Reference plugins for alpha

1. Discord Rich Presence — proves lifecycle events and opt-in privacy
2. PCGamingWiki — proves cached metadata/link integration and rate limiting
3. Playnite Import — proves local importer and provenance mapping
4. SteamGridDB adapter — proves migration of an existing provider contract

### Post-alpha integration order

1. IGDB
2. RetroAchievements
3. Ludusavi
4. Nexus Mods
5. ProtonDB experimental/link-out
6. HowLongToBeat partnership/link-out

See [[Engineering/Architecture/Integration Feasibility Matrix|Integration Feasibility Matrix]].

## Phase 2 — Emulator support

### Increment 1: emulator profiles

- Emulator discovery and user-defined executable
- Version detection
- Command-line template and working directory
- Platform association
- BIOS/firmware requirement status without distributing firmware
- Profile export/import

### Increment 2: ROM import

- User-selected scan roots
- Extension and archive policy
- Stable file hash and platform detection
- Dry-run preview, duplicates, and exclusions
- Import provenance and reversible changes
- No ROM acquisition or copyrighted-content discovery

### Increment 3: launch and metadata

- Profile-based launch actions
- Per-game overrides
- Artwork and metadata provider pipeline reuse
- RetroAchievements hash mapping
- Controller and fullscreen handoff
- Save-location discovery hooks for cloud saves

### Initial emulator targets

- RetroArch
- Dolphin
- PCSX2
- RPCS3

Add Ryujinx-compatible forks, Citra-compatible forks, Xenia, and others after
the profile contract is stable rather than hard-coding emulator-specific logic.

## Phase 3 — Seamless cloud saves

### Increment 1: local save engine

- Save-location discovery using manual overrides and Ludusavi manifests
- Pre-launch restore preview
- Post-exit quiet period and file-stability check
- Snapshot manifest with relative paths, sizes, timestamps, and hashes
- Atomic staging and restore
- Local version history and retention
- Dry-run, backup-before-restore, and recovery tests

### Increment 2: identity and conflict safety

- Stable user, device, game, and snapshot IDs
- Per-game generation/head tracking
- Device lease while a game is active
- Three-way conflict detection
- Never silently overwrite divergent saves
- Keep-both, choose-local, choose-remote, and inspect actions
- Cross-platform path mapping, including Proton prefixes

### Increment 3: encrypted transport abstraction

- Provider-neutral snapshot store
- Chunked/resumable upload and download
- Compression before encryption
- End-to-end authenticated encryption
- Master key wrapped by the operating-system credential store
- Integrity verification before restore
- Local folder/NAS reference transport

### Increment 4: Tailscale transport

- [x] Validate and persist an explicitly entered Tailscale IPv4/IPv6 peer address without enabling transfer
- [x] Pair a stable NovaLauncher device identity; an IP address alone is never trusted as identity
- [x] Require explicit per-game save-directory mapping for manual games and hard-exclude Steam imports
- [x] Detect local Tailscale availability without reading credentials or admin API tokens
- [x] Discover the explicitly configured NovaLauncher peer
- [x] Mutual device authentication above the VPN transport
- [x] Direct encrypted peer transfer
- Optional always-on relay/NAS node
- [x] Offline queue and startup retry
- Do not require tailnet administrator API tokens for ordinary users
- Treat Taildrop as experimental because it is not a synchronization database

### Increment 5: seamless lifecycle

- [x] Pull/check before launch
- [x] Block launch only for actionable conflicts or unsafe restore failure
- Mark active session and renew device lease
- [x] Monitor the directly launched executable process
- [x] Snapshot and push after process exit and save-file quiet period
- [x] Surface pending/offline/conflict state without data loss
- [x] Resume pending transfer after restart

### Cloud-save exit gate

- Power loss during upload cannot corrupt the last good snapshot
- Interrupted restore leaves the original local save recoverable
- Two devices editing offline create a conflict, never silent last-write-wins
- Malicious archives cannot escape the staging directory
- Transport provider cannot read plaintext when end-to-end encryption is on

## Phase 4 — Authorized acquisition and installation

### Supported boundaries

- Official store/library APIs
- User-owned installer files
- Authorized direct-download URLs
- Open-source/freeware/homebrew catalogs with distribution permission
- Mod downloads through authorized provider APIs
- Existing-install and Playnite import

NovaLauncher will not integrate piracy, repack, cracked-game, DRM-bypass, or
copyright-circumvention sources.

### Increment 1: download manager

- Provider-neutral download contract
- Resume, retry, rate limit, progress, and cancellation
- Expected size and disk-space validation
- Hash/signature verification
- Download quarantine and malware-scanner hook

### Increment 2: safe installer pipeline

- Staging directory
- Archive type allowlist
- Path traversal, symlink, and decompression-bomb protection
- User confirmation before executable launch
- Installer process tracking
- Rollback/cleanup after failure
- Never request elevation without explicit user action

### Increment 3: post-install onboarding

- Detect executable and working directory
- Import as a staged library mutation
- Resolve metadata and artwork
- Offer save-location discovery
- Preserve provider, license, receipt, and installer provenance

### Increment 4: provider plugins

- Official store adapters as terms permit
- Nexus Mods for mods, not full game distribution
- Homebrew/open-source catalog reference plugin
- User-provided installer reference plugin

## Alpha release recommendation

Ship `0.5.0-alpha.1` when:

- Plugin contracts and lifecycle are stable
- Local plugin install/enable/disable/update rollback works
- Permissions are visible and enforced to the extent promised
- Discord, PCGamingWiki, and Playnite Import reference plugins work
- Emulator profile and ROM-import MVP works for at least two emulators
- Cloud-save local snapshot engine is available behind an experimental flag
- Acquisition is limited to user-owned installers/imports

Do not block the first alpha on every external integration, Tailscale sync, or
full download management. Those features need real-world alpha feedback and
independent security review.

## Related

- [[Planning/Roadmap/Master Roadmap|Master Roadmap]]
- [[Planning/Releases/Versions|Versions]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Product/Epics/Cloud Saves|Cloud Saves]]
- [[Product/Epics/Emulator Support|Emulator Support]]
