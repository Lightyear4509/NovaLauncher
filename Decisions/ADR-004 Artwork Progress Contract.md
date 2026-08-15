---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-004 Artwork Progress Contract

## Context

Artwork work crosses provider lookup, retry, download, cache, validation, and
installation boundaries. The UI needs useful status without depending on
individual providers or taking ownership of lower-level implementation details.

## Decision

Use optional `IProgress<ArtworkProgress>` parameters across artwork
boundaries. Report:

- stage and human-readable message
- game and artwork type
- provider when applicable
- current item and total items when applicable
- retry attempt when applicable

Keep the original `IArtworkProvider.GetArtworkUrisAsync` method as the required
provider contract. Add a default progress-aware overload that delegates to the
original method. Providers may override the overload for detailed progress,
while existing providers continue to compile and run unchanged.

Progress is observational. It does not create, cancel, or dispose cancellation
tokens.

## Consequences

- The Steam-import UI can display end-to-end operation status.
- Non-UI callers can omit progress without behavior changes.
- Existing providers remain compatible.
- UI cancellation remains a separate lifecycle decision.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
