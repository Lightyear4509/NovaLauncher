---
type: ai-context
status: active
created: 2026-07-29
updated: 2026-07-30
focus: Windows restriction proof after Plugin SDK Increment 8
---

# AI Context

> [!WARNING]
> This checkout contains a newly verified Increment 0 source foundation. The
> older source solution and test projects described below remain absent, so the
> historical implementation claims cannot be verified here. Continue from
> [[AI/Google AI Studio Build Guide|Google AI Studio Build Guide]] and do not
> treat this report as current build or test evidence.

## Current objective

Build the alpha plugin ecosystem in independently verified increments while
preserving the working launcher and its existing services.

## Implemented state

- Metadata Increment 1 is complete.
- `GameMetadata` is the normalized descriptive metadata shape.
- `LibraryItem` is the long-term canonical library entity.
- The active `Game` UI model carries the same domain metadata type.
- `GameLibraryItemAdapter` is the explicit compatibility boundary.
- `games.json` is written as a schema-versioned document.
- Legacy root-array libraries remain readable.
- Saves use a temporary sibling file and retain the previous valid library as
  `games.json.bak`.
- Unreadable primary libraries fall back to a valid backup and surface a
  warning.
- General game metadata remains separate from asset-folder `AssetMetadata`.
- Metadata Increment 2 is complete.
- `MetadataRequest` isolates providers from mutable library state.
- `MetadataSnapshot` represents one normalized, unmerged provider response.
- `MetadataProviderResult` records success, no match, or typed failure.
- `MetadataProviderManager` filters eligible providers and orders them
  deterministically.
- `MetadataService` queries every eligible provider without mutating or
  persisting library metadata.
- Provider failures are logged and isolated so later providers still run.
- Caller cancellation propagates immediately.
- Provider attribution is validated.
- Metadata progress reports start, query, provider outcome, and completion.
- The metadata manager and service are registered through dependency injection
  with the Steam provider enabled.
- Metadata Increment 3 is complete.
- `SteamMetadataProvider` supports Steam sources with positive numeric app IDs.
- `SteamStoreMetadataClient` isolates the public storefront `appdetails`
  endpoint.
- Steam metadata priority is 100.
- Requests use English, US storefront data.
- Steam descriptions are normalized to safe plain text.
- Developers, publishers, and genres are trimmed and deduplicated.
- Released English dates are parsed conservatively.
- Steam-supplied Metacritic scores use an explicit 0–100 scale.
- Missing, empty, coming-soon, unparseable, and invalid values are omitted
  rather than guessed.
- Returned Steam app identity is validated.
- HTTP, connection, and JSON failures become contextual metadata failures.
- Caller cancellation propagates.
- Steam results remain unmerged snapshots and do not change library data.
- Metadata Increment 4 is complete.
- `MetadataMerger` applies ordered snapshots independently to each managed
  field.
- Manual provenance has absolute precedence.
- Otherwise, the first valid provider value wins and lower-priority snapshots
  fill gaps.
- Missing or invalid provider values preserve the last-known-good value.
- Accepted values record provider name, provider item ID, and retrieval time.
- The overall refresh time never moves backward.
- Accepted lists and ratings are deep-copied.
- `MetadataOverrideService` marks and clears manual field protection.
- Clearing a manual override retains the current value until a provider
  supplies a valid replacement.
- Field provenance survives library serialization and adapter conversion.
- Merge and override services are registered through dependency injection but
  are not invoked directly by the UI.
- Metadata Increment 5 is complete.
- `MetadataRefreshCoordinator` composes read-only retrieval, staged merge, and
  whole-library persistence for one active game.
- `IGameLibraryPersistence` isolates the coordinator from
  `GameLibraryService`.
- The target game must appear by reference once and have a unique ID in the
  supplied collection.
- No accepted provider fields means no save.
- Provider values merge into a staged `LibraryItem`.
- The live `Game.Metadata` changes only after persistence succeeds.
- False-return and thrown persistence failures return a typed result and leave
  the live game unchanged.
- Caller cancellation propagates during retrieval and before persistence.
- Metadata progress now covers merging, saving, refresh completion, and
  persistence failure.
- The coordinator is registered through dependency injection but is not
  invoked by the UI.
- Metadata Increment 6 is complete.
- `MetadataCache` stores only successful normalized snapshots in memory.
- Cache keys use normalized source and platform plus stable provider identity,
  or game ID and name when no provider identity exists.
- Provider item IDs remain case-sensitive.
- Fresh entries skip provider retrieval.
- Stale entries trigger live retrieval and are used only when no live provider
  returns a successful snapshot.
- Entries beyond stale retention are removed.
- Explicit bypass skips cache reads and stale fallback.
- Successful live retrieval replaces the cache entry.
- Empty and failed retrieval is not cached.
- Snapshots are deep-copied on store and lookup.
- Caller cancellation is checked before live results enter the cache.
- Lazy cleanup owns expiration and least-recently-used capacity enforcement.
- Validated defaults are 24-hour freshness, 7-day stale retention, 6-hour
  cleanup interval, and 1,000 entries.
