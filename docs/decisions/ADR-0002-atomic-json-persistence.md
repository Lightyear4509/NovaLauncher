# ADR-0002: Atomic versioned JSON persistence

Status: Accepted for Increment 1

Date: 2026-08-13

## Context

NovaLauncher needs offline-first library persistence that survives interrupted
writes, corruption, cancellation, and concurrent processes. The alpha canonical
documents are `games.json`, `collections.json`, and `settings.json`; SQLite is a
future migration candidate.

## Decision

Each document carries an integer schema version and has an independent store.
Writes occur beside the destination, are durably flushed, read back, domain-
validated, and then atomically moved or replaced. A valid prior primary becomes
`.bak`. Invalid primaries are copied to `.invalid-<UTC timestamp>` before later
replacement. Newer schema versions are never overwritten.

Each store uses an exclusive lock file opened with `FileShare.None`; lock waits
are asynchronous and cancellable. Every store first acquires the launcher-wide
backup barrier, then its document lock, so multi-document export/restore cannot
interleave with ordinary reads or writes across processes. Live application state
must only be published after a `Saved` result. Load returns explicit not-found,
loaded, legacy-migrated, backup-recovered, newer-schema, or unrecoverable status.

Export archives allow only the three canonical documents and validate content
before writing. Restore rejects unknown, nested, duplicate, oversized, or invalid
entries; validates all input into staging; creates a pre-restore archive; and
rolls back already-replaced documents if coordinated commit fails.

## Consequences

- Secrets cannot be fields in persisted launcher settings.
- Schema changes require migration code and tests before increasing a version.
- A valid backup may intentionally lag one successful save.
- The pre-restore archive is retained when rollback is needed for manual recovery.
- Multi-document restore is isolated by one launcher-wide lock. Other code must
  use these services rather than writing canonical files directly.
- Source-generated JSON serializer members are excluded from coverage because
  they are framework-generated; document round trips test their contract.
