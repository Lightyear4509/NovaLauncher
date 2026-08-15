---
type: architecture
status: active
aliases:
  - Architecture
  - Overview
created: 2026-07-29
updated: 2026-07-29
---

# Architecture Overview

NovaLauncher follows a layered architecture.

```
UI
 ↓
ViewModels
 ↓
Services
 ↓
Providers
 ↓
External APIs
```

## Principles

- Services own business logic
- Providers talk to external services
- UI never talks directly to APIs
- Everything should be testable
- Dependency Injection everywhere

---

## Core Services

- ArtworkService
- LibraryService
- MetadataService
- ThemeService

---

## Future

- Plugin SDK
- Emulator Framework
- Cloud Save System
- AI Recommendations

## Related

- [[Engineering/Architecture/System Map|System Map]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
