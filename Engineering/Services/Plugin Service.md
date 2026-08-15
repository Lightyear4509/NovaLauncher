---
type: service
status: removed
priority: none
related_epic: "[[Product/Epics/Plugins|Plugins]]"
architecture: "[[Engineering/Architecture/Plugin System|Plugin System]]"
created: 2026-07-29
updated: 2026-08-13
---

# Plugin Service

> Removed service. It must not be implemented. First-party achievements use a
> narrow typed provider boundary instead. See
> [[Product/Features/Achievements|Achievements]].

## Responsibility

Expose plugin capabilities to the application while keeping discovery and lifecycle mechanics behind a stable boundary.

## Foundation notes

- Plugin contracts must not expose unnecessary internal implementation details.
- Enable, disable, compatibility, permissions, and failure isolation require explicit behavior.
- The future marketplace should use the same core lifecycle model.
- Alpha packages require manifest, compatibility, hash, consent, health,
  quarantine, and rollback behavior.
- In-process permissions are disclosure and API shaping, not an enforceable
  sandbox.
- A future out-of-process host will enforce high-risk capabilities.

## Related

- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Product/Epics/Plugins|Plugins epic]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015]]
