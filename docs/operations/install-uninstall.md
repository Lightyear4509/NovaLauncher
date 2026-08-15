# Install, inspect, and uninstall the unsigned alpha preview

1. Download the installer and `SHA256SUMS.txt` from the same trusted GitHub
   release, then run `Get-FileHash .\NovaLauncher-Setup-0.1.0-alpha.1-win-x64.exe -Algorithm SHA256`.
2. Compare every hexadecimal character with the published value. A mismatch
   means do not run the file.
3. Run the installer as a standard user. It targets the current user's Local
   AppData Programs folder and does not require administrator access.
4. Windows may say **Unknown publisher**, show SmartScreen, or block the file.
   That is what “unsigned” means: the file has no code-signing certificate that
   cryptographically identifies a publisher. A checksum detects alteration only
   when the checksum itself came from a trusted location; it does not establish
   publisher identity. Never turn off SmartScreen or Smart App Control globally.
5. Uninstall from Windows Installed apps. The installer removes application
   files but does not target NovaLauncher's separate user-library data directory.

The portable ZIP is the fallback for inspection. Extract it to a new folder and
run `NovaLauncher.App.exe`. Delete that extracted folder to remove the portable
copy. NovaLauncher does not delete installed games.

This preview has local library management, defensive Steam discovery/import,
metadata/artwork, read-only achievements, themes, and local backup/restore. It
does not have cloud save synchronization or device linking.
