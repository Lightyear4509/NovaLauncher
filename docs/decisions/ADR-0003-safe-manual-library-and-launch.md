# ADR-0003: Safe manual-library mutations and launching

- Status: Accepted
- Date: 2026-08-13
- Increment: 2

## Context

Increment 2 needs useful local library management without importing storefront
data or permitting arbitrary command execution. UI state must never claim a
mutation succeeded before durable persistence succeeds.

## Decision

Manual drafts are validated in the application layer. Library and collection
coordinators serialize mutations, save a complete staged document, and publish
the replacement only after a typed successful save.

Game launch is isolated behind `IGameLauncher`. An executable target must be an
absolute existing `.exe`; its optional working directory must exist; each
argument is passed as a separate `ArgumentList` entry with shell execution off.
URI launching is limited to exact, case-insensitive `steam`, `goggalaxy`, and
`com.epicgames.launcher` schemes. Validation and process failures become typed
results. Caller cancellation propagates.

Destructive-looking UI actions use explicit two-step confirmation. Removing a
game removes only its NovaLauncher record. Restore requires a valid archive
preview and a second confirmation; the storage layer creates a pre-restore
backup and owns rollback.

## Consequences

- Persistence failure leaves the published in-memory state unchanged.
- Arguments containing spaces are preserved without shell parsing.
- Arbitrary web, file, script, and unknown URI schemes cannot be launched.
- The launch adapter has an injectable process-start boundary for deterministic
  failure testing without executing malformed binaries.
- Storefront discovery, downloaded code, ROM acquisition, cloud saves, and
  telemetry remain outside this increment.
