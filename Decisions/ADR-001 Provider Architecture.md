---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
affected_systems:
  - "[[Engineering/Architecture/Artwork System|Artwork System]]"
  - "[[Engineering/Services/Artwork Service|Artwork Service]]"
  - "[[Engineering/Services/Metadata Service|Metadata Service]]"
---

# ADR-001: Provider Architecture

## Title

Provider Architecture

---

## Context

NovaLauncher needs to retrieve artwork and game metadata from multiple
independent sources.

---

## Decision

Each provider family has a narrow interface and manager:

- Artwork sources implement `IArtworkProvider` and are selected through
  `ArtworkProviderManager`.
- Metadata sources implement `IMetadataProvider` and are selected through
  `MetadataProviderManager`.

Managers own eligibility filtering and deterministic ordering. Services
coordinate providers without depending on concrete integrations.

---

## Consequences

✔ Easy to extend

✔ Plugins become simple

✔ Better testing

✔ Cleaner architecture

✔ Provider failures and cancellation behavior can be tested independently

## Related

- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Decisions/Decision Index|Decision Index]]
