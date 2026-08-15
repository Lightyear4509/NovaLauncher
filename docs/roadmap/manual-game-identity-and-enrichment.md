# Manual-game identity and enrichment

Name-based enrichment is feasible but must use confirmation rather than silently
binding the first fuzzy result.

## Proposed flow

1. After a user selects an executable, derive a cleaned display-name suggestion.
2. Search read-only local Steam manifests first for exact/normalized title matches.
3. If the user supplied a SteamGridDB key, query its documented game-search API
   with bounded input, response size, result count, timeout, and cancellation.
4. Show candidate cards with provider, title, year, and provider IDs. Do not
   persist or download artwork until the user confirms one candidate or chooses
   “No match.”
5. Store a first-party linked identity separately from the launch source. A
   manually selected executable remains the launch target even if it is linked
   to a Steam/SteamGridDB record.
6. Use the confirmed identity for bounded metadata and artwork requests. Preserve
   manual overrides and provenance; support unlink and rematch.
7. Permit read-only Steam achievements only when a confirmed Steam App ID,
   Steam Web API key, and Steam account ID exist and the API exposes the data.

SteamGridDB supplies artwork identity and assets, not authoritative Steam
achievement state. Achievements therefore require a distinct confirmed Steam
identity. No unlock is inferred from local files or executable names.

## Required safeguards and tests

- Unicode/spacing/punctuation normalization and ambiguous-title fixtures.
- No automatic first-result selection; explicit confirmation and undo.
- Adult/epilepsy/humor filtering according to provider capabilities.
- Bounded images, managed cache, offline/stale behavior, cancellation, and
  malicious JSON/URL tests.
- Identity collision, rematch, unlink, manual-override, and backup/restore tests.
- No credentials in logs, library documents, backups, crash reports, or UI text.