- `MetadataRefreshResult.Source` reports live, fresh-cache, or stale-cache use.
- Game Details Increment 1 is complete.
- `IMetadataRefreshCoordinator` is the UI-facing metadata refresh test seam.
- `GameDetailsViewModel` is now the active child metadata state owner rather
  than an unused callback wrapper.
- `MainWindowViewModel` remains the page data context during the staged
  migration and synchronizes selected game and the active library collection.
- The child owns normalized metadata projections, normal and forced refresh,
  progress text, cancellation, concurrency guards, typed refresh state, and
  user-facing outcome wording.
- Changing selection cancels an active metadata refresh.
- Existing launch, artwork, favorite, rename, and removal commands remain on
  `MainWindowViewModel`.
- The main window now receives the DI-managed `GameLibraryService`; it no
  longer constructs a second persistence instance.
- The active page XAML remains visually unchanged and does not bind the new
  child state outside metadata presentation.
- Game Details Increment 2 is complete.
- The hero uses normalized short description with a safe fallback.
- The page presents full description, genre chips, developer, publisher,
  release date, explicit-scale rating, and last refresh time.
- Games without descriptive metadata receive a clear empty state.
- Normal refresh honors cache policy.
- Force refresh bypasses cache reads and stale fallback.
- Cancel is visible only while metadata refresh is active.
- In-page status text reports progress and final outcomes.
- A result badge distinguishes live providers, fresh cache, and stale fallback.
- Existing launch, artwork, favorite, rename, cover, and removal bindings are
  unchanged.
- Game Details Increment 3 is complete.
- Reusable page styles consume active Nova theme resources.
- The tested layout policy uses wide mode at 900 pixels and compact mode below
  it.
- Compact mode wraps statistics and stacks the management column.
- Metadata actions expose automation names and explanatory tooltips.
- Game Details Increment 4 is complete.
- `IMetadataEditCoordinator` separates edit UI from persistence.
- Edit drafts cover all seven managed metadata fields.
- Dates and ratings are validated before coordination.
- Changed fields receive manual provenance.
- The live game changes only after the staged whole-library save succeeds.
- No-op edits do not save.
- Persistence failure leaves the live metadata unchanged.
- Field sources are visible and manual protection can be cleared without
  deleting the current value.
- Search Increment 1 is complete.
- `ILibrarySearchService` owns provider-neutral token matching, scope, and
  deterministic sorting.
- Active `Game` and canonical `LibraryItem` paths project into the same query
  contract.
- Search covers name, platform, source, provider identity, developers,
  publishers, and genres.
- Ten focused tests raise the suite total to 130.
- Search Increment 2 is complete.
- The active Library page shows query-aware result counts and distinct
  empty-library, empty-scope, and no-match states.
- Search can be cleared from the header or empty state.
- Search controls expose automation names and explanatory tooltips.
- Four presentation tests raise the suite total to 134.
- Collections Increment 1 is complete.
- `GameCollection` stores stable collection and game identities.
- Collections persist separately from `games.json` in a versioned
  `collections.json` document.
- Temporary files are read back before replacement.
- Previous valid collection data is retained as `.bak`.
- Invalid primary data is preserved as `.invalid` before a later save.
- Loads return typed not-found, loaded, recovered, or unrecoverable state.
- Eight tests raise the suite total to 142.
- Collections Increment 2 is complete.
- `CollectionService` stages create, rename, delete, and membership changes.
- Live collection state is published only after the staged document saves.
- Persistence failure leaves live state unchanged.
- `CollectionsPageViewModel` projects collections, members, and available games
  and exposes guarded asynchronous commands.
- Eight coordinator tests raise the suite total to 150.
- Collections Increment 3 is complete.
- The visible Collections page now supports create, rename, delete, add-member,
  and remove-member workflows.
- Collection UI surfaces empty, no-selection, recovery, status, and
  unavailable states using active Nova theme resources.
- Collection actions expose automation names and tooltips.
- Four page view-model tests raise the suite total to 154.
- Themes Increment 1 is complete.
- `ThemeService` owns the canonical five-theme built-in catalog.
- Startup loads settings and applies the saved theme before main-window
  construction.
- Unknown saved IDs fall back to Nova Dark and are normalized best-effort.
- `IThemeHost` isolates Avalonia resource mutation for testability.
- Failed theme persistence restores the previous runtime theme and setting.
- Six theme-service tests raise the suite total to 160.
- Themes Increment 2 is complete.
- Settings surfaces now consume dynamic Nova theme brushes.
- Theme selection exposes progress, saved, failed, and rollback state and is
  disabled while persistence is active.
- `ThemeResourceContract` defines 24 required brushes.
- Tests verify all five built-in theme files, duplicate keys, and every
  application XAML Nova brush reference.
- Three resource-contract tests raise the suite total to 163.
- Theme startup recovery is complete.
- The original process-only smoke missed a UI-thread deadlock during
  asynchronous settings initialization.
- Startup preference preparation now runs off the UI thread; canonical theme
  application remains on the UI thread.
- Desktop startup validation now requires a responding process, a nonzero
  window handle, and the `NovaLauncher` title.
