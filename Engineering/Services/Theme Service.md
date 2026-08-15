---
type: service
status: active
priority: medium
related_epic: "[[Product/Epics/Themes|Themes]]"
created: 2026-07-29
updated: 2026-07-30
---

# Theme Service

## Responsibility

Coordinate the trusted built-in catalog, persisted selection, and runtime
application while isolating Avalonia resource mutation from the UI.

## Implemented contract

- Five canonical built-in themes
- Persisted settings prepared off the UI thread before main-window construction
- Canonical theme applied on the Avalonia UI thread
- Nova Dark fallback for unknown IDs
- `IThemeHost` boundary for Avalonia style mutation
- Apply-and-save rollback on persistence failure
- Serialized asynchronous theme changes
- Failure logging and user-facing status
- Twenty-four-brush resource contract
- Automated built-in theme completeness and XAML-reference validation

## Foundation notes

- Themes may provide colors, fonts, layouts, animations, and controlled styling.
- Invalid theme content must fail safely.
- Theme packaging should align with the future plugin and marketplace model.
- Community discovery and validation are not implemented.
- Automated validation does not replace manual contrast and layout review.

## Related

- [[Product/Epics/Themes|Themes epic]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Product/Features/Theme Reliability|Theme Reliability]]
- [[Decisions/ADR-014 Atomic Theme Application and Persistence|ADR-014]]
