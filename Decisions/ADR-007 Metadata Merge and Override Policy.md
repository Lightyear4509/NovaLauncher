---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-007 Metadata Merge and Override Policy

## Context

Metadata providers return ordered, normalized snapshots. NovaLauncher needs a
deterministic way to select each field without erasing useful data or
overwriting user edits.

Provider precedence can differ by field because a high-priority provider may
omit data that a lower-priority provider supplies. A whole-object winner would
discard useful values.

## Decision

- Merge field-by-field rather than selecting one complete snapshot.
- Give manual provenance absolute precedence.
- Otherwise accept the first valid value in snapshot order.
- Let lower-priority snapshots fill missing higher-priority fields.
- Preserve the last-known-good value when every snapshot omits or invalidates
  a field.
- Record provider name, provider item ID, and retrieval time for each accepted
  field.
- Advance the overall refresh time only when at least one provider field is
  accepted.
- Deep-copy accepted collections and ratings.
- Mark an edited value manual only through an explicit override operation.
- Clearing a manual override removes protection but does not delete the current
  value.

## Consequences

- Provider order remains deterministic but does not force a whole-record
  winner.
- Empty provider values cannot erase useful metadata.
- Users retain control over every managed field.
- Provenance can explain which provider supplied a displayed value.
- Clearing an override is safe and non-destructive.
- The future refresh workflow must explicitly call the merger and then persist
  the changed library item.

## Related

- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