- `ArtworkProviderManager` owns provider filtering and priority ordering.
- `SteamGridDbArtworkProvider` implements `IArtworkProvider`.
- SteamGridDB has priority 200.
- Steam CDN has priority 100 and remains the fallback.
- SteamGridDB registers only when `NOVALAUNCHER_STEAMGRIDDB_API_KEY` is configured.
- The first implementation supports Steam games with numeric Steam app IDs.
- Successful Steam app ID mappings are cached in memory.
- Known `SteamGridDbException` failures return no candidates so fallback continues.
- Cancellation propagates.
- Artwork services are composed through `AppBootstrapper`.
- The application service provider is disposed at desktop exit.
- Steam artwork has been confirmed working.
- Increment 1 adds validated retry/cache settings and shared retry, typed-exception, and progress contracts.
- `ArtworkRetryPolicy` and `ArtworkOptions` are registered through dependency injection.
- Increment 2 applies classified retries to SteamGridDB lookup and artwork downloads.
- Transient failures are network failures, non-caller timeouts, HTTP 408/429, and HTTP 5xx.
- Permanent HTTP failures and invalid content are not retried.
- Exhausted provider failures retain Steam fallback; caller cancellation propagates immediately.
- Provider and download outcomes use structured logging with typed final-failure context.
- Increment 3 enforces cache expiration, interval cleanup, stale `.download` removal, and maximum size.
- Cache size enforcement evicts least-recently-used artwork and protects the path returned by the current download.
- `ArtworkCache.CleanupAsync` returns an `ArtworkCacheCleanupResult` summary.
- Increment 4 generates deterministic in-memory placeholders for cover, hero, logo, and background types.
- Missing or unreadable displayed artwork falls back without installing or counting placeholder assets.
- Increment 5 threads optional progress through provider, retry, cache, service, installer, and Steam-import UI boundaries.
- Existing providers remain compatible through a default progress-aware interface overload.
- Increment 6 owns one UI cancellation source, prevents overlapping artwork operations, and propagates cancellation through the full pipeline.
- Completed Steam-import changes are preserved when later work is cancelled.
- Plugin SDK Increment 1 is complete.
- `NovaLauncher.PluginSdk` is a separate dependency-free `net9.0` contract
  project at `1.0.0-alpha.1`.
- The launcher application does not reference the SDK and performs no plugin
  discovery, installation, extraction, loading, or execution.
- `INovaPlugin` defines cancellable initialization and shutdown with an exact
  immutable plugin ID.
- `IPluginContext` exposes only SDK version, structured logging, and progress.
- Manifest schema version 1 defines namespaced identity, semantic version,
  author, license, homepage, SDK compatibility, entry assembly/type,
  capabilities, and permission declarations.
- Semantic-version comparison handles prerelease precedence and ignores build
  metadata for precedence.
- `.novaplugin` packages are ZIP-compatible archives with root `plugin.json`
  and the declared entry assembly under `lib/`.
- Package validation is read-only and rejects schema drift, incompatible SDK
  ranges, missing entry assemblies, unsafe paths, symbolic links,
  case-insensitive duplicate paths, and configured resource-limit violations.
- Capabilities and permissions are disclosure contracts, not a sandbox.
- `PluginContractTestHarness` validates both the manifest and exact runtime
  identity agreement.
- 34 focused SDK cases raise the complete suite from 163 to 197 tests.
- Plugin SDK Increment 2 is complete.
- `NovaLauncher.PluginManagement` is a separate `net9.0` project that
  references the SDK; the launcher application references neither plugin
  project.
- `JsonPluginInventoryStore` persists schema version 1 through a validated
  temporary file, retains the prior valid inventory as `.bak`, and preserves
  an invalid primary as `.invalid` before replacement.
- Installed versions retain their validated manifest, managed relative path,
  lowercase SHA-256 hash, and installation time.
- Installation copies to private staging first, then validates and hashes the
  exact staged bytes before moving them to versioned managed storage.
- Reinstalling an identical version/hash is idempotent; different bytes under
  an existing version are rejected.
- Updates retain older packages for rollback, select the new version, and
  remain disabled.
- Enable and rollback recheck the selected package hash.
- Missing or changed packages are quarantined.
- Enable, disable, health, quarantine, explicit restoration, rollback, and
  uninstall state persists before the in-memory snapshot changes.
- Consecutive failures quarantine at a configurable threshold.
- Quarantine cannot be bypassed by enable, disable, or success recording.
- Rollback returns to a retained verified version in disabled state.
- Uninstall removes the authoritative inventory entry before deleting only
  package files owned by that entry.
- No plugin assembly is discovered, extracted, loaded, initialized, or
  executed.
- 30 focused lifecycle cases raise the complete suite from 197 to 227 tests.
- Plugin SDK Increment 3 is complete.
- `IPluginOperationProvider<TRequest, TResult>` defines the generic
  provider-operation seam.
- `PluginOperationResult<TResult>` distinguishes success, no result, and typed
  failure without using null as an outcome.
- Plugin failures carry a stable code, safe message, typed kind, retry
  eligibility, and optional `RetryAfter`.
- Caller cancellation remains exception-based and propagates.
- `PluginContractTestHarness` now verifies provider identity and declared
  capability against the manifest.
- Compatibility descriptors record introduction, stability, deprecation,
  earliest removal, and replacement.
