---
type: index
area: services
status: active
created: 2026-07-29
updated: 2026-07-29
---

# Service Catalog

Services own application business logic and expose stable boundaries to ViewModels. Providers handle external systems.

```dataview
TABLE status, priority, related_epic, updated
FROM "Engineering/Services"
WHERE type = "service"
SORT status ASC, file.name ASC
```

## Architecture

- [[Engineering/Architecture/Architecture Overview|Architecture Overview]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]

