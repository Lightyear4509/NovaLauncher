---
type: decision
status: proposed
created: 2026-07-30
updated: 2026-07-30
---

# ADR-017 Authorized Content Acquisition Boundary

## Context

An integrated downloader and installer is useful for official stores,
user-owned installers, homebrew, open-source games, and mods. Integrating
piracy/repack or DRM-circumvention sources would create legal, security, trust,
and distribution risks.

## Decision

NovaLauncher acquisition providers must represent authorized sources and
preserve source/license provenance. The project will not integrate piracy,
repack, cracked-game, DRM-bypass, or copyright-circumvention sources.

## Consequences

- Download and extraction infrastructure can be designed as a reusable secure
  pipeline.
- Nexus Mods belongs to the mod provider boundary, not general game piracy.
- User-owned installers and authorized direct downloads remain supported.
- Provider catalog review includes distribution rights and terms compliance.

## Related

- [[Product/Features/Authorized Acquisition and Installation|Authorized Acquisition and Installation]]
- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