- `PluginPackageBuilder` orders inputs, applies fixed ZIP timestamps, writes
  through a temporary file, validates the completed archive, and returns
  SHA-256.
- Builder output requires `.novaplugin`; additional files use strict safe
  file paths.
- `NovaLauncher.PluginTool` exposes local validate, pack, and disabled-install
  commands.
- The starter `dotnet new` template replaces project/namespace and namespaced
  plugin identity consistently.
- The template successfully installed and generated through an isolated real
  .NET template hive.
- `NovaLauncher.Sample.Presence` compiles and demonstrates lifecycle, provider
  operations, typed failures, logging, progress, and cancellation.
- No developer project is referenced by the launcher application, and no
  third-party code is loaded or executed.
- 31 focused developer-experience cases raise the complete suite from 227 to
  258 tests.
- Plugin SDK Increment 4 is complete.
- `NovaLauncher.PluginCatalog` is an isolated project that references SDK and
  management; the launcher application still references no plugin project.
- Catalog schema version 1 signs canonical normalized document bytes with
  RSA-PSS/SHA-256.
- Exact `.novaplugin` package bytes use a separate publisher RSA-PSS signature.
- RSA signing keys must be at least 2048 bits.
- Trusted keys are resolved by exact key ID, owner ID, and catalog or package
  purpose.
- Unknown, wrong-purpose, expired, not-yet-valid, revoked, weak, and malformed
  keys fail trust validation.
- Catalog validation covers schema, namespaced ID, freshness/expiration,
  duplicate plugin versions, manifests, SDK compatibility, HTTPS URIs, package
  sizes/hashes, publisher keys, and catalog signature.
- `GitHubReleaseCatalogSource` fetches one exact asset from the latest public
  release and records owner, repository, release tag, asset, URI, and fetch
  time.
- GitHub owner/repository/asset inputs are path-safe; duplicate assets and
  oversized catalogs fail.
- `HttpPluginPackageTransport` requires HTTPS and enforces declared and streamed
  size limits while reporting progress and propagating cancellation.
- `PluginCatalogInstaller` downloads to private staging and verifies exact
  length, SHA-256, publisher signature, package structure, SDK compatibility,
  and every signed manifest disclosure before requesting consent.
- Consent receives exact capabilities, permissions, publisher key, catalog ID,
  source provenance, update state, and previous version.
- Approved installs and updates remain disabled and execute no code.
- Previous versions are retained. Persistence failure leaves the prior
  inventory authoritative, and explicit rollback verifies the retained hash.
- Staging files are removed after success, failure, denial, or cancellation.
- 30 focused catalog-security cases raise the complete suite from 258 to 288
  tests.
- Plugin SDK Increment 5 is complete.
- `NovaLauncher.PluginRuntime.Protocol` defines a strict, versioned,
  length-prefixed control protocol with a 64 KiB default message bound.
- Protocol version 1 permits only handshake, ready, ping, pong, shutdown,
  stopped, and bounded error messages.
- Unknown fields, message types, versions, malformed IDs, invalid lengths,
  truncated frames, misplaced tokens, and unbounded errors fail closed.
- `NovaLauncher.PluginRuntime` creates a random authenticated session,
  validates exact session/correlation identity, and supervises startup, health,
  and shutdown deadlines.
- `NovaLauncher.PluginHost` is a separate non-executing worker. Its private
  256-bit token is inherited outside the command line, removed immediately,
  and compared in fixed time.
- Timeout, protocol failure, or broken transport faults the session and
  terminates the worker process tree.
- Lifecycle and catalog staging cleanup is age-limited, suffix-specific,
  non-recursive, refuses reparse-point roots, and returns observable results.
- The launcher references no runtime project. The worker accepts no package
  path and cannot discover, extract, load, initialize, or execute plugins.
- 29 focused runtime-boundary cases raise the complete suite from 288 to 317
  tests.
- Plugin SDK Increment 6 is complete.
- Runtime start accepts only explicitly enabled, inventory-owned active
  packages.
- The trusted coordinator rechecks installed SHA-256 before worker launch.
- Typed handoff carries exact identity, version, hash, entry point, package
  path, and session extraction root.
- The worker independently reruns hash, package, SDK, manifest, and identity
  validation.
- Native permission, non-managed DLLs, and metadata-declared P/Invoke fail
  before managed loading.
- Validated entries extract beneath a new session-owned root.
- One collectible load context loads one managed plugin inside one worker.
- Runtime identity must equal manifest identity exactly.
- Initialization and shutdown use deadline-bound protocol messages.
- Successful initialization resets persisted failures; load, initialization,
  health, and shutdown failures use the lifecycle counter and quarantine
  threshold.
- Process termination remains the definitive unload fallback.
- The launcher references no plugin project and no provider operation exists.
- 14 focused managed-runtime cases raise the complete suite from 317 to 331.
- Plugin SDK Increment 7 is complete.
- `PluginManagerService` is the single launcher owner for plugin inventory,
  trust, signed catalog, staged installation, lifecycle, runtime workers, and
  shutdown.
- The launcher deploys the complete host payload but does not reference the
  host assembly in-process.
