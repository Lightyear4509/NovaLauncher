---
type: implementation-report
status: complete
created: 2026-07-30
updated: 2026-07-30
increment: 3
feature: "[[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]"
---

# Plugin SDK Increment 3 Report

## Outcome

Increment 3 establishes a usable local developer workflow around the alpha SDK:
typed provider operations, deterministic package creation, validation and
disabled-install commands, an installable starter template, and a
compile-tested sample plugin. NovaLauncher still references none of these
developer projects and executes no plugin code.

## Source changes

### Created

- Generic provider-operation interface
- Typed operation status, failure, retry, success, and no-result contracts
- Compatibility/deprecation descriptors and validation policy
- Deterministic `.novaplugin` package builder
- Package-build inputs and typed result
- `NovaLauncher.PluginTool` validate, pack, and disabled-install CLI
- Installable `novalauncher-plugin` .NET template
- Compile-tested sample presence plugin
- Developer guide
- Versioning, deprecation, and migration policy
- 31 focused operation, builder, CLI, template, and sample test cases

### Modified

- Contract harness validates provider identity and manifest capability.
- Package validator and builder share one archive-path policy.
- Solution includes the CLI and sample in Debug and Release.
- Tests reference the CLI and sample.
- SDK, lifecycle, architecture, and changelog documentation describe the
  developer boundary.

No launcher application source file or launcher project reference changed.

## Developer workflow

1. Install the repository template.
2. Generate a project with a namespaced plugin ID.
3. Implement `INovaPlugin` and optional operation-provider contracts.
4. Build the entry assembly.
5. Pack it through the validated package builder or CLI.
6. Validate the completed `.novaplugin`.
7. Record it as disabled in an isolated local lifecycle directory.

The real .NET template engine successfully installed the template, generated a
renamed project, and replaced the plugin ID in both source and manifest.

## Contract decisions

- Success, no-result, and failure are distinct outcomes.
- Failure codes are machine-readable and should remain stable.
- `RetryAfter` is valid only for transient or rate-limited failures.
- Caller cancellation propagates as `OperationCanceledException`.
- Provider ID must match manifest ID exactly.
- Provider capability must be declared in the manifest.
- Package output must use `.novaplugin`.
- Identical builder inputs are byte-identical under the current SDK/runtime.
- Local install remains disabled and never loads a plugin.

## Verification

- Debug solution build: passed, 0 errors, 4 pre-existing nullable warnings
- Debug tests: 258 passed, 0 failed
- Release solution build: passed, 0 errors, 4 pre-existing nullable warnings
- Release tests: 258 passed, 0 failed
- SDK package: `NovaLauncher.PluginSdk.1.0.0-alpha.1.nupkg` created
  successfully
- Sample CLI pack/validate/install workflow: passed; the package was recorded
  disabled and never executed
- Real template install/create workflow: passed
- Debug launcher responsive-window test: passed
- Release launcher responsive-window test: passed
- Source whitespace audit: passed
- Brain internal-link audit: 125 Markdown notes, 784 wiki links, 0 broken
  links
- Source commit: `87098a7` (`feat: add plugin developer tooling`)

## Remaining risks

- The SDK package and template are not published; template builds currently
  require a local NuGet source.
- Alpha contracts are not yet binary-stable.
- Deterministic bytes are tested only under identical inputs and the current
  .NET runtime implementation.
- SHA-256 establishes integrity, not publisher identity.
- The CLI has no signing, catalog provenance, download, or consent flow.
- Runtime dependency conflicts, unload, crash containment, and sandboxing
  remain unimplemented because execution stays disabled.

## Recommended next step

Plan Increment 4 as a curated catalog and publisher-verification boundary:
signed index format, package checksum/signature policy, catalog provenance,
permission consent, download staging, and failed-update rollback. Continue to
defer runtime loading until its isolation design is separately approved.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[AI/AI Context|AI Context]]
- [[AI/Plugin SDK Increment 2 Report|Plugin SDK Increment 2 Report]]
