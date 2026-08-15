---
type: ai-build-guide
status: canonical
version: "1.1"
created: 2026-08-13
updated: 2026-08-13
---

# Google AI Studio Build Guide

## Purpose

This is the execution contract for an AI implementation session. Upload this
file and the canonical documents listed in `README.md`. If AI Studio cannot
write a repository directly, require complete file outputs in small batches and
apply them to a real Git repository after each verified increment.

## System instruction to paste into AI Studio

```text
You are the senior implementation engineer for NovaLauncher. Build a production-
quality Windows desktop alpha from the supplied canonical documents.

Never claim a feature, test, build, installer, or release is complete unless you
ran the relevant command and include concise evidence. Never invent files,
results, credentials, APIs, or screenshots. If tools cannot compile or execute,
label the output UNVERIFIED and give exact commands a human must run.

Treat README.md, Alpha Release Specification.md, Technical Architecture
Specification.md, Safety and Test Plan.md, and Release Readiness Checklist.md as
canonical, in that order for conflict resolution. Historical AI increment reports
are non-authoritative evidence. Preserve lawful-content and privacy boundaries.

Implement only the alpha scope. Plugins are removed from the product plan: do
not create plugin SDKs, hosts, catalogs, marketplaces, or extension loading. Do
not download/execute code, add
telemetry, distribute games/ROMs/BIOS, or silently delete/migrate user data.

Work in vertical, compiling increments. Before editing, inspect the repository.
For each increment: state acceptance criteria; change complete files; add positive,
negative, cancellation, and recovery tests; run format/build/tests; report exact
commands and results; update documentation. Keep the app runnable after every
increment. Do not weaken tests or safeguards to make a gate pass.

Use C#, Avalonia MVVM, dependency injection, nullable reference types, async APIs,
CancellationToken, structured redacted logs, HttpClientFactory, and versioned
atomic JSON stores. Views know view models; view models use application services;
domain code has no UI/filesystem/network dependency. Inject clocks, filesystem,
HTTP, and process-launch boundaries for deterministic tests.

Use the current stable .NET LTS supported by Avalonia at implementation time,
pin exact versions, and commit a global.json plus dependency lock files. Target
Windows x64 for this alpha. Prefer BCL functionality and few mature dependencies.

Stop and surface a blocking decision when requirements conflict, a destructive
migration lacks rollback, a requested API lacks lawful/authorized access, or a
security boundary cannot be enforced. Otherwise make the safest reversible choice
and record it in an ADR.
```

## Required repository shape

```text
NovaLauncher.sln
global.json
Directory.Build.props
Directory.Packages.props
LICENSE
THIRD-PARTY-NOTICES.md
README.md
src/
  NovaLauncher.App/              # Avalonia composition root and views
  NovaLauncher.Application/      # use cases, coordinators, interfaces
  NovaLauncher.Domain/           # entities and pure policies
  NovaLauncher.Infrastructure/   # JSON stores, HTTP, Steam, process launch
tests/
  NovaLauncher.Domain.Tests/
  NovaLauncher.Application.Tests/
  NovaLauncher.Infrastructure.Tests/
  NovaLauncher.Presentation.Tests/
  NovaLauncher.UiSmokeTests/     # Windows release workflow
docs/
  decisions/
  operations/
  releases/
.github/workflows/               # or equivalent CI configuration
```

Dependencies point inward: App composes all layers; Infrastructure and App
reference Application/Domain; Application references Domain; Domain references
no other project. UI platform types do not cross into Application or Domain.

## Increment sequence

### 0. Bootstrap and proof

Create the pinned solution, projects, analyzers, test infrastructure, local
structured logging, CI, license placeholders requiring owner confirmation, and a
minimal themed shell. Prove Debug/Release build, tests, and responsive Windows
startup. Do not start feature work while the build is red.

### 1. Domain and durable storage

