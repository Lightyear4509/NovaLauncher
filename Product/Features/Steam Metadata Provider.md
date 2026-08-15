---
type: feature
status: complete
priority: critical
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Steam Metadata Provider

## Outcome

Metadata Increment 3 adds the first external metadata provider while
preserving the read-only Increment 2 orchestration boundary.

## Eligibility

- Source must be Steam.
- Provider item ID must be a positive 32-bit numeric Steam app ID.
- Provider priority is 100.

## Normalized fields

- Short description
- Detailed plain-text description
- Developers
- Publishers
- Genres
- Released, parseable English release date
- Metacritic score with an explicit 100-point scale
- Retrieval timestamp

## Behavior

- Uses English, US storefront data for deterministic parsing.
- Removes HTML markup and decodes entities.
- Trims and deduplicates list values.
- Omits coming-soon or unparseable dates.
- Omits invalid Metacritic scores.
- Returns no match for missing or empty Steam data.
- Validates the app ID returned by Steam.
- Converts HTTP, connection, and JSON errors into `MetadataException`.
- Preserves caller cancellation.
- Returns an unmerged snapshot without modifying the library.

## Endpoint boundary

`ISteamStoreMetadataClient` isolates Steam's public storefront `appdetails`
endpoint. It does not require an API key, but the endpoint is not part of the
documented Steamworks partner Web API contract and may change independently.

## Not included

- Merge or persistence
- Manual overrides
- Cache or expiration
- Retry policy
- Ratings other than an explicitly supplied Metacritic score
- Achievements
- Metadata UI
- Bulk refresh

## Related

- [[Product/Features/Metadata Provider Contracts|Metadata Provider Contracts]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
