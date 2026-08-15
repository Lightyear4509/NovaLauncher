---
type: architecture
status: removed
related_epic: "[[Product/Epics/Plugins|Plugins]]"
created: 2026-07-29
updated: 2026-08-13
---

# Plugin System

> Removed architecture. NovaLauncher will use reviewed, first-party provider
> adapters and will not load third-party or downloaded code. See
> [[docs/decisions/ADR-0004-remove-plugins-add-first-party-achievements|ADR-0004]].

## Purpose

Provide stable extension points so importers, artwork providers, metadata providers, game actions, widgets, themes, and future integrations can be added without coupling them to the core application.

## Initial boundaries

- `PluginService` coordinates plugin-facing application behavior.
- `PluginManager` is responsible for discovery and lifecycle.
- Plugins depend on a documented SDK rather than internal implementation details.
- Permissions must be explicit before marketplace distribution.

## Package contract

Each package requires:

- immutable plugin ID
- semantic version
- author, license, homepage, and support URL
- SDK and launcher compatibility range
- declared extension points and capabilities
- entry assembly and entry type
- package hash and optional signature
- dependency declarations

## Implemented foundation

Increment 1 establishes the SDK boundary without changing launcher runtime
behavior:

- `NovaLauncher.PluginSdk` is independent from the launcher application and
  has no third-party package dependencies.
- The manifest schema covers immutable identity, semantic version, author,
  license, homepage, SDK range, entry assembly/type, capabilities, and
  permissions.
- `.novaplugin` archives use a root `plugin.json` and a `lib/` entry assembly.
- Validation is read-only. It does not extract archives, load assemblies, or
  execute plugin code.
- Unsafe archive paths, symbolic links, duplicate paths, excessive resource
  use, schema drift, and incompatible SDK ranges are rejected.
- Manifest capabilities and permissions are disclosure contracts, not an
  in-process security boundary.

Increment 2 adds a separate management layer:

- `NovaLauncher.PluginManagement` references the SDK but is not referenced by
  the launcher application.
- `inventory.json` is schema-versioned, validated before replacement, and
  recoverable from a last-valid backup.
- Installation validates and hashes the exact staged bytes before moving a
  versioned package into managed storage.
- Lifecycle and health changes persist before the new in-memory snapshot is
  published.
- Enable and rollback verify SHA-256 integrity; missing or changed packages
  are quarantined.
- Updates retain older versions and rollback always returns to disabled state.
- Uninstall deletes only package files named by the inventory.

The management layer models installation and eligibility only. It does not
discover arbitrary directories, extract packages, or load plugin code.

Increment 3 adds developer-only surfaces:

- `IPluginOperationProvider<TRequest, TResult>` provides one generic operation
  boundary with success, no-result, typed failure, progress, and cancellation
  semantics.
- The contract harness verifies provider identity and manifest capability
  declaration.
- `PluginPackageBuilder` creates ordered, fixed-timestamp archives, validates
  the completed package, and returns SHA-256.
- `NovaLauncher.PluginTool` exposes local validate, pack, and disabled-install
  commands.
- The starter template and sample presence plugin compile against public SDK
  contracts.

None of these projects are referenced by the launcher application.

Increment 4 adds the pre-execution catalog trust chain:

- `NovaLauncher.PluginCatalog` references the SDK and management layers but is
  not referenced by the launcher application.
- Canonical catalog documents and exact package bytes use RSA-PSS/SHA-256
  signatures.
- Catalog and publisher keys are scoped by exact ID, owner, and separate
  purpose.
- Catalog freshness, key validity/revocation, HTTPS sources, package bounds,
  hashes, signatures, and signed manifests are validated before consent.
- GitHub Release retrieval records owner, repository, tag, asset, URI, and
  fetch time as provenance.
- Consent receives the exact validated capabilities, permissions, publisher
  key, source provenance, update state, and previous version.
- Installs and updates remain disabled; failed persistence preserves the prior
  inventory and explicit rollback verifies the retained package hash.

Increment 5 adds the non-executing process boundary:

- `NovaLauncher.PluginRuntime.Protocol` defines strict, length-prefixed,
  versioned control messages with no generic command or payload.
- `NovaLauncher.PluginRuntime` creates authenticated sessions, validates exact
  session/correlation identity, applies deadlines, and supervises process
  termination.
- `NovaLauncher.PluginHost` accepts only handshake, health, shutdown, and
  bounded error exchanges.
- A private 256-bit token is inherited outside the command line, removed by
  the worker, and compared in fixed time.
- Timeout, protocol failure, or broken transport faults the session and kills
  the worker process tree.
- Lifecycle and catalog staging cleanup is age-limited, non-recursive,
  suffix-specific, and refuses reparse-point roots.

The launcher references none of these projects. The worker receives no plugin
package and cannot load or execute an assembly.

Increment 6 adds isolated managed execution behind that boundary:

- `PluginRuntimeCoordinator` accepts only enabled inventory-owned active
  packages and rechecks their exact SHA-256.
- The typed handoff discloses exact package identity, version, hash, entry
  point, and session extraction root.
- The worker independently reruns package, SDK, hash, manifest, and runtime
  identity checks.
- Native permission, non-managed DLLs, and metadata-declared P/Invoke fail
  closed.
