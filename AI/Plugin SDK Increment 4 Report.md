---
type: implementation-report
status: complete
created: 2026-07-30
updated: 2026-07-30
increment: 4
feature: "[[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]"
---

# Plugin SDK Increment 4 Report

## Outcome

Increment 4 adds the security and lifecycle foundation for a curated plugin
catalog. NovaLauncher can now validate a signed catalog, retrieve it from an
exact GitHub Release asset, download a package into private staging, verify its
identity and contents, request exact permission consent, and record an approved
install or update as disabled. Previous versions remain available for explicit
rollback.

This increment does not add catalog UI or runtime loading. The launcher
application still references no plugin project and executes no third-party
code.

## Source changes

### Created

- `NovaLauncher.PluginCatalog` isolated class-library project
- Canonical catalog schema and strict serializer
- Separate catalog and publisher trust-key purposes
- RSA-PSS/SHA-256 signing and verification
- Trusted-key store with validity and revocation enforcement
- Catalog validation and structured validation issues
- Exact GitHub Release catalog source with provenance
- Bounded HTTPS package transport with progress and cancellation
- Staged catalog installer with checksum, signature, manifest, and SDK checks
- Exact install/update consent contracts
- Disabled installation, retained-version update, and explicit rollback flow
- Catalog architecture documentation
- 30 focused security, source, transport, installation, and rollback tests

### Modified

- Solution includes the catalog project in Debug and Release.
- Tests reference the catalog project.
- Architecture, SDK, developer guide, and changelog documentation describe the
  new trust boundary.

No launcher application source file or launcher project reference changed.

## Security flow

1. Fetch one named catalog asset from the latest public GitHub Release.
2. Parse strict JSON and reject unknown fields.
3. Canonicalize and verify the catalog signature against its catalog key.
4. Validate freshness, identifiers, manifests, SDK bounds, HTTPS package
   locations, sizes, hashes, and publisher-key metadata.
5. Download an exact package into private staging with bounded streaming,
   progress, and caller cancellation.
6. Verify exact length, SHA-256, publisher signature, archive safety, SDK
   compatibility, and every signed manifest disclosure.
7. Present exact capabilities, permissions, publisher, provenance, and update
   details for consent.
8. Record an approved package as disabled without loading it.
9. Retain the prior version and support hash-verified explicit rollback.
10. Remove staging files after every outcome.

Catalog signing keys and package-publisher signing keys are intentionally
separate trust purposes. Keys fail closed when missing, mismatched, weak,
expired, not yet valid, revoked, or malformed.

## Verification

- Debug solution rebuild: passed, 0 errors, 4 pre-existing nullable warnings
- Debug tests: 288 passed, 0 failed
- Release solution rebuild: passed, 0 errors, 4 pre-existing nullable warnings
- Release tests: 288 passed, 0 failed
- SDK package: `NovaLauncher.PluginSdk.1.0.0-alpha.1.nupkg` created
  successfully
- Debug launcher responsive-window test: passed
- Release launcher responsive-window test: passed
- Source whitespace audit: passed
- Brain internal-link audit: 126 Markdown notes, 799 wiki links, 0 broken
  links
- Source commit: `cfd3f6c` (`feat: add signed plugin catalog`)

## Remaining risks

- There is no launcher catalog browser, update UI, trust-management UI, or
  consent UI.
- Catalog creation and key rotation/revocation distribution are not yet
  operational workflows.
- GitHub Releases is the only catalog source.
- RSA public-key material is configured locally; there is no operating-system
  trust-store integration or hardware-backed signing workflow.
- A process interruption can leave an inert staging file; startup scavenging
  is not yet implemented.
- Runtime dependency isolation, unload, crash containment, and sandboxing
  remain unimplemented because execution stays disabled.
- Alpha contracts remain subject to deliberate breaking changes.

## Recommended next step

Review and approve the runtime-host and launcher-integration boundary before
writing it. The plan should cover process isolation, capability enforcement,
dependency conflicts, crash recovery, unload/update behavior, catalog and
consent presentation, and safe startup cleanup. Runtime loading should remain
disabled until that boundary and its failure policy are explicit.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[AI/AI Context|AI Context]]
- [[AI/Plugin SDK Increment 3 Report|Plugin SDK Increment 3 Report]]