- The Plugin command center presents available, disabled, enabled, running,
  and quarantined state.
- Catalog refresh is locked until a real GitHub source and trusted catalog and
  publisher keys are configured.
- Exact signed disclosure consent precedes install/update.
- Enable and start remain separate explicit actions; automatic startup is
  false by default.
- Stop, disable, quarantine restore, rollback, confirmed uninstall, progress,
  cancellation, and safe failure states are integrated.
- Provider-operation messages and brokered resource access remain absent.
- Nine focused launcher integration cases raise the complete suite from 331
  to 340.
- Plugin SDK Increment 8 is complete.
- `NovaLauncher.PluginBroker` is an isolated trusted policy project that
  references only the public Plugin SDK.
- File, HTTPS network, process-profile, credential-handle, and game save-data
  requests are separate typed contracts.
- Policies bind exact plugin identity/version, manifest permissions, scopes,
  quotas, timeouts, validity, and enabled state.
- Per-operation consent is the only consent mode and expires within five
  minutes.
- Consent fingerprints use length-prefixed canonical fields and SHA-256,
  binding the complete trusted policy, request identity, target, and declared
  byte ceiling.
- Local paths reject UNC/device roots, noncanonical paths, alternate streams,
  and relative save traversal.
- Network policy is exact HTTPS host/port/method; future DNS address
  classification and pinning remain mandatory.
- Process policy uses trusted profiles and bounded named parameters; plugins do
  not provide executable paths or raw commands.
- Credential requests contain opaque handles only; audit records omit handles,
  purposes, locators, parameters, paths, and secrets.
- Authorization is pure and deny-by-default with invalid, denied,
  consent-required, rate-limited, and allowed outcomes.
- File/save allowance explicitly requires future final-handle verification.
- No executor, transport, protocol message, launcher reference, worker
  reference, or resource operation exists.
- 32 focused broker-policy cases raise the complete suite from 340 to 372.

## Verification

- Debug tests: 372 passed, 0 failed.
- Debug solution build: succeeded, 0 errors, 4 pre-existing nullable warnings.
- Release tests: 372 passed, 0 failed.
- Release solution build: succeeded, 0 errors, 4 pre-existing nullable warnings.
- SDK package: `NovaLauncher.PluginSdk.1.0.0-alpha.1.nupkg` created
  successfully with its README.
- Debug launcher window test: passed (responding, nonzero handle,
  `NovaLauncher` title).
- Release launcher window test: passed (responding, nonzero handle,
  `NovaLauncher` title).
- Brain internal-link audit: 130 Markdown notes, 857 wiki links, 0 broken
  links.
- Source commit: `83814e2`.
- Search Increment 1 source commit: `72045ce`.
- Search Increment 2 source commit: `973dfb5`.
- Collections Increment 1 source commit: `92cb9c4`.
- Collections Increment 2 source commit: `3ee4b9f`.
- Collections Increment 3 source commit: `3f85e5b`.
- Themes Increment 1 source commit: `feb3548`.
- Themes Increment 2 source commit: `1f675f8`.
- Themes startup recovery source commit: `f2d0c3a`.
- Plugin SDK Increment 1 source commit: `65abd5a`.
- Plugin SDK Increment 2 source commit: `2afed33`.
- Plugin SDK Increment 3 source commit: `87098a7`.
- Plugin SDK Increment 4 source commit: `cfd3f6c`.
- Plugin SDK Increment 5 source commit: `e75177a`.
- Plugin SDK Increment 6 source commit: `aa0c02c`.
- Plugin SDK Increment 7 source commit: `a969068`.
- Plugin SDK Increment 8 source commit: pending final commit.

## Important source files

