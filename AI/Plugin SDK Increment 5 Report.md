---
type: implementation-report
status: complete
created: 2026-07-30
updated: 2026-07-30
increment: 5
feature: "[[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]"
---

# Plugin SDK Increment 5 Report

## Outcome

Increment 5 establishes a separate, authenticated, supervised plugin-host
process without enabling plugin execution. The control protocol is versioned,
strict, bounded, and allowlisted. The supervisor applies deadlines,
correlation validation, cancellation, health checks, graceful shutdown, and
forced process-tree termination.

Lifecycle and catalog staging areas now remove abandoned old files through a
non-recursive, suffix-specific, reparse-aware cleanup boundary.

The launcher application references no plugin runtime project. The worker
receives no package path and cannot discover, extract, load, initialize, or
execute a plugin.

## Source changes

### Created

- `NovaLauncher.PluginRuntime.Protocol` dependency-free class library
- Protocol version, limits, message types, strict validator, and framed codec
- `NovaLauncher.PluginRuntime` trusted supervisor class library
- Typed start and operation results
- Explicit runtime session state
- Authenticated process startup and exact handshake validation
- Health, shutdown, timeout, cancellation, and termination coordination
- `NovaLauncher.PluginHost` non-executing console worker
- Safe lifecycle and catalog staging cleanup service
- Runtime-host architecture documentation
- 29 focused runtime-boundary tests

### Modified

- Solution includes the protocol, runtime, and host projects in Debug and
  Release.
- Tests reference all three runtime projects.
- Plugin lifecycle options include a bounded stale-stage age.
- The lifecycle manager cleans its staging area before first inventory load
  and exposes the cleanup result.
- Catalog installation cleans abandoned catalog downloads before creating its
  new stage.
- Architecture, SDK, catalog, developer, and changelog documentation describes
  the new boundary.

No launcher application source file or launcher project reference changed.

## Protocol and host decisions

- Frames use a four-byte big-endian length and strict camel-case JSON.
- The default message limit is 64 KiB; configured limits must remain between
  1 KiB and 1 MiB.
- Protocol version 1 contains only handshake, ready, ping, pong, shutdown,
  stopped, and bounded error messages.
- Every message requires exact `N`-format session and correlation GUIDs.
- There is no generic command or arbitrary payload field.
- A random 256-bit token is inherited outside the command line, removed by the
  worker immediately, and compared in fixed time.
- Unknown fields, versions, message types, malformed identifiers, invalid
  lengths, truncation, misplaced tokens, and unbounded errors fail closed.
- Timeout or protocol failure faults the session and kills the worker process
  tree.

## Staging cleanup decisions

- Cleanup never recurses.
- Only the configured `.novaplugin.tmp` or `.novaplugin.download` suffix is
  eligible.
- Recent, unrelated, nested, and reparse-point entries are retained.
- A reparse-point root is refused.
- Paths are normalized and must remain beneath the exact configured root.
- The default lifecycle stale age is one day and configuration is bounded to
  30 days.
- Caller cancellation propagates and inspection/deletion failures are returned
  as warnings.

## Verification

- Debug solution rebuild: passed, 0 errors, 4 pre-existing nullable warnings
- Debug tests: 317 passed, 0 failed
- Release solution rebuild: passed, 0 errors, 4 pre-existing nullable warnings
- Release tests: 317 passed, 0 failed
- Real Debug and Release host startup/handshake/health/shutdown tests: passed
- SDK package: `NovaLauncher.PluginSdk.1.0.0-alpha.1.nupkg` created
  successfully
- Debug launcher responsive-window test: passed
- Release launcher responsive-window test: passed
- Source whitespace audit: passed
- Brain internal-link audit: 127 Markdown notes, 814 wiki links, 0 broken
  links
- Source commit: `e75177a` (`feat: add supervised plugin host foundation`)

## Remaining risks

- A normal worker process still has the current user's operating-system
  authority. It is not an enforceable permission sandbox.
- Verified-package handoff, safe extraction, assembly loading, initialization,
  shutdown, and provider operations remain unimplemented.
- Worker failures are typed but are not yet connected to persisted lifecycle
  health and quarantine.
- There is no broker for filesystem, network, process, credential, or save-data
  access.
- Native plugin code is not technically blocked because no loading exists yet;
  Increment 6 must reject it before execution is introduced.
- Inventory remains process-local and the launcher has no catalog, consent, or
  runtime UI.
- Catalog trust-root rotation and emergency revocation delivery remain
  operational follow-ups.

## Recommended next step

Plan Increment 6 as verified managed-package handoff and isolated loading
inside one worker per plugin. Reverify the installed package, reject native
code, extract safely into a private session root, apply initialization and
shutdown deadlines, connect failures to lifecycle quarantine, and use process
termination as the definitive unload fallback. Brokered capability contracts
must precede resource-bearing provider operations.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[AI/AI Context|AI Context]]
- [[AI/Plugin SDK Increment 4 Report|Plugin SDK Increment 4 Report]]
