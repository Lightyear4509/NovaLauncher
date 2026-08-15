---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-005 Artwork Cancellation Ownership

## Context

Lower artwork layers already accept and propagate cancellation tokens, but the
UI needs an explicit owner for token creation, command state, disposal, and
user cancellation.

## Decision

Use `ArtworkOperationController` as the single owner of the active artwork
operation's `CancellationTokenSource`.

- Permit one owned artwork operation at a time.
- Expose active and cancellation-requested state.
- Reject overlapping starts and repeated cancel requests.
- Pass the owned token from `MainWindowViewModel` through the complete artwork
  pipeline.
- Dispose the source when the operation completes.
- Cancel active work when the view model is disposed.
- Disable conflicting artwork commands while active.
- Preserve changes completed before cooperative cancellation.

Cancellation does not roll back completed downloads, installations, or library
changes.

## Consequences

- The status bar can offer one predictable cancel action.
- Lower layers remain token consumers rather than token owners.
- Cancellation behavior is testable without constructing the full UI.
- Synchronous Steam discovery cannot stop mid-enumeration; cancellation is
  observed immediately afterward and during all asynchronous artwork work.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Decisions/ADR-004 Artwork Progress Contract|ADR-004 Artwork Progress Contract]]
