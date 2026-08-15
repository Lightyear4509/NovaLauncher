---
type: decision
status: accepted
created: 2026-07-29
updated: 2026-07-29
---

# ADR-009 Metadata Cache Freshness and Stale Fallback

## Context

Metadata refresh should avoid unnecessary provider traffic without allowing
old cache values to silently replace live results. The cache must remain
provider-neutral, preserve manual overrides, and fit the existing atomic
refresh coordinator.

## Decision

- Cache only successful normalized snapshots.
- Keep the first implementation in memory.
- Key entries by normalized source, platform, and stable request identity.
- Treat entries as fresh for 24 hours by default.
- Retain stale entries for 7 days by default.
- Skip providers for a fresh entry.
- Query providers when an entry is stale.
- Use stale snapshots only when live retrieval has no successful snapshots.
- Let explicit refresh bypass cache reads and stale fallback.
- Replace cache data after successful live retrieval.
- Never cache empty or failed retrieval.
- Deep-copy snapshots on store and lookup.
- Own lazy interval cleanup inside `MetadataCache`.
- Enforce a least-recently-used maximum-entry bound.

## Consequences

- Routine refreshes avoid repeated provider traffic.
- Stale data is an availability fallback rather than live-data precedence.
- Manual provenance still wins during merge.
- The cache is lost at application exit.
- Provider registration changes can take effect after freshness expires or an
  explicit bypass.
- Disk persistence, background timers, retries, rate limiting, UI, and bulk
  refresh remain separate work.

## Related

- [[Product/Features/Metadata Cache Policy|Metadata Cache Policy]]
- [[Decisions/ADR-008 Atomic Metadata Refresh Coordination|ADR-008 Atomic Metadata Refresh Coordination]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
