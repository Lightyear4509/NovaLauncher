---
type: implementation-report
status: complete
created: 2026-07-30
updated: 2026-07-30
increment: 6
feature: "[[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]"
---

# Plugin SDK Increment 6 Report

## Outcome

Increment 6 adds verified managed loading inside one supervised worker per
plugin. Only explicitly enabled, inventory-owned active packages are eligible.
The trusted coordinator and worker independently verify package integrity and
identity before session-scoped extraction and managed loading.

Initialization, health, and shutdown failures feed the existing persisted
lifecycle failure counter and quarantine threshold. Timeout, protocol failure,
or unload failure terminates the worker process as the definitive fallback.

The launcher application still references no plugin project. There is no
catalog UI, runtime startup integration, or provider-operation protocol.

## Source changes

### Created

- Typed `PluginPackageHandoff`
- Explicit load, loaded, initialize, initialized, plugin-shutdown, and
  plugin-stopped protocol messages
- `PluginRuntimeCoordinator`
- `RunningPluginHandle`
- Typed runtime start and operation results
- Worker-side package loader and lifecycle owner
- Collectible plugin assembly load context
- Worker plugin context with bounded diagnostics
- Managed/native inspection using PE metadata
- Safe session extraction and cleanup
- Compile-tested runtime failure/timeout probe
- 14 focused managed-runtime tests

### Modified

- Runtime protocol validates exact typed package fields and rejects package
  data on every other message.
- Supervisor sessions expose load, initialize, and plugin-shutdown exchanges.
- Caller cancellation now terminates the worker before propagation so the
  control stream cannot remain desynchronized.
- Lifecycle management exposes its validated managed-package path resolver.
- Runtime and host projects reference only the layers required for isolated
  coordination and SDK loading.
- Solution and tests include the runtime probe.
- Runtime architecture, SDK, developer, catalog, architecture, and changelog
  documentation describe the actual boundary.

No launcher application source file or launcher project reference changed.

## Runtime flow

1. Require an installed plugin in persisted `Enabled` state.
2. Select its exact inventory-owned active version.
3. Reject declared `NativeCode`.
4. Resolve the managed package path and recompute trusted-side SHA-256.
5. Start and authenticate one worker.
6. Send exact identity, version, hash, entry point, package path, and
   session-root handoff.
7. Recompute hash and rerun package, SDK, manifest, and identity validation in
   the worker.
8. Reject non-managed DLLs and metadata-declared P/Invoke.
9. Extract validated entries beneath a new session-owned root.
10. Load one managed plugin in a collectible context.
11. Require concrete `INovaPlugin`, public parameterless construction, and
    exact runtime/manifest ID.
12. Initialize through a deadline-bound exchange.
13. Reset persisted health after successful initialization.
14. Health-check and shut down through explicit protocol messages.
15. Unload the context, terminate the worker if necessary, and clean the
    session root.

## Verification

- Debug solution rebuild: passed, 0 errors, 4 pre-existing nullable warnings
- Debug tests: 331 passed, 0 failed
- Release solution rebuild: passed, 0 errors, 4 pre-existing nullable warnings
- Release tests: 331 passed, 0 failed
- Managed sample load/initialize/health/shutdown/unload workflow: passed
- Initialization failure and persisted failure recording: passed
- Initialization timeout, forced worker termination, and cleanup: passed
- Shutdown failure and persisted failure recording: passed
- Failure-threshold quarantine: passed
- Native permission and native binary rejection: passed
- Runtime identity mismatch rejection: passed
- SDK package: `NovaLauncher.PluginSdk.1.0.0-alpha.1.nupkg` created
  successfully
- Debug launcher responsive-window test: passed
- Release launcher responsive-window test: passed
- Source whitespace audit: passed
- Brain internal-link audit: 128 Markdown notes, 828 wiki links, 0 broken
  links
- Source commit: `aa0c02c` (`feat: add isolated managed plugin loading`)

## Remaining risks

- Managed/native inspection is defense in depth, not an OS sandbox. Managed
  code can still reach user-authorized framework and operating-system APIs.
- There are no brokered filesystem, network, process, credential, save-data,
  or secret capabilities.
- The launcher does not yet own or compose catalog, inventory, consent, or
  runtime services.
- There is no catalog, enablement, health, quarantine, recovery, or safe-mode
  UI.
- Provider-operation messages remain disabled.
- Plugin dependency/version policy is limited to process isolation and normal
  managed resolution.
- There is no automatic restart or launcher-crash recovery for running
  workers.
- Root-key rotation and emergency revocation delivery remain operational work.

## Recommended next step

Plan Increment 7 as a launcher-owned plugin service and catalog/consent UI.
Use one owner for inventory, catalog, and runtime state; keep startup opt-in and
off the UI thread; surface disabled, enabled, running, failed, and quarantined
state; preserve exact signed consent; and provide explicit stop/recovery.
Resource-bearing provider operations must remain disabled until broker
contracts or OS restrictions are separately approved.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[AI/AI Context|AI Context]]
- [[AI/Plugin SDK Increment 5 Report|Plugin SDK Increment 5 Report]]
