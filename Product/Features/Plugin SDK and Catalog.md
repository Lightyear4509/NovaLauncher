---
type: feature
status: removed
priority: none
related_epic: "[[Product/Epics/Plugins|Plugins]]"
release: alpha
created: 2026-07-30
updated: 2026-08-13
progress: 0
---

# Plugin SDK and Catalog

> Removed from the product plan by
> [[docs/decisions/ADR-0004-remove-plugins-add-first-party-achievements|ADR-0004]].
> The material below is retained only as historical research and must not be
> treated as implementation scope.

## Goal

Allow users to customize NovaLauncher and install integrations without giving
plugins undocumented access to launcher internals.

## Alpha scope

- Stable SDK contracts
- Manifest and compatibility range
- Package validation
- Local install, enable, disable, uninstall
- Curated catalog index
- Permission/capability disclosure
- Failure quarantine and rollback
- SDK templates, samples, packaging, and tests

## Implemented — Increment 1

- Isolated, dependency-free `NovaLauncher.PluginSdk` project at
  `1.0.0-alpha.1`
- Cancellable lifecycle, structured logging, and progress contracts
- Strict semantic-version parsing and SDK compatibility ranges
- Versioned manifest with namespaced identity, entry point, capabilities, and
  permission disclosure
- Read-only `.novaplugin` ZIP validation with archive traversal, symbolic-link,
  duplicate-path, manifest-size, entry-count, and uncompressed-size defenses
- Contract harness that checks exact manifest/runtime identity
- Packable NuGet SDK project with embedded README
- 34 focused test cases

The launcher does not discover, install, load, or execute plugins yet. This
keeps Increment 1 isolated from the working application.

## Implemented — Increment 2

- Isolated `NovaLauncher.PluginManagement` project; the launcher application
  still has no plugin-project reference
- Schema-versioned inventory with validated atomic writes, last-valid backup
  recovery, and invalid-primary preservation
- Exact staged-byte package validation and SHA-256 hashing
- Versioned managed package storage, idempotent reinstalls, and immutable
  version/hash conflict detection
- Persist-before-publish enable, disable, health, quarantine, explicit restore,
  rollback, and uninstall transitions
- Configurable consecutive-failure quarantine threshold
- Hash verification before enable or rollback
- Previous package versions retained for rollback
- Conservative uninstall that deletes only inventory-owned package files
- 30 focused lifecycle and persistence test cases

“Enabled” is persisted eligibility for future activation. No plugin assembly is
loaded, initialized, or executed in this increment.

## Implemented — Increment 3

- Generic provider-operation contract with explicit success, no-result, and
  typed failure outcomes
- Stable failure codes, failure categories, retry eligibility, and optional
  `RetryAfter`
- Compatibility/deprecation descriptors and consistency validation
- Deterministic package builder for identical inputs within the current
  SDK/runtime
- Completed-package validation and SHA-256 output
- Local developer CLI for validate, pack, and disabled install
- Installable `dotnet new` starter template with configurable namespaced ID
- Compile-tested sample presence plugin
- Provider identity/capability checks in the contract harness
- Developer handbook, versioning policy, and migration guidance
- 31 focused operation, builder, CLI, template, and sample test cases

The real .NET template engine successfully installs and instantiates the
starter template. Local CLI install records a validated disabled package and
still performs no plugin execution.

## Implemented — Increment 4

- Isolated `NovaLauncher.PluginCatalog` project; the launcher still references
  no plugin project
- Canonical catalog schema and deterministic signing input
- RSA-PSS/SHA-256 catalog and exact-package signatures with minimum 2048-bit
  keys
- Separate trust-store purposes for catalog owners and package publishers
- Catalog expiration, key validity, revocation, ownership, and duplicate-entry
  validation
- Exact manifests, HTTPS URLs, sizes, SHA-256 hashes, publisher IDs, and
  signatures in the signed disclosure
- Bounded GitHub Release catalog source with explicit provenance
- Bounded HTTPS package transport with progress and cancellation
- Staged size, checksum, publisher signature, package, SDK, and signed-manifest
  verification
- Permission/capability consent only after cryptographic and structural
  validation
- Disabled install/update, previous-version retention, persistence-failure
  preservation, and explicit rollback
- 30 focused catalog, cryptography, transport, consent, update, and rollback
  test cases

This is the catalog security foundation, not an in-launcher marketplace UI.
No downloaded assembly is loaded or executed.

## Implemented — Increment 5

- Dependency-free, versioned plugin-host control protocol
- Four-byte length framing, strict JSON, bounded messages, and fail-closed
  validation
- Unique session and correlation IDs
- Private 256-bit authentication token inherited outside the command line and
  compared in fixed time
- Non-executing `NovaLauncher.PluginHost` worker
- Trusted `NovaLauncher.PluginRuntime` process supervisor
- Startup, operation, and shutdown deadlines
- Health checks, caller cancellation, typed failures, and forced process-tree
  termination
- Exact allowlist containing only handshake, ready, ping, pong, shutdown,
  stopped, and bounded error messages
- Age-based, non-recursive cleanup for exact lifecycle and catalog staging
  suffixes
- Reparse-point cleanup refusal and observable cleanup results
- 29 focused protocol, host, supervisor, timeout, cancellation, and cleanup
  test cases

The launcher references none of the runtime projects. The worker receives no
package path and cannot discover, extract, load, initialize, or execute a
plugin.

## Implemented — Increment 6

- Typed verified-package handoff with exact identity, version, SHA-256, entry
  point, package path, and session root
- Runtime start restricted to explicitly enabled, inventory-owned active
  versions
