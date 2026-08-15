# ADR-0006: Bounded first-party metadata and artwork enrichment

- Status: Accepted
- Date: 2026-08-13
- Increment: 4

## Decision

Metadata and artwork integrations are first-party typed providers ordered by
priority and stable ID. External payloads are bounded and normalized before
domain merge. Manual provenance has absolute precedence; otherwise the first
valid ordered value wins and missing data retains the last-known-good value.

Fresh cache entries bypass providers. Stale entries are fallback-only after live
failure. Forced refresh bypasses cache reads and stale fallback. A staged
enrichment reaches live library state only after one successful atomic save.

Steam detailed metadata is isolated behind the public storefront `appdetails`
client specified by the canonical alpha plan. Because this response is not a
formally documented Steamworks API method, it is treated as replaceable and a
compatibility risk. No account identity, credential, or authentication data is
used.

Steam CDN supplies deterministic no-key artwork references. SteamGridDB is
queried only when an environment key is present and is never logged or persisted.
Only bounded HTTPS or internal placeholder references may enter persistence.

## Consequences

- Provider failure cannot corrupt or partially update the library.
- Normal automated tests require no live network or credentials.
- Plugins and provider-delivered code remain prohibited.
- Remote image byte materialization, bounded decoding, and visual rendering
  remain a required follow-up before artwork can be considered release-ready.
