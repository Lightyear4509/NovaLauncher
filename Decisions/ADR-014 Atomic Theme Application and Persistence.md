---
type: decision
status: accepted
created: 2026-07-30
updated: 2026-07-30
---

# ADR-014 Atomic Theme Application and Persistence

## Context

Theme definitions were duplicated in the main view model, saved settings were
not loaded at startup, and a settings-save failure left the new runtime theme
active even though it would not survive restart.

## Decision

`ThemeService` owns the canonical built-in catalog. Startup prepares settings
off the UI thread, then applies the resolved theme on the Avalonia UI thread
before constructing the main window. Unknown saved IDs fall back to Nova Dark.

Runtime selection is coordinated as apply-then-save. If saving fails, the
service restores both the previous theme and previous in-memory setting before
propagating the error. `IThemeHost` isolates Avalonia style mutation from this
coordination policy.

## Consequences

- The first rendered window uses the persisted theme.
- Asynchronous settings I/O never blocks its own UI-context continuation.
- Presentation code cannot drift from the service catalog.
- Ordinary save failures do not leave runtime and persisted choices divergent.
- A double failure during save and rollback is reported as a combined failure
  and requires operator/user recovery.
- Community theme discovery and trust policy remain separate future work.

## Related

- [[Product/Features/Theme Reliability|Theme Reliability]]
- [[Engineering/Services/Theme Service|Theme Service]]
