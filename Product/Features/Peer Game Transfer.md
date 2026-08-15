# Peer game transfer

Status: proposed later increment; not implemented or enabled.

NovaLauncher may support user-initiated transfer of a manually selected,
user-owned DRM-free game folder between already paired devices. It must not
copy Steam libraries, bypass DRM or licensing, acquire games from third-party
sources, or imply that ownership on one device grants redistribution rights.

## Required consent and boundaries

- Sender explicitly selects one source folder and attests that they may copy it.
- Receiver explicitly requests the named package and chooses an empty destination.
- Manual/DRM-free games only; Steam and other managed-store installations are
  rejected by source, path, and executable provenance checks.
- No automatic execution, installation, registry modification, service creation,
  shortcut creation, or privilege elevation after receipt.
- No public discovery, catalog, marketplace, relay, seeding, or multi-peer sharing.
- Both devices must already be identity-pinned through the Tailscale pairing flow.

## Transfer safeguards

- Read-only source scan with deterministic manifest, file count, individual-file,
  and aggregate-size limits.
- Reject reparse points, symlinks, device paths, alternate data streams, traversal,
  sparse-file expansion, and files that change during scanning.
- Chunked authenticated encryption with bounded memory, cancellation, resume tokens,
  byte-accurate progress, bandwidth limiting, and free-space preflight.
- Receive only into managed staging; verify every length and SHA-256 hash before an
  atomic move into an empty user-selected destination.
- Preserve partial data only when the user elects resumable transfer; otherwise
  remove staging safely. Never overwrite an existing installation.
- Local audit records contain package name, sizes, hashes, devices, and outcome but
  omit file contents, secrets, and sensitive absolute paths.

## Release gates

Threat modeling and independent legal/security review are required before this
feature is implemented. Tests must cover malicious paths, link races, mutation
during scan, disk exhaustion, interruption at every chunk, replay, wrong peer,
hash mismatch, oversized packages, firewall loss, cancellation, resume, cleanup,
and explicit refusal of Steam or other store-managed roots.
