---
type: service
status: planned
priority: medium
related_epic: "[[Product/Epics/Infrastructure|Infrastructure]]"
created: 2026-07-29
updated: 2026-07-30
---

# Download Service

## Responsibility

Coordinate controlled background downloads, progress reporting, cancellation, retry behavior, and failure handling.

## Foundation notes

- Networking must not block the UI.
- Download concurrency and retry policy require explicit limits.
- Consumers should not manage raw download lifecycle details.
- Downloads require expected-size, disk-space, hash/signature, quarantine, and
  resumability support.
- Archive extraction belongs to a separate staging boundary with traversal,
  symlink, and decompression-bomb defenses.
- Sources must be official, user-owned, open-source, homebrew, mod, or
  otherwise authorized.

## Related

- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Product/Features/Authorized Acquisition and Installation|Authorized Acquisition and Installation]]
- [[Decisions/ADR-017 Authorized Content Acquisition Boundary|ADR-017]]
