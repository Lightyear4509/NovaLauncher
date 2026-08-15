---
type: feature
status: complete
priority: high
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Cache Policy

## Outcome

Metadata Increment 6 adds bounded provider-neutral in-memory caching, explicit
freshness, stale fallback, forced bypass, and lazy cleanup to single-game
metadata refresh.

## Implemented

- `MetadataOptions`
- `MetadataCacheKey`
- `MetadataCache`
- `MetadataCacheLookup`
- `MetadataCacheCleanupResult`
- `MetadataRefreshSource`
- Startup configuration validation
- Fresh-cache provider bypass
- Live retrieval for stale entries
- Stale fallback only after no successful live snapshots
- Explicit cache bypass with successful live replacement
- Deep-copy isolation on store and lookup
- Expiration and least-recently-used capacity cleanup
- Cache progress stages and refresh-source reporting
- Focused cache and coordinator integration tests

## Defaults

- 24-hour freshness
- 7-day stale retention
- 6-hour lazy cleanup interval
- 1,000 entries maximum

## Not included

- Persistent disk or database cache
- Background cleanup timer
- Metadata refresh UI
- Provider retries or rate limiting
- Bulk refresh

## Related

- [[Decisions/ADR-009 Metadata Cache Freshness and Stale Fallback|ADR-009 Metadata Cache Freshness and Stale Fallback]]
- [[Product/Features/Metadata Refresh Coordination|Metadata Refresh Coordination]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