- `Domain/Metadata/GameMetadata.cs`
- `Domain/Library/LibraryItem.cs`
- `Infrastructure/Library/GameLibraryItemAdapter.cs`
- `Models/Game.cs`
- `Services/GameLibraryService.cs`
- `NovaLauncher.Tests/Library/`
- `Services/Metadata/`
- `NovaLauncher.Tests/Metadata/`
- `Services/Steam/ISteamStoreMetadataClient.cs`
- `Services/Steam/SteamStoreMetadataClient.cs`
- `Services/Steam/SteamStoreAppDetails.cs`
- `Services/Metadata/SteamMetadataProvider.cs`
- `Services/Metadata/MetadataMerger.cs`
- `Services/Metadata/MetadataOverrideService.cs`
- `Services/Metadata/MetadataMergeResult.cs`
- `Services/Metadata/MetadataRefreshCoordinator.cs`
- `Services/Metadata/MetadataRefreshResult.cs`
- `Services/Metadata/MetadataRefreshStatus.cs`
- `Services/Metadata/MetadataRefreshSource.cs`
- `Services/Metadata/MetadataCache.cs`
- `Services/Metadata/MetadataCacheKey.cs`
- `Services/Metadata/MetadataCacheLookup.cs`
- `Services/Metadata/MetadataCacheCleanupResult.cs`
- `Services/Metadata/MetadataOptions.cs`
- `Services/IGameLibraryPersistence.cs`
- `Services/Metadata/IMetadataRefreshCoordinator.cs`
- `ViewModels/GameDetailsViewModel.cs`
- `NovaLauncher.Tests/ViewModels/GameDetailsViewModelTests.cs`
- `Views/GameDetails.axaml`
- `Views/GameDetails.axaml.cs`
- `Views/GameDetailsLayoutPolicy.cs`
- `Styles/GameDetails.axaml`
- `Services/Metadata/IMetadataEditCoordinator.cs`
- `Services/Metadata/MetadataEditCoordinator.cs`
- `Services/Metadata/MetadataEditDraft.cs`
- `Services/Metadata/MetadataEditResult.cs`
- `NovaLauncher.Tests/Metadata/MetadataEditCoordinatorTests.cs`
- `NovaLauncher.Tests/Views/GameDetailsLayoutPolicyTests.cs`
- `docs/GameDetailsMetadata.md`
- `docs/GameDetailsPolish.md`
- `docs/MetadataEditing.md`
- `Domain/Metadata/MetadataField.cs`
- `Domain/Metadata/MetadataFieldProvenance.cs`
- `Domain/Metadata/MetadataSourceKind.cs`
- `Services/Artwork/SteamGridDbArtworkProvider.cs`
- `Services/Artwork/ArtworkProviderManager.cs`
- `Services/Artwork/ArtworkService.cs`
- `Services/Artwork/ArtworkOptions.cs`
- `Services/Artwork/ArtworkRetryPolicy.cs`
- `Services/Artwork/ArtworkException.cs`
- `Services/Artwork/ArtworkProgress.cs`
- `Services/Artwork/ArtworkCache.cs`
- `Services/Artwork/ArtworkCacheCleanupResult.cs`
- `Services/Artwork/ArtworkPlaceholderService.cs`
- `Converters/CoverImageService.cs`
- `Services/Artwork/ArtworkProgress.cs`
- `Services/Artwork/ArtworkProgressStage.cs`
- `Services/Artwork/ArtworkOperationController.cs`
- `Services/SteamGridDb/ISteamGridDbService.cs`
- `Services/SteamGridDb/SteamGridDbService.cs`
- `Core/Bootstrap/AppBootstrapper.cs`
- `ViewModels/MainWindowViewModel.cs`
- `NovaLauncher.Tests/Artwork/`
- `Services/Search/`
- `NovaLauncher.Tests/Search/`
- `docs/Search.md`
- `Domain/Collections/`
- `Services/Collections/`
- `NovaLauncher.Tests/Collections/`
- `docs/Collections.md`
- `ViewModels/Pages/CollectionsPageViewModel.cs`
- `ViewModels/Pages/CollectionsPage.axaml`
- `NovaLauncher.Tests/ViewModels/CollectionsPageViewModelTests.cs`
- `Core/Theming/ThemeService.cs`
- `Core/Theming/IThemeHost.cs`
- `Core/Theming/AvaloniaThemeHost.cs`
- `NovaLauncher.Tests/Theming/ThemeServiceTests.cs`
- `Core/Theming/ThemeResourceContract.cs`
- `NovaLauncher.Tests/Theming/ThemeResourceContractTests.cs`
- `ViewModels/Pages/SettingsPage.axaml`
- `App.axaml.cs`
- `docs/Themes.md`
- `NovaLauncher.PluginSdk/NovaLauncher.PluginSdk.csproj`
- `NovaLauncher.PluginSdk/Abstractions/`
- `NovaLauncher.PluginSdk/Manifest/`
- `NovaLauncher.PluginSdk/Packaging/`
- `NovaLauncher.PluginSdk/Testing/`
- `NovaLauncher.PluginSdk/Versioning/`
- `NovaLauncher.Tests/PluginSdk/`
- `NovaLauncher/docs/PluginSdk.md`
- `NovaLauncher.PluginManagement/NovaLauncher.PluginManagement.csproj`
- `NovaLauncher.PluginManagement/Lifecycle/`
- `NovaLauncher.PluginManagement/Models/`
- `NovaLauncher.PluginManagement/Persistence/`
- `NovaLauncher.Tests/PluginManagement/`
- `NovaLauncher/docs/PluginLifecycle.md`
- `NovaLauncher.PluginSdk/Operations/`
- `NovaLauncher.PluginSdk/Packaging/PluginPackageBuilder.cs`
- `NovaLauncher.PluginSdk/Versioning/PluginCompatibilityPolicy.cs`
- `NovaLauncher.PluginTool/`
- `samples/NovaLauncher.Sample.Presence/`
- `templates/NovaLauncher.Plugin/`
- `NovaLauncher.Tests/PluginSdk/PluginOperationContractTests.cs`
- `NovaLauncher.Tests/PluginSdk/PluginPackageBuilderTests.cs`
- `NovaLauncher.Tests/PluginSdk/PluginToolApplicationTests.cs`
- `NovaLauncher.Tests/PluginSdk/PluginTemplateTests.cs`
- `NovaLauncher.Tests/PluginSdk/SamplePluginTests.cs`
- `NovaLauncher/docs/PluginDeveloperGuide.md`
- `NovaLauncher/docs/PluginVersioning.md`
- `NovaLauncher.PluginCatalog/Catalog/`
- `NovaLauncher.PluginCatalog/Downloads/`
- `NovaLauncher.PluginCatalog/Installation/`
- `NovaLauncher.PluginCatalog/Models/`
- `NovaLauncher.PluginCatalog/Security/`
- `NovaLauncher.PluginCatalog/Sources/`
- `NovaLauncher.Tests/PluginCatalog/`
- `NovaLauncher/docs/PluginCatalog.md`
- `NovaLauncher.PluginRuntime.Protocol/`
- `NovaLauncher.PluginRuntime/Hosting/`
- `NovaLauncher.PluginHost/`
- `NovaLauncher.PluginManagement/Maintenance/`
- `NovaLauncher.Tests/PluginRuntime/`
- `NovaLauncher/docs/PluginRuntimeHost.md`
- `NovaLauncher.PluginRuntime/Coordination/`
- `NovaLauncher.PluginHost/PluginHostRuntime.cs`
- `NovaLauncher.PluginHost/PluginAssemblyLoadContext.cs`
- `testassets/NovaLauncher.RuntimeProbe.Plugin/`
- `NovaLauncher/Services/Plugins/`
- `NovaLauncher/ViewModels/Pages/PluginsPageViewModel.cs`
- `NovaLauncher/ViewModels/Pages/PluginsPage.axaml`
- `NovaLauncher/Core/Bootstrap/PluginOptions.cs`
- `NovaLauncher.Tests/Plugins/`
- `NovaLauncher/docs/PluginLauncherIntegration.md`
- `NovaLauncher.PluginBroker/`
- `NovaLauncher.Tests/PluginBroker/`
- `NovaLauncher/docs/PluginBrokerPolicy.md`

