---
type: implementation-report
status: complete
created: 2026-07-30
updated: 2026-07-30
increment: 1
feature: "[[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]"
---

# Plugin SDK Increment 1 Report

## Outcome

Increment 1 establishes a compile-safe Plugin SDK contract and package
inspection boundary without changing launcher runtime behavior. The launcher
does not reference the SDK and does not discover, install, extract, load, or
execute third-party code.

## Source changes

### Created

- `NovaLauncher.PluginSdk` dependency-free `net9.0` project
- Lifecycle, context, logging, and progress abstractions
- Semantic-version parser, precedence rules, and SDK version constant
- Manifest schema, capability and permission catalogs, serializer, and
  validator
- Read-only `.novaplugin` package validator and configurable resource limits
- Plugin contract test harness
- SDK package README
- 34 focused test cases
- `NovaLauncher/docs/PluginSdk.md`

### Modified

- `NovaLauncher.sln` includes the SDK project in Debug and Release.
- `NovaLauncher.Tests` references the SDK for contract tests.
- Source architecture and changelog documentation record the implemented
  boundary.

No launcher application source file or project reference changed.

## Contract decisions

- SDK version: `1.0.0-alpha.1`
- Package format: ZIP-compatible `.novaplugin`
- Required layout: root `plugin.json`; declared entry assembly in `lib/`
- Plugin IDs: lowercase namespaced identifiers
- Compatibility: inclusive minimum SDK and optional exclusive maximum SDK
- Inspection: read-only; no extraction or assembly loading
- Trust: capability and permission declarations are disclosure only

## Verification

- Debug solution build: passed, 0 errors, 4 pre-existing nullable warnings in
  `CinematicHero.axaml.cs`
- Debug tests: 197 passed, 0 failed
- Release solution build: passed, 0 errors, 0 warnings
- Release tests: 197 passed, 0 failed
- SDK NuGet package: created successfully with embedded README
- Debug launcher: responding, nonzero main-window handle, title
  `NovaLauncher`
- Release launcher: responding, nonzero main-window handle, title
  `NovaLauncher`
- Source whitespace audit: passed
- Brain internal-link audit: 123 Markdown notes, 754 wiki links, 0 broken
  links
- Source commit: `65abd5a` (`feat: establish plugin SDK contracts`)

## Remaining risks

- The SDK is an alpha preview; binary compatibility and deprecation policy are
  not final.
- In-process .NET plugins run with launcher/user permissions. A manifest cannot
  sandbox them.
- Package validation does not establish publisher identity or trust.
- Runtime dependency conflicts, unload behavior, native code, and plugin crash
  containment are not tested because execution is not enabled.
- Hash/signature verification, inventory, install/update rollback, quarantine,
  and health state remain unimplemented.
- Provider-specific operation contracts, configuration, secrets, and typed
  runtime failures remain later work.

## Recommended next step

Design Increment 2 as a persistent plugin inventory and lifecycle state
machine for validated local packages. Prove atomic install, enable, disable,
uninstall, quarantine, rollback, health, and hash-verification behavior before
allowing any third-party assembly to load.

## Related

- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[AI/AI Context|AI Context]]
