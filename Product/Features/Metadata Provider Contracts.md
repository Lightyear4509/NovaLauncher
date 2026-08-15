---
type: feature
status: complete
priority: critical
related_epic: "[[Product/Epics/Metadata|Metadata]]"
created: 2026-07-29
updated: 2026-07-29
---

# Metadata Provider Contracts

## Outcome

Metadata Increment 2 provides deterministic, cancellation-aware, read-only
provider orchestration without enabling an external metadata source.

## Implemented

- Immutable `MetadataRequest`
- Normalized, unmerged `MetadataSnapshot`
- Success, no-match, and failed `MetadataProviderResult`
- Contextual `MetadataException`
- Structured `MetadataProgress` stages
- Cancellation-aware `IMetadataProvider`
- Deterministic `MetadataProviderManager`
- Read-only `MetadataService`
- Ordered `MetadataRetrievalResult`
- Structured provider-failure logging
- Dependency-injection registration with an empty provider set
- Fake-provider tests

## Behavior

- Higher provider priority runs first.
- Equal priorities are ordered deterministically.
- Ineligible providers are skipped.
- Duplicate concrete provider types are ignored.
- One provider failure does not stop lower-priority providers.
- Caller cancellation propagates immediately.
- Provider attribution is validated.
- Retrieval does not modify or persist library metadata.

## Not included

- Steam or other external providers
- Retries
- Snapshot merging
- Manual overrides
- Metadata persistence after retrieval
- Cache or expiration
- Metadata UI
- Bulk refresh

## Related

- [[Product/Features/Metadata Pipeline Foundation|Metadata Pipeline Foundation]]
- [[Engineering/Services/Metadata Service|Metadata Service]]
- [[Decisions/ADR-001 Provider Architecture|ADR-001 Provider Architecture]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
