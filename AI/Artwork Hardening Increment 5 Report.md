---
type: report
status: complete
scope: Artwork Hardening Increment 5
created: 2026-07-29
updated: 2026-07-29
---

# Artwork Hardening Increment 5 Report

## Outcome

Increment 5 is complete. Artwork progress now flows from provider resolution
through retrieval, retry, cache, validation, installation, and completion.
Steam import and manual cover installation surface these messages through the
existing status text. Callers that omit progress retain existing behavior.

## Reported stages

- `ResolvingProvider`
- `QueryingProvider`
- `Downloading`
- `CacheHit`
- `Retrying`
- `Validating`
- `Installing`
- `CandidateFailed`
- `FallingBack`
- `Completed`
- `Cleanup`

Events include game, artwork type, provider, item counts, and retry attempt
where applicable.

## Compatibility

The original `IArtworkProvider.GetArtworkUrisAsync` method remains the required
provider contract. A default progress-aware overload delegates to it. Existing
providers therefore continue to compile and run without implementing progress;
providers such as SteamGridDB can override the overload to forward retry
details.

Progress parameters are optional on cache, service, installer, and cleanup
operations. Existing call sites remain valid.

## Source changes

### Created

- `NovaLauncher.Tests/Artwork/ArtworkProgressTests.cs`

### Modified

- `NovaLauncher/Services/Artwork/IArtworkProvider.cs`
- `NovaLauncher/Services/Artwork/SteamArtworkProvider.cs`
- `NovaLauncher/Services/Artwork/SteamGridDbArtworkProvider.cs`
- `NovaLauncher/Services/Artwork/ArtworkRetryPolicy.cs`
- `NovaLauncher/Services/Artwork/ArtworkCache.cs`
- `NovaLauncher/Services/Artwork/ArtworkService.cs`
- `NovaLauncher/Services/Artwork/ArtworkInstaller.cs`
- `NovaLauncher/ViewModels/MainWindowViewModel.cs`
- `NovaLauncher.Tests/Artwork/ArtworkProviderManagerTests.cs`
- `NovaLauncher/docs/ArtworkHardening.md`
- `NovaLauncher/docs/Architecture.md`
- `NovaLauncher/docs/Changelog.md`
- `NovaLauncher/docs/CodeReview.md`

## Vault changes

### Created

- `Decisions/ADR-004 Artwork Progress Contract.md`
- `AI/Artwork Hardening Increment 5 Report.md`

### Modified

- `AI/AI Context.md`
- `Dashboard/Development Dashboard.md`
- `Engineering/Architecture/Artwork System.md`
- `Engineering/Services/Artwork Service.md`
- `Planning/Releases/Changelog.md`
- `Planning/Roadmap/Master Roadmap.md`
- `Planning/Sprints/Current Sprint.md`
- `Planning/Sprints/Sprint Board.md`
- `Product/Features/Artwork System Hardening.md`
- `Product/Features/SteamGridDB Artwork Provider.md`

## Verification

- Debug test suite: 37 passed, 0 failed
- Full serial Release rebuild: succeeded, 0 errors
- Release test suite: 37 passed, 0 failed
- Application configuration JSON parse: passed
- Git whitespace check: passed
- Vault link audit: 73 notes checked, 0 unresolved wikilinks

The Release rebuild reports four existing nullable warnings in
`Views/Controls/CinematicHero.axaml.cs` at lines 193, 196, 241, and 249. They
are outside this increment and were not modified.

Focused coverage verifies:

- retry progress carries game, artwork type, provider, and attempt context
- an end-to-end download/install operation reports provider resolution,
  provider query, download, validation, installation, and final completion
- progress omission preserves every existing Increment 1 through Increment 4
  test path

## Increment boundary

The following remain intentionally unimplemented:

- UI cancellation-token ownership
- user-facing cancel commands or controls
- retrying another candidate after post-download image validation fails
- honoring provider `Retry-After` response headers
- background cache cleanup independent of artwork access

## Remaining risks

- `Progress<T>` dispatches through the captured UI synchronization context, so
  visual status updates are asynchronous by design.
- Progress exposes stages and item counts, not byte-level download percentage,
  because current HTTP streaming does not expose a stable total for every
  provider response.
- Custom progress reporters are expected to follow the normal `IProgress<T>`
  contract and avoid throwing from `Report`.

## Commit readiness

The Increment 5 paths above are independently scoped and verified. The source
repository still contains earlier uncommitted provider, Increment 1 through
Increment 4, and repository-cleanup work. A focused commit should stage the
listed Increment 5 source paths deliberately rather than staging the entire
working tree.

## Recommended next step

Add explicit UI cancellation ownership and controls while preserving the
existing immediate token propagation in lower layers.

## Related

- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-004 Artwork Progress Contract|ADR-004 Artwork Progress Contract]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