## Constraints for the next change

- Do not store API keys in source control or the Brain vault.
- Preserve Steam CDN fallback.
- Do not add fuzzy name matching until ambiguity and user selection are designed.
- Preserve the user's confirmed working Steam artwork behavior.
- Preserve the existing dirty-worktree changes unless they are intentionally reconciled.
- All six planned artwork-hardening increments are implemented.
- Treat candidate-after-validation retry, `Retry-After`, and background cleanup as separate follow-ups.
- Do not add provider-specific response fields directly to `GameMetadata`.
- Keep `AssetMetadata` separate from general descriptive game metadata.
- Preserve legacy root-array loading until a later migration policy explicitly
  retires it.
- Keep `MetadataService.RetrieveAsync` read-only; coordination remains an
  explicit higher layer.
- Treat the public Steam storefront endpoint as a compatibility boundary; do
  not expose its response types in the domain.
- Do not infer missing Steam dates, ratings, or descriptive fields.
- Increment 6 does not include metadata UI, persistent disk cache, background
  cleanup timers, retries, rate limiting, bulk refresh, or asynchronous
  persistence.
- Preserve field-level provenance when adding future persistence migrations.
- Provider snapshots remain mutable compatibility objects, so the merger must
  continue deep-copying accepted collections and ratings.
- Keep the live game unchanged until persistence succeeds.
- The current synchronous whole-library save cannot be cancelled after it
  begins.
- Cache entries are process-local and disappear at application exit.
- Provider registration changes require freshness expiration or explicit
  bypass before a fresh entry is replaced.
- Keep the fixed hero alpha overlays; they provide contrast over arbitrary
  artwork while ordinary page surfaces follow theme resources.
- Manual edits must remain staged until persistence succeeds.
- Clearing manual protection must retain the current field value.
- The 900-pixel layout threshold is a documented, tested presentation policy.
- Keep existing main-view-model page commands in place until their migration is
  separately approved and tested.
- Keep the launcher independent from all plugin projects until the integration
  increment is explicitly approved.
- Do not extract or load third-party assemblies during package inspection.
- Do not load third-party plugin assemblies into the launcher process.
- A normal worker process is crash and dependency isolation, not an
  enforceable permission sandbox.
- Require brokered capabilities or a proven OS sandbox before describing
  manifest permissions as enforced.
- Keep native plugin code prohibited in the alpha runtime plan.
- Keep `1.0.0-alpha.1` labeled as a preview until compatibility and deprecation
  policy are finalized.
- Treat persisted `Enabled` as future activation eligibility only; Increment 2
  does not execute plugins.
- Do not treat SHA-256 as publisher authentication.
- Preserve older version packages needed for rollback.
- Publish lifecycle state only after inventory persistence succeeds.
- Do not recursively delete unknown content during uninstall.
- A future launcher integration needs one inventory owner or explicit
  cross-process locking.
- Keep operation failure codes stable within compatible SDK versions.
- Use `NoResult` rather than null success values.
- Propagate caller cancellation rather than translating it into a failure.
- Keep the CLI local/developer-only until consent and publisher trust are
  designed.
- Do not claim byte-for-byte reproducibility across different .NET runtime
  implementations.
- The template depends on an unpublished alpha SDK package and requires a
  local package source for now.
- Never treat catalog provenance alone as cryptographic trust; require a
  configured catalog signing key.
- Keep catalog and publisher signing keys purpose-separated.
- Consent must use the exact downloaded and signed manifest, not catalog UI
  cache state.
- Do not install after checksum, signature, package, SDK, or disclosure
  mismatch.
