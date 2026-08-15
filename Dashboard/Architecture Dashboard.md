---
type: dashboard
area: architecture
status: active
created: 2026-07-29
updated: 2026-07-29
---

# Architecture Dashboard

## Core flow

```text
UI → ViewModels → Services → Managers → Providers → External APIs
```

## Architecture

- [[Engineering/Architecture/Architecture Index|Architecture Index]]
- [[Engineering/Architecture/Architecture Overview|Architecture Overview]]
- [[Engineering/Architecture/System Map|System Map]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Product/Design/Master Design Document|Master Design Document]]

## Services

- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Engineering/Services/Artwork Service|Artwork Service]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Engineering/Services/Library Service|Library Service]]
- [[Engineering/Services/Plugin Service|Plugin Service]]
- [[Engineering/Services/Theme Service|Theme Service]]

## Decisions

```dataview
TABLE status, updated
FROM "Decisions"
WHERE type = "decision"
SORT file.name ASC
```
