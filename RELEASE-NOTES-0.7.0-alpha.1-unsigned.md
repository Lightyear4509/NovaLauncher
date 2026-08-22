# NovaLauncher 0.7.0 Alpha 1 — unsigned identity preview

This is an explicitly unsigned Windows prerelease for evaluation. Windows cannot authenticate its publisher. Download it only from the official `Lightyear4509/NovaLauncher` repository and verify the complete SHA-256 value against the attached `SHA256SUMS.txt` before opening it.

## Highlights

- A componentized NovaLauncher interface with a distinct responsive shell and dedicated Home, Library, game-details, Downloads & Saves, and Settings views.
- Refined library cards, artwork presentation, navigation states, typography, spacing, themed controls, and restrained hover and selection feedback.
- Bounded artwork rendering that preserves aspect ratio rather than stretching covers or hero images.
- Existing launch, Steam import, artwork, achievements, save-sync, peer-transfer, backup, settings, update, and diagnostics actions retained in the redesigned views.
- Manual DRM-free game transfer now scans the selected game folder and its subfolders automatically, clearly reports readiness, and supports valid packages through 500 GiB with a hard 700 GiB aggregate maximum.

## Safety boundaries

- Folder transfer remains opt-in, requires explicit copy-rights confirmation and a trusted recipient, and rejects Steam/store-managed games.
- Transfer manifests remain bounded to 50,000 files, 16 GiB per file, and 700 GiB total. Links, reparse points, unsafe paths, unexpected files, and changed source files fail closed.
- NovaLauncher does not install, elevate, or automatically launch a received game.
- Steam access remains read-only. NovaLauncher never reads Steam credentials or modifies Steam files.
- No telemetry, plugins, scripting, marketplace, ROM acquisition, cloud-hosted NovaLauncher service, or general downloaded-code execution is included.

## Unsigned-preview limitations

- The application and installer intentionally have Authenticode status `NotSigned`.
- No trusted publisher-certificate pin is embedded, so automatic update installation and signed rollback fail closed and remain unavailable.
- Windows may display **Unknown publisher** or a Microsoft Defender SmartScreen warning.
- Signed install/upgrade/repair/downgrade/rollback/uninstall qualification, physical multi-device transfer qualification, assistive-technology review, 200% display-scale review, antivirus review, and independent security/legal review remain open.

No signature check, publisher pin, test, or safety limit was weakened for this release. Keep independent backups of valuable launcher data, games, and saves.

## Install safely

1. Download the installer or portable ZIP and `SHA256SUMS.txt` from this release.
2. Run `Get-FileHash .\<downloaded-file> -Algorithm SHA256` in PowerShell.
3. Compare the entire result with the matching entry in `SHA256SUMS.txt`.
4. Do not run the file if the values differ.