- Trusted-side installed-byte revalidation before worker launch
- Independent worker-side hash, package, SDK, manifest, and identity validation
- Native permission, native binary, and metadata-declared P/Invoke rejection
- Session-scoped safe extraction
- One collectible managed load context inside one worker per plugin
- Exact `INovaPlugin` runtime identity enforcement
- Deadline-bound initialization and shutdown
- Health checks and definitive process-termination unload fallback
- Persisted success reset and load/initialize/health/shutdown failure recording
- Existing lifecycle threshold drives quarantine
- Compile-tested failure/timeout probe
- 14 focused handoff, managed loading, identity, native-code, timeout, cleanup,
  and quarantine tests

The launcher still references no plugin project. Provider operations and
resource access remain unavailable.

## Implemented — Increment 7

- One launcher-owned service composes inventory, trust, catalog, installation,
  lifecycle, runtime workers, and shutdown
- Complete plugin-host output is deployed beneath the launcher without an
  in-process host assembly reference
- Plugin command center added to the main navigation
- Explicit available, disabled, enabled, running, and quarantined states
- Signed catalog refresh remains locked until a real source and trust roots
  are configured
- Exact validated publisher, source, capability, permission, and update
  consent
- Install/update, enable, start, stop, disable, restore, rollback, and
  confirmed uninstall actions
- Cooperative progress and cancellation
- Automatic startup is opt-in and false by default
- A runnable-deployment test starts the launcher-shipped worker
- Nine focused launcher integration tests raise the full suite to 340

The launcher now references the trusted management, catalog, and runtime
coordination projects. It never loads third-party plugin assemblies. Provider
operations and resource access remain unavailable.

## Implemented — Increment 8

- Isolated `NovaLauncher.PluginBroker` trusted policy project
- Separate typed file, HTTPS network, process-profile, credential-handle, and
  game save-data requests
- Exact plugin ID, version, policy version, permission, scope, quota, timeout,
  and validity binding
- Disabled-by-default policy state
- Per-operation consent as the only supported consent mode
- Five-minute maximum consent receipts bound to request ID, the complete
  trusted policy, declared byte ceiling, and a length-prefixed SHA-256
  fingerprint
- Canonical local-path enforcement with UNC/device and alternate-stream
  rejection
- Exact public DNS host, HTTPS port, and method policy
- Trusted process profile IDs with bounded named parameters instead of
  executable paths or raw argument strings
- Opaque credential handles; no secret bytes in requests or audit records
- Redacted audit decisions with no resource locator or process parameter
- Pure invalid, denied, consent-required, rate-limited, and allowed decisions
- Explicit final-handle path-verification requirement for file/save allowance
- 32 focused policy, scope, consent, quota, redaction, and cancellation tests

The broker is not referenced by the launcher or worker and performs no
resource operation. The runtime protocol is unchanged.

## Extension points

- Metadata and artwork
- Library import
- Game actions and lifecycle
- Presence and achievements
- Save discovery and cloud transport
- Mods
- Emulator profiles
- Settings panels

Arbitrary replacement of core UI, persistence, dependency injection, or
security services is not an alpha extension point.

## Trust model

- The launcher process must never load third-party plugin assemblies.
- One worker process per plugin is the planned crash and dependency-isolation
  boundary.
- A normal worker still has the current user's OS authority; manifest
  permissions remain disclosure until brokered APIs or an OS sandbox enforce
  them.
- Native code remains prohibited for the alpha runtime plan.
- Downloaded packages require hash validation and explicit user consent.
- Curated does not mean infallible; disable and rollback must always remain
  available.

## Reference integrations

- Discord Rich Presence
- PCGamingWiki
- Playnite Import
- SteamGridDB

## Risks

- The alpha SDK is a contract preview, not a compatibility guarantee.
- Capability and permission declarations are disclosure only; they do not
  sandbox .NET code, even in a separate ordinary user process.
- Dependency isolation is one worker per plugin; dependency resolution and
  cross-process inventory locking remain unimplemented.
- Binary compatibility across SDK versions
- Malicious or abandoned plugins
- Secret storage and OAuth callback handling
- Provider rate limits and terms changes
- Provider-operation failure policy remains deferred because provider
  operations are not exposed.
- Cross-process inventory locking is not implemented.
- The local template consumes an alpha NuGet package that is not yet published.
- Deterministic package bytes are guaranteed only for identical inputs under
  the current SDK/runtime implementation.
- Root-key distribution, rotation, and emergency revocation delivery are not
  implemented.
- Catalog provenance is displayed context; cryptographic trust comes from the
  configured catalog key.
- Download retries and private GitHub authentication remain unimplemented.
- The authenticated private control stream is not an OS security sandbox.
- Brokered capabilities and enforceable OS restrictions are not implemented.
- Policy cannot prevent reparse-point escape, DNS rebinding, or direct worker
  OS access without a trusted executor and restricted worker identity.

## Related

- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Engineering/Architecture/Integration Feasibility Matrix|Integration Feasibility Matrix]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[AI/Plugin SDK Increment 1 Report|Plugin SDK Increment 1 Report]]
- [[AI/Plugin SDK Increment 2 Report|Plugin SDK Increment 2 Report]]
- [[AI/Plugin SDK Increment 3 Report|Plugin SDK Increment 3 Report]]
- [[AI/Plugin SDK Increment 4 Report|Plugin SDK Increment 4 Report]]
- [[AI/Plugin SDK Increment 5 Report|Plugin SDK Increment 5 Report]]
- [[AI/Plugin SDK Increment 6 Report|Plugin SDK Increment 6 Report]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/Plugin SDK Increment 8 Report|Plugin SDK Increment 8 Report]]
