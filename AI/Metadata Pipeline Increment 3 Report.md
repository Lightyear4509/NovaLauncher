---
type: report
status: complete
scope: Metadata Pipeline Increment 3
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Increment 3 Report

## Outcome

Increment 3 is complete. NovaLauncher now has a Steam metadata provider
registered against the Increment 2 contracts.

The provider retrieves and normalizes Steam storefront data into an unmerged
`MetadataSnapshot`. It does not modify or persist library metadata, so existing
UI, library, launch, artwork, playtime, and save behavior remains unchanged.

## Eligibility

- Source ID equals Steam, case-insensitively.
- Provider item ID is a positive 32-bit numeric Steam app ID.
- Provider priority is 100.

## Runtime behavior

- Request English, US storefront metadata for deterministic parsing.
- Validate that Steam returns the requested app ID.
- Return no match when Steam reports no app or supplies no useful fields.
- Normalize HTML descriptions to decoded plain text.
- Trim and case-insensitively deduplicate developers, publishers, and genres.
- Parse only released, recognized English release dates.
- Normalize a Steam-supplied Metacritic score to an explicit 0–100 rating.
- Omit coming-soon, unparseable, empty, or invalid values rather than guessing.
- Record retrieval time on the snapshot and normalized metadata.
- Convert HTTP, connection, empty-response, JSON, and identity failures into
  contextual `MetadataException` values.
- Preserve caller cancellation.
- Return an unmerged snapshot without changing the library.

## Endpoint boundary

`SteamStoreMetadataClient` isolates:

`https://store.steampowered.com/api/appdetails`

The request uses `appids`, `l=english`, and `cc=us`. The endpoint does not
require a Web API key, but it is not part of Steam's documented partner Web API
contract. All endpoint-specific response types remain outside the domain.

## Source changes

### Created

- `NovaLauncher/Services/Steam/ISteamStoreMetadataClient.cs`
- `NovaLauncher/Services/Steam/SteamStoreAppDetails.cs`
- `NovaLauncher/Services/Steam/SteamStoreMetadataClient.cs`
- `NovaLauncher/Services/Metadata/SteamMetadataProvider.cs`
- `NovaLauncher.Tests/Metadata/SteamStoreMetadataClientTests.cs`
- `NovaLauncher.Tests/Metadata/SteamMetadataProviderTests.cs`
- `NovaLauncher/docs/SteamMetadata.md`

### Modified

- `NovaLauncher/Core/Bootstrap/AppBootstrapper.cs`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`
- `NovaLauncher/docs/MetadataProviders.md`

## Vault changes

### Created

- `Product/Features/Steam Metadata Provider.md`
- `AI/Metadata Pipeline Increment 3 Report.md`

### Modified

- `AI/AI Context.md`
- `Dashboard/Development Dashboard.md`
- `Engineering/Architecture/Technical Architecture Specification.md`
- `Engineering/Services/Metadata Service.md`
- `Planning/Releases/Changelog.md`
- `Planning/Roadmap/Master Roadmap.md`
- `Planning/Sprints/Current Sprint.md`
- `Planning/Sprints/Sprint Board.md`
- `Product/Epics/Metadata.md`

## Verification

- Debug solution build: succeeded, 0 errors
- Debug test suite: 73 passed, 0 failed
- Release solution build: succeeded, 0 errors
- Release test suite: 73 passed, 0 failed
- Git whitespace check: passed
- Vault link audit: 82 notes checked, 0 unresolved wikilinks
- Live read-only Steam smoke test: app 620 returned success, matching app ID,
  `Portal 2`, and a non-empty short description

Eighteen new test cases verify:

- provider eligibility and numeric app-ID bounds
- endpoint URI, locale, and country parameters
- response deserialization
- missing-app behavior
- HTTP failure conversion
- malformed JSON conversion
- returned app-ID validation
- client cancellation
- provider field normalization
- HTML-to-text conversion
- list trimming and deduplication
- released-date parsing
- coming-soon and uncertain-value omission
- Metacritic scale normalization and invalid-score omission
- missing and empty provider responses
- provider cancellation

The builds report four pre-existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. That
file is outside Increment 3.

The Release build used `AVALONIA_TELEMETRY_OPTOUT=1` and disabled build-server
reuse because the managed execution environment does not permit Avalonia's
telemetry task to write its user-profile log.

## Increment boundary

The following are not implemented:

- snapshot merge or persistence
- field provenance
- manual overrides
- retry or rate-limit handling
- metadata cache or expiration
- metadata UI
- bulk refresh
- achievements
- IGDB, RAWG, or fuzzy name matching

## Remaining risks

- Steam's public storefront `appdetails` endpoint is not part of the documented
  partner Web API contract and may change without a versioned contract.
- The provider currently has no retry or cache policy.
- English storefront date parsing intentionally omits unknown formats.
- HTML normalization supports common storefront block and break markup but is
  not a general-purpose HTML parser.
- Metacritic is the only rating normalized because its scale is explicit in the
  response.
- The registered provider is not invoked by the UI until merge, persistence,
  and refresh behavior are implemented.
- The repository still contains earlier uncommitted artwork and metadata
  increments.

## Commit readiness

Increment 3 is compile-safe and test-verified. Stage the Increment 3 paths
listed above deliberately rather than staging the entire dirty working tree.
`AppBootstrapper.cs`, source documentation, and vault planning notes also
contain earlier increment changes and should be reviewed during focused
staging.

## Recommended next step

Define and implement deterministic metadata merge, field provenance, and
manual-override policy. Provider snapshots should remain read-only until that
policy is approved.

## Related

- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