- Keep catalog installs and updates disabled until separately enabled.
- Preserve the previous installed version until the update is proven healthy.
- Root-key distribution, rotation, and emergency revocation delivery require a
  separate operational design.
- Do not add a generic runtime command or arbitrary protocol payload.
- Runtime package handoff must be typed, inventory-owned, enabled, and
  revalidated on both sides.
- Every runtime exchange requires exact protocol, session, and correlation
  validation plus a bounded deadline.
- Use process termination as the definitive fallback after timeout, protocol
  failure, or unload failure.
- Reject native permission, non-managed binaries, and declared P/Invoke before
  loading.
- Do not describe managed/native inspection as an OS sandbox.
- Keep provider-operation messages disabled until brokered resource boundaries
  are approved.

## Recommended next step

Plan Increment 7 as a launcher-owned plugin service and catalog/consent UI.
Compose one inventory/catalog/runtime owner, keep startup opt-in and off the UI
thread, surface disabled/enabled/quarantined state and recovery, and preserve
exact signed consent. Do not add resource-bearing provider operations until
broker contracts or OS restrictions are separately approved.

## Related

- [[Product/Features/SteamGridDB Artwork Provider|SteamGridDB Artwork Provider]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Product/Features/Artwork System Hardening|Artwork System Hardening]]
- [[Decisions/ADR-002 Artwork Resilience Policy|ADR-002 Artwork Resilience Policy]]
- [[Decisions/ADR-003 Placeholder Artwork Policy|ADR-003 Placeholder Artwork Policy]]
- [[Decisions/ADR-004 Artwork Progress Contract|ADR-004 Artwork Progress Contract]]
- [[Decisions/ADR-005 Artwork Cancellation Ownership|ADR-005 Artwork Cancellation Ownership]]
- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Decisions/ADR-006 Metadata Domain and Persistence Boundary|ADR-006 Metadata Domain and Persistence Boundary]]
- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Product/Features/Metadata Merge and Override Policy|Metadata Merge and Override Policy]]
- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[AI/Metadata Pipeline Increment 4 Report|Metadata Pipeline Increment 4 Report]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[AI/Metadata Pipeline Increment 5 Report|Metadata Pipeline Increment 5 Report]]
- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-009 Metadata Cache Freshness and Stale Fallback|ADR-009 Metadata Cache Freshness and Stale Fallback]]
- [[AI/Metadata Pipeline Increment 6 Report|Metadata Pipeline Increment 6 Report]]
- [[Product/Features/Game Details Metadata Experience|Game Details Metadata Experience]]
- [[Decisions/ADR-010 Game Details State Ownership|ADR-010 Game Details State Ownership]]
- [[AI/Game Details Increment 1 Report|Game Details Increment 1 Report]]
- [[AI/Game Details Increment 2 Report|Game Details Increment 2 Report]]
- [[Decisions/ADR-011 Atomic Manual Metadata Editing|ADR-011 Atomic Manual Metadata Editing]]
- [[AI/Game Details Increment 3 Report|Game Details Increment 3 Report]]
- [[AI/Game Details Increment 4 Report|Game Details Increment 4 Report]]
- [[Product/Features/Library Search|Library Search]]
- [[Decisions/ADR-012 Unified Library Query Boundary|ADR-012 Unified Library Query Boundary]]
- [[AI/Search Increment 1 Report|Search Increment 1 Report]]
- [[AI/Search Increment 2 Report|Search Increment 2 Report]]
- [[Product/Features/Collection Management|Collection Management]]
- [[Decisions/ADR-013 Separate Collection Persistence|ADR-013 Separate Collection Persistence]]
- [[AI/Collections Increment 1 Report|Collections Increment 1 Report]]
- [[AI/Collections Increment 2 Report|Collections Increment 2 Report]]
- [[AI/Collections Increment 3 Report|Collections Increment 3 Report]]
- [[Product/Features/Theme Reliability|Theme Reliability]]
- [[Decisions/ADR-014 Atomic Theme Application and Persistence|ADR-014 Atomic Theme Application and Persistence]]
- [[AI/Themes Increment 1 Report|Themes Increment 1 Report]]
- [[AI/Themes Increment 2 Report|Themes Increment 2 Report]]
- [[AI/Themes Startup Recovery Report|Themes Startup Recovery Report]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[AI/Alpha Ecosystem Roadmap Report|Alpha Ecosystem Roadmap Report]]
- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- [[Product/Features/Authorized Acquisition and Installation|Authorized Acquisition and Installation]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[AI/Plugin SDK Increment 1 Report|Plugin SDK Increment 1 Report]]
- [[AI/Plugin SDK Increment 2 Report|Plugin SDK Increment 2 Report]]
- [[AI/Plugin SDK Increment 3 Report|Plugin SDK Increment 3 Report]]
- [[AI/Plugin SDK Increment 4 Report|Plugin SDK Increment 4 Report]]
- [[AI/Plugin SDK Increment 5 Report|Plugin SDK Increment 5 Report]]
- [[AI/Plugin SDK Increment 6 Report|Plugin SDK Increment 6 Report]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/Plugin SDK Increment 8 Report|Plugin SDK Increment 8 Report]]
