---
type: feature
status: complete
priority: high
related_epic: "[[Product/Epics/Themes|Themes]]"
created: 2026-07-30
updated: 2026-07-30
progress: 100
---

# Theme Reliability

## Goal

Make built-in themes deterministic at startup, safe to change, consistent
across pages, and protected by automated resource validation.

## Increment 1 complete

- One canonical catalog for five built-in themes
- Persisted theme loading before main-window construction
- Non-blocking startup preference preparation
- Nova Dark fallback for unknown saved IDs
- Best-effort normalization of invalid persisted IDs
- Testable Avalonia style-host boundary
- Coordinated runtime apply and settings save
- Runtime and setting rollback after persistence failure
- Six focused service tests

## Increment 2 complete

- Twenty-four-brush resource contract
- Automated validation across all five built-in theme files
- XAML reference-to-contract validation
- Theme-aware Settings page surfaces
- Accessible theme selector, progress, and result status
- Removed hard-coded Settings page colors
- Three focused resource-contract tests; 163 total tests pass

## Risks

- Theme resource changes remain UI-thread operations.
- A failure in both persistence and runtime rollback can leave the active theme
  different from the saved setting; this is logged and surfaced as a combined
  failure.
- Community theme loading is outside the current trusted built-in boundary.

## Related

- [[Product/Epics/Themes|Themes epic]]
- [[Engineering/Services/Theme Service|Theme Service]]
- [[Decisions/ADR-014 Atomic Theme Application and Persistence|ADR-014 Atomic Theme Application and Persistence]]
- [[AI/Themes Increment 1 Report|Themes Increment 1 Report]]
- [[AI/Themes Increment 2 Report|Themes Increment 2 Report]]
- [[AI/Themes Startup Recovery Report|Themes Startup Recovery Report]]
