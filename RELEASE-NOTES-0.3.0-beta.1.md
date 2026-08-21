# NovaLauncher 0.3.0-beta.1

This unsigned Windows beta adds confirmation-first enrichment for manually
installed games while preserving NovaLauncher's local-first launch model.

## Highlights

- Match manual games against read-only local Steam manifests or optional
  session-only SteamGridDB search, with explicit candidate confirmation.
- Keep confirmed provider identity separate from the executable and undo or
  rematch it without changing how the game launches.
- Retrieve bounded metadata and artwork only after confirmation.
- Override description, genres, developers, publishers, and release date while
  preserving manual values through refresh and offline provider failures.
- Manage cover, hero, logo, and background artwork; review provider variants;
  crop managed images; inspect the cache; and remove bounded orphan assets.
- Request read-only Steam achievements for manual games only when a confirmed
  Steam App ID and the existing user-provided Steam credentials are available.

## Safety and compatibility

- Steam discovery is read-only. NovaLauncher does not read Steam credentials or
  modify Steam files.
- Candidate search never selects the first result or downloads artwork automatically.
- SteamGridDB-only identity is not treated as Steam ownership or achievement proof.
- No plugins, downloaded-code execution, telemetry, ROM acquisition, ownership
  bypass, or generic Steam multiplayer emulation is included.
- The release remains unsigned. Verify `SHA256SUMS.txt` before running it and do
  not disable Windows security protections globally.

Automated verification passed 218 tests in Debug and Release configurations,
including the responsive-window smoke test. Narrator/NVDA, 200% scale,
high-contrast, clean-VM, and signed-package qualification remain manual gates.
