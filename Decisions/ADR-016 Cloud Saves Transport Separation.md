---
type: decision
status: accepted
created: 2026-07-30
updated: 2026-07-30
---

# ADR-016 Cloud Saves Transport Separation

## Context

Tailscale can connect devices securely, but connectivity alone does not provide
save discovery, snapshots, version history, conflicts, offline operation,
integrity, encryption-at-rest, or atomic restore.

## Decision

NovaLauncher owns a provider-neutral save snapshot and synchronization engine.
Tailscale, local folders, NAS, and future services implement transport/store
contracts beneath that engine.

## Consequences

- Cloud-save correctness can be tested without a live VPN.
- Users are not locked to Tailscale.
- A transport cannot force last-write-wins semantics into the core.
- Tailscale remains valuable for direct peer and private relay connectivity.
- An always-on peer or storage node is required for seamless sync when the
  source device is offline.

## Related

- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- [[Product/Epics/Cloud Saves|Cloud Saves]]
