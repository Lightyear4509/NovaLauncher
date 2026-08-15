---
type: feature
status: experimental
priority: critical
related_epic: "[[Product/Epics/Cloud Saves|Cloud Saves]]"
release: post-alpha
created: 2026-07-30
updated: 2026-07-30
progress: 70
---

# Seamless Cloud Saves

## Goal

Let a user stop a game on one device and safely continue on another without
manually copying save files or surrendering control of the data.

## User flow

1. Before launch, NovaLauncher checks the remote generation.
2. A newer compatible snapshot is verified and restored atomically.
3. The device obtains a renewable per-game lease.
4. NovaLauncher launches and monitors the game process tree.
5. After exit, it waits for save files to become stable.
6. It snapshots changed files, encrypts them, and transfers the new generation.
7. Another device can verify and restore that generation before launch.

## Core requirements

- Local version history
- Backup before restore
- File hashing and manifest
- Atomic staging
- Conflict detection
- Offline queue
- Cross-device identity
- Cross-platform path mapping
- End-to-end encryption
- Transport abstraction
- Cancellation, progress, logs, and recovery

## Save discovery order

1. Per-game user override
2. Plugin-provided mapping
3. Ludusavi manifest/CLI
4. PCGamingWiki-assisted mapping
5. Conservative heuristic with mandatory user confirmation

## Tailscale position

Tailscale is an optional secure network transport, not NovaLauncher’s source of
truth. A Tailscale implementation still needs NovaLauncher device identity,
snapshot manifests, version history, leases, conflict handling, encryption,
retry, and an always-available destination when direct peers are offline.

## Non-negotiable safety rules

- Never silently use last-write-wins for divergent devices.
- Never delete the last known-good local or remote snapshot during sync.
- Never restore an unverified snapshot.
- Never allow archive paths outside the staging root.
- Never upload while the save set is still changing.
- Never expose plaintext saves to a transport provider when encryption is
  enabled.

## Implemented experimental slice

- Manual-game-only explicit save-folder mapping; Steam is hard-excluded
- Stable device IDs and first-authenticated peer-ID pinning
- Single-use six-digit invitations expire after 24 hours and lock after three
  persisted failures. Only a salted PBKDF2 verifier is persisted; the code is
  accepted through the Tailscale-only listener and never encrypts save data.
- A separate random 256-bit session secret is stored in Windows Credential
  Manager and returned only after successful invitation redemption.
- First successful authentication atomically pins the peer identity; later connections are automatic
- AES-256-GCM application encryption over a Tailscale-only TCP listener
- SHA-256 manifests, changed-file deltas, immutable local generations, and retry
- Pull-before-launch and quiet-period snapshot/push after executable exit
- Backup-before-restore and explicit keep-local/use-remote/keep-both conflicts
- Pairing-derived shared save identities link separately added copies of the same
  manual game without clipboard transfer or simultaneous physical access. The
  identity is an HMAC of a domain-separated, normalized sync label and platform
  under the devices' existing 256-bit pairing credential. Each device retains
  its own executable and save-folder mapping. A raw identity field remains only
  as an advanced recovery path.
- The Downloads & Saves page exposes the current scan/transfer and persisted
  per-game snapshot state. Transfer progress is indeterminate in this slice;
  byte-accurate streaming progress requires a future chunked transport.
- A successful push is recorded as **Peer acknowledged**. Failed pushes remain
  queued across restarts and can be retried explicitly from Downloads & Saves.
- Mapping a non-empty save folder presents an explicit confirmation to upload
  existing saves or defer. **Sync now** performs the same bounded, quiet-period,
  hash-manifested snapshot without requiring a game launch/exit cycle. It is
  available only after the manual game has a shared save identity.
- A newly linked empty folder cannot publish the first snapshot. This prevents
  an empty installation from creating a false deletion conflict. Once a game
  has a synchronized baseline, intentional deletion of all saves remains a
  valid changed snapshot.

Still required for release qualification: real two-device tailnet validation,
injected power-loss/disk-full restore drills, Windows Firewall matrix, large-save
soak, accessible conflict-state usability review, and independent security review.

## Related

- [[Product/Epics/Cloud Saves|Cloud Saves]]
- [[Decisions/ADR-016 Cloud Saves Transport Separation|ADR-016 Cloud Saves Transport Separation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Engineering/Architecture/Integration Feasibility Matrix|Integration Feasibility Matrix]]
