---
type: service
status: planned
priority: critical
related_epic: "[[Product/Epics/Cloud Saves|Cloud Saves]]"
created: 2026-07-30
updated: 2026-07-30
---

# Cloud Save Service

## Responsibility

Coordinate save discovery, stable snapshot creation, version history,
encryption, transport, conflict handling, atomic restore, and launch lifecycle.

## Boundaries

- `ISaveDiscoveryProvider` returns confirmed relative save sets.
- `ISaveSnapshotStore` persists immutable snapshot manifests and chunks.
- `ICloudTransport` transfers opaque encrypted content.
- `IGameSessionCoordinator` owns pre-launch pull, lease, process monitoring,
  quiet-period detection, and post-exit push.
- `IConflictResolver` produces explicit keep-local, keep-remote, keep-both, or
  deferred outcomes.

## Safety requirements

- Stage before restore.
- Back up current files before replacement.
- Verify paths, hashes, and authenticated encryption.
- Preserve last known-good generations.
- Detect offline divergence.
- Never silently resolve conflicts with last-write-wins.
- Make cancellation and power loss recoverable.

## Related

- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- [[Product/Epics/Cloud Saves|Cloud Saves]]
- [[Decisions/ADR-016 Cloud Saves Transport Separation|ADR-016]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
