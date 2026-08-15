# ADR-0005: Steam import identity and revision-bound preview

- Status: Accepted
- Date: 2026-08-13
- Increment: 3

## Context

Steam manifests are external, mutable, and potentially malformed. NovaLauncher
must reimport without duplicating games or overwriting user-owned organization
and edits. A preview must describe the state that will actually be committed.

## Decision

Steam App ID is the external identity. `GameId.FromSteamAppId` derives a stable,
namespaced identifier, while `SourceItemId` retains the decimal App ID and
`ImportedName` records the last provider-owned name. A manually changed display
name is preserved when the provider name later changes.

Discovery reads only supported Steam registry values or an explicit absolute
local root. A bounded parser accepts the Valve Data Format object shape and
treats each invalid manifest as an isolated failure. No Steam input is modified.

The application creates a dry-run preview under the library mutation gate and
records the library revision. Commit requires the same revision, recomputes the
merge against live state, persists one complete staged document, and only then
publishes it. Any stale preview, cancellation, or save failure publishes nothing.

## Consequences

- Repeated imports are deterministic and do not duplicate Steam games.
- Favorites, collection IDs, metadata, manual names, and stable IDs survive
  reimport.
- A library change after preview requires a new preview.
- Plugins, credentials, authentication files, network metadata, and Steam-file
  writes remain outside this increment.
