# Post-alpha save synchronization roadmap

Cloud save synchronization is explicitly deferred and must not be represented
as implemented by the alpha preview.

1. Build a local snapshot engine with user-approved save folders, bounded file
   traversal, hashes, atomic manifests, dry-run preview, backup, and restore.
2. Add device identity, version history, offline queues, and conflict detection.
   Never silently overwrite divergent saves; keep both copies and offer recovery.
3. Define a provider-neutral, end-to-end encrypted transport contract. Encrypt
   file contents and metadata before transport and keep recovery keys outside
   the save payload.
4. Add a first-party Tailscale/private-VPN transport adapter. The VPN supplies
   reachability, not application authorization: NovaLauncher still needs mutual
   device approval, least-privilege endpoints, replay protection, rate limits,
   and revocation. It must not store a Tailscale admin/API token.
5. Validate interruption, rollback, ransomware-style mass changes, disk-full,
   clock skew, rename/delete conflicts, large saves, and multi-device races.

Other VPN services may implement the same transport boundary later without
plugins or downloaded code. A hosted relay, accounts, billing, telemetry, or
central storage requires a separate privacy/security specification and consent.
