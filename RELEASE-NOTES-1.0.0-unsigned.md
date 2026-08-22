# NovaLauncher 1.0.0 — unsigned preview

This is an explicitly unsigned Windows prerelease for evaluation. Windows cannot authenticate its publisher. Download it only from the official `Lightyear4509/NovaLauncher` repository and verify the complete SHA-256 value against the attached `SHA256SUMS.txt` before opening it.

## Highlights

- A distinct, responsive, artwork-first Home, Library, game-details, Downloads & Saves, and Settings experience with accessible navigation, consistent themed controls, reduced-motion support, and an optional controller-oriented navigation mode.
- Manual game discovery, defensive read-only Steam import, collections, favorites, card/list views, cover and hero artwork, local metadata, playtime tracking, additional validated launch actions, and read-only supported achievements.
- Customizable Home sections and locally persisted layout preferences.
- Experimental encrypted save synchronization between explicitly trusted Windows devices over Tailscale, including per-game multi-peer destinations, conflict comparison, retry state, resumable authenticated chunks, transfer progress, integrity verification, snapshot history, and two-phase peer-credential rotation.
- Authorized manual-game folder transfer with dry-run review, a chosen active recipient, expiring offers, resumable chunks, bounded manifests, exact file verification, and atomic promotion.
- Large archive files are supported within the same hard 700 GiB aggregate package ceiling; the 50,000-file maximum remains enforced.

## Safety boundaries

- Folder transfer remains opt-in, requires explicit copy-rights confirmation and a trusted recipient, and rejects Steam or other store-managed games. It never bypasses DRM or proves ownership.
- Transfer manifests remain bounded to 50,000 files and 700 GiB total. Links, reparse points, unsafe paths, unexpected files, changed source files, and oversized packages fail closed.
- NovaLauncher does not install, elevate, or automatically launch a received game.
- Steam access remains read-only. NovaLauncher never reads Steam credentials or modifies Steam files.
- No telemetry, plugins, scripting, marketplace, ROM acquisition, cloud-hosted NovaLauncher service, or general downloaded-code execution is included.

## Unsigned-release limitations

- The application and installer intentionally have Authenticode status `NotSigned`.
- No trusted publisher-certificate pin is embedded, so automatic update installation and signed rollback fail closed and remain unavailable.
- Windows may display **Unknown publisher** or a Microsoft Defender SmartScreen warning.
- Signed install, upgrade, repair, downgrade, rollback, and uninstall qualification remains open.
- Physical multi-device and very-large transfer qualification, interruption and power-loss qualification, Narrator/NVDA and 200% display-scale review, antivirus review, clean-VM review, and independent security/legal review remain open.

No signature check, publisher pin, test, ownership safeguard, or safety limit was weakened for this release. Keep independent backups of valuable launcher data, games, and saves.

## Install safely

1. Download the installer or portable ZIP and `SHA256SUMS.txt` from this release.
2. Run `Get-FileHash .\<downloaded-file> -Algorithm SHA256` in PowerShell.
3. Compare the entire result with the matching entry in `SHA256SUMS.txt`.
4. Do not run the file if the values differ.
