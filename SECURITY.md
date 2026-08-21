# Security Policy

Do not open a public issue for a suspected vulnerability. Use the repository's
**Security → Report a vulnerability** private-reporting flow when it is
available. If GitHub does not show that option, do not post exploit details;
open a minimal issue asking the maintainer to enable a private channel. Never
send credentials, pairing secrets, API keys, game saves, or copyrighted game
content.

NovaLauncher never needs Steam credentials and must never modify Steam files.
The current build has no telemetry, plugin execution, hosted NovaLauncher
cloud, or ROM acquisition. The Phase 6 updater is a narrow exception to the
downloaded-code prohibition: it checks only this official repository after an
explicit user action and refuses to stage an installer unless its size,
SHA-256, Windows Authenticode chain, and embedded publisher-certificate pin all
match. It never runs an installer automatically.

Supported security fixes are published through GitHub releases. Unsigned
preview releases are not eligible for in-app update staging.