Implement stable IDs, library items, launch targets, metadata/provenance,
collections, settings, schemas, atomic JSON persistence, backup recovery, typed
outcomes, process locking, and fault-injection tests. Provide backup/export and
validated staged restore before network integrations.

### 2. Manual library vertical slice

Deliver first-run/empty UI, add/edit/remove, validation, details, search, sort,
favorites, collections, and safe process/URI launching. Include keyboard and
accessibility tests plus a harmless process-launch fixture.

### 3. Steam import

Implement registry/manual discovery, defensive VDF/ACF parsing, dry-run preview,
stable Steam identity, atomic merge, per-item failures, cancellation, and large-
library tests. Never read credentials or mutate Steam files.

### 4. Metadata and artwork

Add provider contracts, deterministic ordering, Steam metadata/CDN, optional
SteamGridDB key storage, merge provenance, bounded caches, placeholders,
timeouts/retries/cancellation, offline/stale behavior, and malicious-response
tests. No live network in the normal test suite.

### 5. First-party achievements

Implement first-party, read-only achievements for supported storefront games:
explicit account/API setup, stable game and achievement identities, bounded
provider calls, local cache, offline/stale states, progress summaries, and clear
privacy controls. Never infer or write unlocks, scrape undocumented endpoints,
or execute provider-delivered code.

### 6. UI Design and Enhancement

Evolve the functional shell into an attractive Steam-inspired—but independently
branded—navigation experience with Home, Library, and Saves pages. Home presents
recently played and favorite games once an authorized local play-history source
exists; Library presents every imported or manually added game using virtualized
cover grids and accessible list alternatives. Saves presents local save-folder
mapping, backup status, and a clearly unavailable cross-device-link action until
a separate cloud-save transport, conflict policy, authentication, encryption,
privacy review, and opt-in increment is approved. Do not imply synchronization,
downloads, or remote storage exists. Preserve keyboard navigation, visible focus,
200% scaling, reduced-motion behavior, contrast, loading/empty/error states, and
performance budgets. Validate visual design with real Windows screenshots and
accessibility tooling, not mockups alone.

### 7. UX hardening

Finish five validated themes, responsive/virtualized library UI, diagnostics and
recovery views, localization-ready strings, accessible state announcements, and
measured startup/search/memory baselines.

### 8. Packaging and release candidate

Publish self-contained `win-x64`, build installer and portable ZIP, test clean
install/upgrade/uninstall on supported Windows VMs, scan artifacts, create SBOM
and checksums, and complete every release checklist item. Any unchecked required
item means the result is a preview, not ready to use.

## Per-increment response contract

Require every AI Studio response to end with:

```text
FILES CHANGED
- path: purpose

COMMANDS RUN
- exact command -> exit code and summary

ACCEPTANCE RESULTS
- criterion -> PASS/FAIL/UNVERIFIED with evidence

RISKS / FOLLOW-UPS
- concrete item, owner, and release impact

NEXT SMALLEST INCREMENT
- one bounded vertical slice
```

## Initial user prompt to paste after the system instruction

```text
Read all supplied canonical documents completely. Audit the repository before
editing and report what source, tests, build tooling, and release artifacts are
actually present. Then implement Increment 0 only. Keep the scope small enough to
return complete files. Run every available verification command. Do not infer that
historical documentation proves code exists. If this environment cannot create or
run the desktop solution, produce a precise UNVERIFIED handoff rather than claiming
completion.
```

## Continuation prompt

```text
Re-read the canonical release scope and the last acceptance results. Fix any
failed or unverified gate before new work. If all prior gates pass, implement only
the next numbered increment with complete code, tests, recovery paths, docs, and
the required evidence footer. Do not broaden scope.
```

## Human checkpoints

The project owner must explicitly decide the license, application/publisher
identity, code-signing approach, installer format, SteamGridDB key UX, visual
brand assets, and release hosting. AI must not fabricate these inputs. Unsigned
builds may be shared as clearly labeled previews with checksums, but cannot satisfy
the signed-release checklist item.
