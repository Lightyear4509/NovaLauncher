---
type: quality-plan
status: canonical
version: "1.0"
created: 2026-08-13
updated: 2026-08-13
---

# Safety and Test Plan

## Mandatory engineering safeguards

- Enable nullable reference types, implicit usings, analyzers, warnings as
  errors for owned source, deterministic builds, and locked dependency restore.
- Validate all external input at the boundary; domain models contain normalized
  values, not raw provider payloads.
- Every async operation accepts and propagates `CancellationToken`. UI commands
  prevent duplicate work and restore a stable state in `finally`.
- Never block the UI thread with `.Wait()`, `.Result`, synchronous network I/O,
  large file parsing, or image decoding.
- Network clients are created by `IHttpClientFactory`, use HTTPS, bounded
  timeouts/redirects/response sizes, and do not retry permanent failures.
- Persistence is staged and validated before publication to live state.
- Destructive UI actions state their exact scope and require confirmation when
  user-created NovaLauncher data would be lost.
- Global unhandled-exception logging is a last-resort diagnostic, not recovery.
  The app must avoid continuing after corrupted invariants.

## Required test layers

### Unit tests

Cover deterministic domain behavior: normalization, merge precedence,
provenance, search tokenization and sorting, URI/path validation, schema
validation, retry classification, cache expiry/LRU behavior, theme contracts,
and view-model state transitions.

### Persistence contract tests

Run every store against temporary directories and verify:

- first save/load and round trip;
- legacy migration and unknown-newer-schema refusal;
- interrupted write leaves the prior valid document readable;
- corrupt primary recovers from a valid backup and surfaces a warning;
- corrupt primary and backup produce a typed unrecoverable result;
- invalid input is preserved before later save;
- concurrent writes cannot interleave or publish stale in-memory state;
- cancellation before commit changes neither disk nor live state;
- disk-full, access-denied, and replacement failure leave recoverable data.

Use injectable filesystem/fault seams rather than depending only on flaky
machine-level failure simulation.

### Provider contract tests

Use local fake HTTP handlers; normal tests must not call live services. Verify
timeouts, cancellation, 408/429/5xx retry, `Retry-After`, permanent 4xx, invalid
JSON, wrong identity, redirects, oversized payloads/images, decompression limits,
partial provider failure, offline cache fallback, and secret-free logs.

Increment 4 additionally verifies deterministic provider priority, manual
provenance precedence, last-known-good preservation, fresh/stale/expired/LRU
cache transitions, force-refresh bypass, atomic publish failure, HTTPS-only
requests, redirects disabled, content-length and streamed-size bounds, JSON depth
and content-type rejection, HTML/text normalization, unsafe artwork URL rejection,
disabled SteamGridDB behavior, and placeholder completion.

Increment 4 image materialization additionally verifies encoded byte limits,
declared/actual PNG-JPEG-WebP agreement, complete decode, single-frame content,
dimension and pixel ceilings, generated filenames, managed-root confinement,
per-item corruption isolation, cancellation rollback, failed-persistence rollback,
obsolete-cache cleanup, offline placeholders, and corrupt/missing cached-image
rendering fallback. Remote filenames never become local paths.

### Steam import contract tests

Use generated local fixtures and injected registry/filesystem seams. Verify
supported registry views, manual absolute roots, legacy and current
`libraryfolders.vdf`, valid manifests, filename/App-ID mismatch, duplicates,
missing install directories, relative/UNC/device paths, traversal, malformed and
oversized VDF, excessive nesting/tokens/counts, cancellation, per-file failure
isolation, stale preview rejection, failed-save non-publication, preservation of
manual fields/favorites/metadata/collections, and deterministic 10,000-game
preview/commit behavior. Tests must assert Steam inputs are never written.

### Process-launch tests

