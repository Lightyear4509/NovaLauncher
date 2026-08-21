# Privacy Policy

NovaLauncher is local-first and has no telemetry or advertising identifier.
Library records, settings, logs, artwork cache, backups, crash markers, and
transfer history remain on the user's device unless the user explicitly starts
a supported export or peer operation.

Network activity is limited to user-requested provider refreshes,
user-configured Tailscale peer operations, and user-requested checks of the
official `Lightyear4509/NovaLauncher` GitHub releases. NovaLauncher does not
request or store Steam passwords. Session API keys and pairing credentials are
excluded from diagnostic exports.

The sanitized diagnostic export contains product/runtime/OS versions and a
redacted copy of the bounded local structured log. It excludes settings,
library documents, save contents, credentials, API keys, and raw device IDs.
Users must review the ZIP before sharing it.

Uninstalling NovaLauncher removes installed application files but preserves
separate user data by default. Users may delete `%LOCALAPPDATA%\NovaLauncher`
after making any desired backup.
