---
type: feature
status: complete
priority: critical
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Merge and Override Policy

## Outcome

Metadata Increment 4 adds deterministic field-level merging, accepted-source
provenance, and non-destructive manual override protection.

## Policy

- Manual values always win.
- Otherwise, the first valid value in ordered snapshot precedence wins.
- Lower-priority snapshots fill fields omitted by higher-priority snapshots.
- Missing or invalid values preserve last-known-good metadata.
- Accepted values record field-level provenance.
- Clearing manual protection retains the current value until a provider
  supplies a valid replacement.

## Managed fields

- Short description
- Description
- Developers
- Publishers
- Genres
- Release date
- Rating

## Implemented

- `MetadataField`
- `MetadataSourceKind`
- `MetadataFieldProvenance`
- Persistent `GameMetadata.FieldProvenance`
- `MetadataMerger`
- `MetadataMergeResult`
- `MetadataOverrideService`
- Adapter and persistence normalization
- Merge, override, serialization, and deep-copy tests
- Dependency-injection registration

## Not included

- Metadata UI
- Automatic retrieve/merge/save workflow
- Cache or expiration
- Retry or rate limiting
- Bulk refresh

## Related

- [[Decisions/ADR-007 Metadata Merge and Override Policy|ADR-007 Metadata Merge and Override Policy]]
- [[Product/Features/Steam Metadata Provider|Steam Metadata Provider]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