Verify argument preservation with spaces and Unicode, missing executable,
invalid working directory, allowlisted and rejected URI schemes, no shell
injection, process start failure, cancellation before start, and unchanged
library state. Use a purpose-built harmless test executable.

### UI and accessibility tests

Test primary view-model flows headlessly and maintain a smaller Windows UI smoke
suite for first run, import preview, add/edit/remove, search, collection changes,
theme switch, backup/restore, offline refresh, and launch failure. Assert focus
order, automation names, keyboard actions, 200% scale, compact window width, and
explicit empty/error states.

### Security/adversarial tests

Include traversal, UNC/device paths, alternate data streams, reparse points,
symlink escapes, case-insensitive duplicates, huge counts/strings, malformed
Unicode, JSON depth/size bombs, image bombs, unsafe schemes, log injection,
secret canaries, and malicious command arguments. Fuzz parsers with bounded
time and memory in CI or a scheduled security workflow.

### Save-sync contract tests

Use isolated temporary save roots and paired loopback transports. Verify Steam
hard-exclusion, explicit folder mapping, traversal/ADS/reparse rejection,
file/count/aggregate bounds, quiet-period mutation refusal, changed-file-only
deltas, deletion manifests, immutable parent generations, offline retry,
wrong-secret rejection, six-digit format, salted verifier persistence, persisted
three-failure lockout, 24-hour expiry, single-use consumption, first-device
identity pinning, invitation replay after revocation, authenticated encryption,
timestamp/request replay rejection, peer-head divergence, first-sync conflict,
all three conflict choices, backup-before-restore, hash mismatch, cancellation,
and rollback after injected write/move/disk failures. Release qualification also
requires two real Windows devices on one tailnet and interruption during every
transfer/restore phase.

The six-digit value is bootstrap authorization, not cryptographic key material.
Tests must prove bootstrap is reachable only through the Tailscale-bound
listener, no plaintext code is persisted or logged, clipboard clearing is
best-effort, and the independent 256-bit session credential protects all save
traffic after pairing.

### Performance and soak tests

- Benchmark search over 10,000 deterministic generated games.
- Measure cold start, idle memory, and library scrolling on a documented Windows
  reference runner.
- Run repeated import/refresh/cancel cycles and a multi-hour idle/interaction
  soak before release candidates.
- Performance failures are visible gates; baselines include hardware, OS, SDK,
  build type, and sample size.

## CI gates

For every pull request:

1. Formatting and static analysis
2. Dependency restore in locked mode and vulnerability audit
3. Debug and Release build
4. Unit, contract, integration, and headless presentation tests
5. Code-coverage report with no regression on changed production code
6. Secret scan and artifact-content scan
7. Documentation link and canonical-status validation

For a release candidate, additionally require the Windows UI smoke suite,
self-contained publish, clean-machine install/upgrade/uninstall tests, binary
malware scan, checksum generation, software-bill-of-materials generation, and
artifact signing when a trusted signing identity is available.

## Minimum coverage policy

Coverage is a diagnostic, not proof of correctness. Nonetheless, changed core
domain and persistence code must reach 90% line and 85% branch coverage; changed
view models/providers must reach 80% line and 70% branch coverage. No exclusions
without a documented reason. Every fixed defect gains a regression test.

## Achievement-provider safety matrix

First-party achievement providers require deterministic tests for identity and
deduplication, valid and malformed payloads, partial data, unknown fields,
response and item-count limits, rate limiting, timeout, retry classification,
cancellation, offline/stale cache behavior, atomic cache failure, credential and
account-identifier redaction, and provider failure isolation. Normal test runs
must use fakes or fixtures and must not require live accounts or network access.

No test or production path may write, simulate, or fabricate an achievement
unlock. Provider failure must not mutate the library or block game launching.

## Test evidence

Each build records the commit, SDK, dependency lock hash, commands, test counts,
failures/skips, coverage, performance environment, artifact SHA-256, and SBOM.
Historical prose claiming tests passed is not current evidence.
