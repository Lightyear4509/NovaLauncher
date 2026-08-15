---
type: report
status: complete
scope: Metadata Pipeline Increment 2
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Pipeline Increment 2 Report

## Outcome

Increment 2 is complete. NovaLauncher now has provider-neutral metadata
contracts, deterministic provider selection, and read-only orchestration.

No external metadata provider is registered. Retrieval does not merge, modify,
cache, or persist library metadata, so existing application behavior remains
unchanged.

## Runtime behavior

- Convert a canonical `LibraryItem` into an immutable, normalized
  `MetadataRequest`.
- Filter providers by request eligibility.
- Order eligible providers by descending priority, provider name, and concrete
  type name.
- Ignore duplicate registrations of the same concrete provider type.
- Query all eligible providers in order.
- Retain success, no-match, and failure outcomes.
- Validate that provider result attribution matches the queried provider.
- Convert non-cancellation provider exceptions into contextual
  `MetadataException` failures.
- Log provider failures and continue with lower-priority providers.
- Propagate caller cancellation immediately.
- Report start, provider query, provider outcome, and completion progress.
- Return ordered, unmerged snapshots without changing the library item.
- Operate safely with no registered metadata providers.

## Contracts

- `MetadataRequest` — immutable library identity
- `MetadataSnapshot` — one normalized, unmerged provider response
- `MetadataProviderResult` — success, no match, or failure
- `MetadataProviderStatus` — provider outcome classification
- `MetadataException` — operation, provider, and game failure context
- `MetadataProgress` and `MetadataProgressStage` — structured progress
- `IMetadataProvider` — eligibility and asynchronous retrieval contract
- `MetadataProviderManager` — registration, filtering, and ordering
- `MetadataRetrievalResult` — ordered outcomes and successful snapshots
- `MetadataService` — read-only provider orchestration

## Source changes

### Created

- `NovaLauncher/Services/Metadata/IMetadataProvider.cs`
- `NovaLauncher/Services/Metadata/MetadataException.cs`
- `NovaLauncher/Services/Metadata/MetadataProgress.cs`
- `NovaLauncher/Services/Metadata/MetadataProgressStage.cs`
- `NovaLauncher/Services/Metadata/MetadataProviderManager.cs`
- `NovaLauncher/Services/Metadata/MetadataProviderResult.cs`
- `NovaLauncher/Services/Metadata/MetadataProviderStatus.cs`
- `NovaLauncher/Services/Metadata/MetadataRequest.cs`
- `NovaLauncher/Services/Metadata/MetadataRetrievalResult.cs`
- `NovaLauncher/Services/Metadata/MetadataService.cs`
- `NovaLauncher/Services/Metadata/MetadataSnapshot.cs`
- `NovaLauncher.Tests/Metadata/MetadataProviderManagerTests.cs`
- `NovaLauncher.Tests/Metadata/MetadataServiceTests.cs`
- `NovaLauncher/docs/MetadataProviders.md`

### Modified

- `NovaLauncher/Core/Bootstrap/AppBootstrapper.cs`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`

## Vault changes

### Created

- `Product/Features/Metadata Provider Contracts.md`
- `AI/Metadata Pipeline Increment 2 Report.md`

### Modified

- `AI/AI Context.md`
- `Dashboard/Development Dashboard.md`
- `Decisions/ADR-001 Provider Architecture.md`
- `Engineering/Architecture/Technical Architecture Specification.md`
- `Engineering/Services/Metadata Service.md`
- `Planning/Releases/Changelog.md`
- `Planning/Roadmap/Master Roadmap.md`
- `Planning/Sprints/Current Sprint.md`
- `Planning/Sprints/Sprint Board.md`
- `Product/Epics/Metadata.md`

## Verification

- Debug solution build: succeeded, 0 errors
- Debug test suite: 55 passed, 0 failed
- Release solution build: succeeded, 0 errors
- Release test suite: 55 passed, 0 failed
- Git whitespace check: passed
- Vault link audit: 80 notes checked, 0 unresolved wikilinks

The Release build used `AVALONIA_TELEMETRY_OPTOUT=1` and disabled build-server
reuse because the managed execution environment does not permit Avalonia's
telemetry task to write its user-profile log.

Eight new tests verify:

- eligibility filtering
- deterministic priority and name ordering
- duplicate concrete-type registration
- ordered unmerged outcomes
- no mutation of existing library metadata
- provider failure isolation
- provider attribution validation
- structured progress order
- active caller cancellation
- operation with no eligible providers

The Debug build continues to report four pre-existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. That
file is outside Increment 2.

## Increment boundary

The following are not implemented:

- Steam or another external metadata provider
- HTTP access
- retry or rate-limit policy
- snapshot merge precedence
- field provenance
- manual overrides
- persistence of retrieved snapshots
- metadata cache or expiration
- metadata UI
- bulk refresh

## Remaining risks

- `CanProvideMetadata` is expected to be fast and side-effect free; an
  eligibility method that throws currently prevents provider selection.
- Provider snapshots contain mutable `GameMetadata` objects. The later merge
  layer must copy accepted values rather than retain mutable provider-owned
  references.
- Failure isolation has automated fake-provider coverage but no real network
  provider coverage yet.
- The application registers an empty metadata-provider collection, so this
  increment intentionally has no user-visible metadata retrieval.
- The source repository still contains earlier uncommitted artwork and
  metadata-foundation changes.

## Commit readiness

Increment 2 is compile-safe and test-verified. Stage the Increment 2 source
paths listed above deliberately rather than staging the entire dirty working
tree. `AppBootstrapper.cs`, `Architecture.md`, and `Changelog.md` also contain
earlier increment changes and should be reviewed when preparing focused
commits.

## Recommended next step

Implement a Steam metadata provider against `IMetadataProvider`, beginning with
numeric Steam app IDs and provider-level normalization. Keep merge, persistence,
cache, overrides, and UI changes outside that increment.

## Related

- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
