# NovaLauncher

NovaLauncher is a planned Windows desktop game-library launcher that unifies
user-owned games without replacing their official launchers.

## Repository status

This repository contains the Increment 0 Avalonia/.NET foundation, Increment 1
durable storage, Increment 2 local manual-library experience, and Increment 3
defensive Steam import. The Windows UI
supports first-run recovery, manual add/edit/remove, search, deterministic sorts,
favorites, collections, safe executable or allowlisted-launcher-URI starting, and
validated backup/restore. It is **not** a feature-complete or distributable
launcher. Packaging, the planned navigation redesign, and full release
qualification remain later increments. Increment 4 supplies first-party
metadata/artwork providers, deterministic merge and cache behavior, bounded
image validation, managed cache installation/cleanup, offline placeholders, and
safe visual artwork rendering.
Increment 5 adds opt-in, first-party, read-only Steam achievements with stable
identities, bounded documented API calls, atomic local caching, stale/offline
fallback, completion summaries, and explicit privacy states.
Increment 6 adds independently branded Home, Library, Saves, and Settings
navigation. Increment 7 adds five trusted themes, diagnostics, compact-layout
hardening, localization boundaries, accessibility contracts, and performance
gates. Increment 8 now produces an unsigned installer, portable ZIP, SBOM, and
checksums under `artifacts/release`. These are alpha preview candidates, not a
qualified public release: the clean Windows 10/11 installer matrix and manual
NVDA/Narrator/200%-scale checks remain open.

Plugins, plugin SDKs, marketplaces, scripting, and downloaded-code execution
have been removed from the active product plan. Achievements are planned as a
first-party, read-only integration after Steam identity and metadata are in
place; see [[Product/Features/Achievements|Achievements]]. Historical plugin
notes are retained only as non-authoritative research records.

Historical increment reports describe an older implementation that is still
absent and cannot be independently verified from this checkout. They are not
evidence for the new source foundation.

Do not advertise this checkout as release-ready until every gate in
[[Planning/Releases/Release Readiness Checklist|Release Readiness Checklist]]
passes and installable artifacts are attached to a release.

## Try the alpha preview

Use `artifacts/release/NovaLauncher-Setup-0.1.0-alpha.1-win-x64.exe`, or extract
the portable ZIP beside it and run `NovaLauncher.App.exe`. Both are unsigned;
verify the SHA-256 value in `SHA256SUMS.txt` before running them. Windows may
show **Unknown publisher** or a Microsoft Defender SmartScreen warning. Do not
disable security protections globally. See `docs/operations/install-uninstall.md`.

An experimental post-alpha Tailscale save-sync implementation is now present for
explicitly mapped **manual games only**. It uses stable device identity, a
single-use 24-hour six-digit invitations with three-attempt lockout, backed by a separate 256-bit secret stored in Windows Credential Manager, authenticated
encryption above Tailscale, immutable hash manifests, changed-file deltas,
offline retry, quiet-period checks, atomic backup-before-restore, replay
rejection, and explicit conflict choices. Steam-imported games are hard-excluded
and continue to use Steam Cloud. This remains a preview until the real two-device
tailnet, interruption, Windows Firewall, and recovery matrices are completed.

The latest post-Increment-8 preview adds a card-based Library page, separate
game details, executable file picking, directly measured local-executable
playtime, explicit per-game UAC launch requests, clearer Steam import failures,
and a memory-only SteamGridDB API-key setting. See
`docs/releases/0.1.0-alpha.1-ui-library-redesign.md` for boundaries.

The subsequent Steam-import identity fix repairs staged JSON round-tripping and
has been verified against an isolated copy of the local library plus read-only
Steam manifests. Name-based manual-game enrichment is designed but remains a
separate confirmation-first increment; see
`docs/roadmap/manual-game-identity-and-enrichment.md`.

The current UI workspace adds aspect-preserving cover art to Library and Home,
manual-game-only Add Cover and Remove Cover actions, and opaque themed buttons
with animated hover color feedback. Custom images are decoded, bounded, copied
atomically into managed artwork storage, and never modify the chosen source file.

## Build handoff

Use these documents as the canonical handoff for Google AI Studio or another
implementation agent:

1. [[AI/Google AI Studio Build Guide|Google AI Studio Build Guide]]
2. [[Product/Requirements/Alpha Release Specification|Alpha Release Specification]]
3. [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
4. [[Engineering/Quality/Safety and Test Plan|Safety and Test Plan]]
5. [[Planning/Releases/Release Readiness Checklist|Release Readiness Checklist]]

The build agent must implement in small, compiling increments. A feature is not
complete merely because code was generated: its tests, recovery behavior,
documentation, and release gate must also pass.

## Intended technology

- Windows 10/11 desktop application
- C# and the latest supported stable .NET LTS at implementation time
- Avalonia UI with MVVM and dependency injection
- Local, offline-first, versioned JSON persistence for the alpha
- xUnit (or an equivalent .NET test runner) for unit and integration tests
- Self-contained `win-x64` release artifact

The implementation agent must pin exact SDK and package versions in generated
source control and record them in the release notes.
