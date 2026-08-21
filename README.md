# NovaLauncher

NovaLauncher is a local-first Windows game launcher for organizing and launching games you already have installed. It brings manually installed games and an existing Steam library into one visual interface while leaving ownership, installation, updates, multiplayer, DRM, and platform services with their original providers.

The launcher includes a Steam-inspired Home page, a card-based Library, individual game-detail pages, cover artwork, playtime tracking, read-only achievements, local backup and restore, themes, and experimental encrypted save synchronization between trusted Windows devices over Tailscale.

NovaLauncher does **not** download games or ROMs, bypass DRM, replace Steam, provide cracked multiplayer compatibility, execute downloaded code, or upload telemetry. Steam games still launch through Steam and continue to use Steam Cloud and Steam's online services.

## Download NovaLauncher

Download the latest build from [GitHub Releases](https://github.com/Lightyear4509/NovaLauncher/releases). For most users, choose:

- `NovaLauncher-Setup-0.5.0-experimental.1-win-x64.exe` for the Windows installer.
- `NovaLauncher-0.5.0-experimental.1-win-x64-portable.zip` if you prefer a portable copy.

This alpha is currently **unsigned**. Windows may display **Unknown publisher** or a Microsoft Defender SmartScreen warning. Download `SHA256SUMS.txt` from the same release and verify the installer before opening it:

```powershell
Get-FileHash .\NovaLauncher-Setup-0.5.0-experimental.1-win-x64.exe -Algorithm SHA256
```

Compare the complete result with the installer entry in `SHA256SUMS.txt`. Do not run the file if the values differ, and do not disable Windows security protections globally.

## Install and start

### Installer

1. Download the setup executable and checksum file from the release page.
2. Verify the checksum as described above.
3. Run the installer. NovaLauncher installs for the current Windows user and does not require administrator access.
4. Open **NovaLauncher** from the Start menu or desktop shortcut.

### Portable version

1. Download and extract the portable ZIP into a new folder.
2. Keep all extracted files together.
3. Run `NovaLauncher.App.exe`.
4. Delete that extracted folder when you no longer want the portable application. This does not delete your installed games.

## Navigate the launcher

The left navigation rail contains four primary pages:

- **Home** shows recently played and highlighted games for quick access.
- **Library** displays all manually added and Steam-imported games as cover cards.
- **Downloads & Saves** shows save-sync activity, conflicts, pending transfers, peer acknowledgements, snapshots, and local backup controls.
- **Settings** contains themes, diagnostics, optional SteamGridDB configuration, and trusted-device pairing.

Select any game card to open its details page. From there you can launch the game, inspect its metadata and playtime, manage its cover, refresh supported achievements, and configure save synchronization for eligible manual games.

## Add a manually installed game

1. Open **Library**.
2. Select **Add installed game**.
3. Use the Windows file picker to locate and select the game's `.exe` file.
4. Review the name and executable information, then save the entry.
5. Select the new card to open its game-details page.

NovaLauncher stores a reference to the selected executable; it does not copy, move, modify, or upload the game. Launches normally use the current user's permissions. If a particular game genuinely requires administrator privileges, enable the explicit elevated-launch option for that game. Windows will still show its User Account Control confirmation—NovaLauncher does not silently bypass UAC.

Measured playtime is recorded while a directly launched local game process is running. Launcher-mediated or unusual child-process behavior can make some titles report playtime differently.

## Import an existing Steam library

1. Make sure Steam is installed and its library folders are accessible.
2. Open **Library** and select **Import Steam games**.
3. Review the dry-run preview. NovaLauncher lists discoverable entries and reports individual items it cannot safely import.
4. Confirm the preview to merge the accepted games into your library.

Steam discovery is read-only. NovaLauncher reads library metadata and manifests needed for discovery, but never reads Steam credentials or modifies Steam files. Imported games retain stable Steam identities and launch through Steam, so Steam remains responsible for licensing, updates, achievements, multiplayer, overlays, and Steam Cloud.

If discovery reports that the Steam root has no `steamapps` directory, confirm that the selected folder is the actual Steam installation root rather than a particular game's folder. Additional Steam library locations must remain connected and readable.

## Use cover artwork and game details

Open a game's details page to view its artwork, description, publisher information, release information, playtime, and supported achievements.

For a manually added game:

1. Use **Match game identity** to review local Steam-manifest and optional
   SteamGridDB candidates. Nothing is linked or downloaded until you confirm a result.
2. Choose cover, hero, logo, or background in the artwork selector.
3. Select **Add artwork** to use your own image, or **Provider variants** to
   review bounded choices for a confirmed identity.
4. Use the crop percentages to create a managed preview, or select **Remove
   artwork** to return that slot to its placeholder without changing the source image.
5. Use **Inspect art cache** and **Clean orphan art** for bounded local cache
   maintenance. Referenced assets and unknown filenames are never removed.

Artwork is displayed with aspect-preserving scaling so covers fit Library and Home cards without stretching. In **Settings**, an optional SteamGridDB API key can enable supported artwork lookup. The key is retained only for the current running session and is not written to the library, logs, or backups.

Metadata and artwork from online services may be incomplete or may not match
similarly named games. Review suggested matches before accepting them. You can
unlink or rematch a provider identity without changing the executable used to
launch the game. A SteamGridDB-only match is not proof of Steam ownership and
does not enable Steam achievements.

## View achievements

Supported first-party achievement information is read-only. Open a game-details page and select **Refresh achievements** to update available completion information. Availability depends on a stable supported game identity and the upstream provider. NovaLauncher does not unlock, modify, or fabricate achievements.

## Synchronize manual-game saves with Tailscale

Save sync is experimental and applies only to manually added games with a save folder you explicitly select. Steam-imported games are excluded and continue to use Steam Cloud.

Before starting, install Tailscale on both Windows devices, sign both into the same tailnet, and confirm that they can reach each other. Do not expose NovaLauncher's TCP port `47471` through a public router.

### Pair two trusted devices

1. Install the same NovaLauncher version on both devices.
2. Open **Settings** on each device and enter the other device's Tailscale IP address.
3. On the first device, select **Generate 24-hour invitation**.
4. Transfer the displayed six-digit code through a trusted channel. You can use **Copy code** on the first device and **Paste code** on the second, or type it manually.
5. On the second device, select **Accept invitation** within 24 hours.
6. Confirm that Settings displays **PAIRED** and the expected pinned device identity. On the invitation-generating device, select **Refresh pairing status** after acceptance.

The invitation is single-use and locks after three failed attempts. The six-digit code is only a short-lived authorization step; it is not the encryption key. NovaLauncher creates a separate random 256-bit secret and stores it in Windows Credential Manager. Previously paired devices reconnect automatically until you select **Revoke paired device**.

### Map and link a game's save folder

1. Add the same manual game on both devices.
2. Open the game's details page on each device.
3. Use the same sync label and platform on both entries.
4. Select **Choose save folder** and choose the exact folder where that game stores its saves.
5. Select **Link automatically with paired device**.
6. If an existing folder contains saves, review the prompt and choose **Upload existing saves** only after verifying the game and folder. You can choose **Not now** and use **Sync now** later.

Before launching a mapped game, NovaLauncher checks for a newer save generation from the paired device. After the launched game process exits—even when you close it from inside the game—NovaLauncher waits for stable file scans and sends changed files. The other device can restore those files before its next launch. **Downloads & Saves** displays transfer progress, queued retries, snapshot state, and peer acknowledgement.

If both devices changed the same baseline, NovaLauncher blocks automatic replacement and presents explicit choices:

- **Keep local** retains the saves on the current device.
- **Use remote** restores the peer's version after creating a managed backup.
- **Keep both** preserves both versions for manual inspection.

Keep independent backups of valuable saves. The alpha has safeguards for authenticated encryption, replay rejection, immutable manifests, atomic replacement, backup-before-restore, cancellation, offline retry, and conflicts, but it has not been qualified across every game, firewall, interruption, disk-full, sleep, or power-loss scenario.

## Back up or restore NovaLauncher data

Open **Downloads & Saves**, choose a destination archive path, and select **Export local backup**. Use **Restore local NovaLauncher backup** to validate and restore a compatible archive.

This backup protects NovaLauncher's library and supported local application data; it is not a substitute for backing up installed game folders or every game's save data. Restoring a backup never installs a game.

## Customize appearance and inspect diagnostics

Open **Settings** to choose one of the included themes. Buttons retain visible themed backgrounds and hover feedback across supported themes. Diagnostics and structured logs are stored locally and are designed to exclude API keys, pairing secrets, and raw configured account identifiers.

When reporting a problem, include the NovaLauncher version, the action that failed, and a sanitized diagnostic message. Never post pairing codes, Tailnet IP addresses, API keys, save contents, or other private data in a public GitHub issue.

## Troubleshooting

### The application process starts but no window appears

Install the newest alpha build or try the portable package in a clean folder. If the problem continues, collect the local diagnostic message and open a GitHub issue with your Windows version and display configuration.

### A game requires elevated permissions

Open that game's details and enable its explicit elevated-launch option only if the game is trusted and actually requires it. Approve the Windows UAC prompt when launching. Avoid running NovaLauncher itself permanently as administrator.

### Steam import finds no games

Confirm Steam is installed, the detected root contains `steamapps`, and external library drives are connected. Steam manifests must be readable. NovaLauncher will not modify a damaged manifest to make discovery succeed.

### Pairing is network-ready but does not connect

Confirm both devices are online in the same tailnet, each is configured with the other device's Tailscale IP, and Windows Firewall permits NovaLauncher on the Tailscale network. Generate a fresh invitation if the previous code expired or was consumed. Do not forward port `47471` on an internet-facing router.

### Save sync reports no new snapshot

Verify that the game is manual rather than Steam-imported, both entries use the same sync label and platform, both devices are paired, and the exact save folders are mapped. On the device that already has saves, choose **Sync now** or **Upload existing saves**, then check **Downloads & Saves** for a peer acknowledgement or queued retry.

## Privacy and security boundaries

NovaLauncher is local-first:

- No telemetry is collected.
- No hosted NovaLauncher cloud service receives your library or saves.
- Steam credentials are never read.
- Steam files are never modified.
- Pairing secrets are kept out of JSON, logs, exports, and release artifacts.
- Online metadata and artwork requests occur only for configured supported providers.
- Save synchronization is limited to folders you explicitly map and peers you explicitly pair.
- Plugins, scripting, marketplaces, ROM acquisition, and downloaded-code execution are not supported.

## Current release status

`v0.5.0-experimental.1` adds explicit peer transfer of user-authorized manual DRM-free game folders. It rejects Steam/store-managed roots, uses authenticated bounded chunks, resumes verified staging, checks every SHA-256 hash, invokes Windows Security where available, and never launches received executables. This unsigned experimental prerelease is not production- or legal/security-qualified; physical multi-device interruption, disk exhaustion, firewall, accessibility, and independent review remain open.

Please use test data or maintain independent backups while evaluating experimental save synchronization.

## Uninstall

Remove the installed application through **Windows Settings → Apps → Installed apps**. Uninstalling removes application files but intentionally does not delete installed games or NovaLauncher's separate user-library data. For the portable build, close NovaLauncher and delete the extracted application folder.

## Contributing and development

NovaLauncher is licensed under the [MIT License](LICENSE). Development documentation, architecture decisions, safety requirements, tests, and release gates remain available in this repository for contributors:

- [Google AI Studio Build Guide](AI/Google%20AI%20Studio%20Build%20Guide.md)
- [Alpha Release Specification](Product/Requirements/Alpha%20Release%20Specification.md)
- [Technical Architecture Specification](Engineering/Architecture/Technical%20Architecture%20Specification.md)
- [Safety and Test Plan](Engineering/Quality/Safety%20and%20Test%20Plan.md)
- [Release Readiness Checklist](Planning/Releases/Release%20Readiness%20Checklist.md)

The application uses pinned .NET and package versions. Changes should preserve its safety boundaries, pass formatting and analyzers, build in Debug and Release, and pass the complete automated test suite without weakening safeguards.
