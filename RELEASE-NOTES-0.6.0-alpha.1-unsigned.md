# NovaLauncher 0.6.0 Alpha 1 — unsigned lifecycle preview

This is an explicitly unsigned Windows prerelease for evaluation. Windows cannot authenticate its publisher. Download it only from the official `Lightyear4509/NovaLauncher` repository and verify the complete SHA-256 value against the attached `SHA256SUMS.txt` before opening it.

## Phase 6 additions

- Fixed-origin GitHub update discovery with Stable, Beta, and Alpha channel filtering.
- Bounded release metadata and release notes.
- Streaming download, SHA-256 verification, official asset-path enforcement, Authenticode verification, and immutable publisher-certificate pinning for future signed builds.
- Separate stage and install confirmation with complete re-verification immediately before installer execution.
- Interrupted-session recovery status and sanitized diagnostic ZIP export.
- Atomic settings schema migration with a rollback backup.
- Versioned signed-installer rollback recovery for future signed releases.
- Public privacy, security, and support policies.

## Unsigned-preview limitations

- `NovaLauncher.App.exe` and the installer intentionally have Authenticode status `NotSigned`.
- No publisher-certificate pin is embedded.
- Automatic update download/installation fails closed and is unavailable.
- Signed installer rollback is unavailable.
- Windows may display **Unknown publisher** or a Microsoft Defender SmartScreen warning.
- Signed install, upgrade, repair, downgrade, rollback, uninstall, Windows-version, and antivirus qualification remain open.

No signature check, publisher pin, test, or updater safeguard was weakened to create this preview. The signed production builder and workflow continue to require a trusted certificate.

## Install safely

1. Download the installer or portable ZIP and `SHA256SUMS.txt` from this release.
2. Run `Get-FileHash .\<downloaded-file> -Algorithm SHA256` in PowerShell.
3. Compare the entire result with the matching line in `SHA256SUMS.txt`.
4. Do not run the download if the values differ.
5. Keep independent backups of valuable launcher data and game saves.

The preview retains the existing Home, Library, Steam import, manual-game metadata and artwork, playtime, read-only achievements, backup and restore, multi-peer Tailscale save synchronization, and authorized manual DRM-free peer-transfer features.