- Validated files extract beneath a new session-owned root.
- One collectible load context loads one managed plugin per worker.
- Initialization and shutdown use deadline-bound protocol exchanges.
- Success resets persisted health; load, initialization, health, and shutdown
  failures increment the lifecycle counter and trigger existing quarantine.
- Process termination is the definitive unload fallback.

At the end of Increment 6, no provider operation or launcher integration
existed.

Increment 7 adds the trusted launcher integration:

- one `PluginManagerService` owns inventory, catalog, trust, installation,
  lifecycle, and running worker handles
- the complete host payload is deployed beneath the launcher while the host
  assembly is not referenced in-process
- startup remains opt-in and false by default
- enabled and running remain separate states
- the command center exposes signed refresh, exact consent, install/update,
  enable/start, stop/disable, quarantine restore, rollback, and confirmed
  uninstall
- progress and cancellation remain end-to-end
- no catalog can refresh until a real source and trusted keys are configured

The launcher references trusted plugin coordination projects but never loads a
third-party plugin assembly. No provider-operation protocol exists.

Increment 8 adds policy without authority:

- `NovaLauncher.PluginBroker` is isolated from launcher and worker
- resource kinds have separate typed requests rather than a generic payload
- policy binds exact plugin identity/version, manifest permissions, local
  roots, exact HTTPS destinations, trusted process profiles, opaque credential
  handles, game save scopes, quotas, timeouts, and validity
- every operation requires exact short-lived consent
- authorization is pure and deny-by-default
- audit records omit resource locators, parameters, handles, and secrets
- file/save allowance still requires final-handle verification

No executor, transport, host message, launcher registration, or resource
operation exists. A Windows restricted-identity prototype remains the next
gate.

## Lifecycle

1. Download or select package.
2. Stage outside the active plugin directory.
3. Validate manifest, compatibility, hash, signature, and archive paths.
4. Show capabilities and request consent.
5. Install atomically in disabled state. — Increment 2 foundation complete
6. Start an authenticated supervised worker. — non-executing Increment 5
   foundation complete
7. Load a verified managed plugin inside its worker. — Increment 6 complete
8. Monitor health and log failures.
9. Quarantine repeated startup failures.
10. Roll back a failed update.

## Trust and isolation

NovaLauncher will not load third-party plugin assemblies into the launcher
process. One supervised worker per plugin is the planned crash and dependency
isolation boundary.

A normal child process still inherits the current user's filesystem, network,
process, credential, and native-code authority. The versioned control protocol
does not claim to be a sandbox. Resource-bearing operations require brokered
capabilities or a separately proven OS-level sandbox, and native code remains
outside the alpha plan.

## Initial extension points

- metadata and artwork providers
- library importers
- game actions and lifecycle listeners
- presence and achievements
- save discovery and cloud transport
- mod services
- emulator profiles
- settings panels

## Foundation work

- [x] Define the plugin contract and lifecycle.
- [x] Define capability and permission disclosure boundaries.
- [x] Establish semantic-version compatibility rules.
- [x] Add strict manifest and package validation.
- [x] Add a compatibility test harness.
- [x] Define and implement the bounded non-executing out-of-process control
  boundary.
- [ ] Define brokered resource APIs or an enforceable OS sandbox.
- [x] Define deny-by-default broker policy and authorization contracts without
  enabling resource operations.
- [ ] Prove a Windows restricted worker identity and adversarial denial
  harness.
- [x] Define persistent inventory, enablement, disablement, health,
  quarantine, rollback, and uninstall behavior.
- [x] Add staged package installation and SHA-256 verification.
- [x] Add signatures and publisher trust.
- [ ] Add dependency resolution and cross-process inventory coordination.
- [x] Define runtime discovery and loading behavior.
- [x] Build starter template, package tooling, compatibility tests, and a
  sample presence plugin.
- [x] Document alpha versioning, deprecation, and migration policy.
- [x] Add a local validate, pack, and disabled-install developer workflow.
- [x] Build the curated signed-catalog, publisher verification, staged
  download, exact consent, and install/update rollback foundation.
- [x] Integrate catalog browsing and consent UI into the launcher.
- [x] Add authenticated host supervision, health, deadlines, termination, and
  safe staging cleanup without plugin execution.
- [x] Add verified-package handoff and isolated managed loading.

## Related

- [[Product/Epics/Plugins|Plugins epic]]
- [[Engineering/Services/Plugin Service|Plugin Service]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Engineering/Architecture/Integration Feasibility Matrix|Integration Feasibility Matrix]]
- [[AI/Plugin SDK Increment 1 Report|Plugin SDK Increment 1 Report]]
- [[AI/Plugin SDK Increment 2 Report|Plugin SDK Increment 2 Report]]
- [[AI/Plugin SDK Increment 3 Report|Plugin SDK Increment 3 Report]]
- [[AI/Plugin SDK Increment 4 Report|Plugin SDK Increment 4 Report]]
- [[AI/Plugin SDK Increment 5 Report|Plugin SDK Increment 5 Report]]
- [[AI/Plugin SDK Increment 6 Report|Plugin SDK Increment 6 Report]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/Plugin SDK Increment 8 Report|Plugin SDK Increment 8 Report]]
