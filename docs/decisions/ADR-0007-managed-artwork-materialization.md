# ADR-0007: Validate remote artwork before managed local rendering

- Status: accepted
- Date: 2026-08-13

## Decision

Remote artwork is fetched only through the bounded HTTPS client and is never
rendered or persisted as a provider URL. NovaLauncher accepts single-frame PNG,
JPEG, and WebP only when the declared content type matches the detected format,
the encoded body is at most 8 MiB, each dimension is at most 8,192 pixels, the
decoded image is at most 16 million pixels, and a complete pixel decode succeeds.

Accepted bytes are written durably under a generated game/kind/content-hash name
inside the per-user artwork cache. Library documents contain an opaque
`managed-artwork` URI. Resolution rejects hosts, traversal, nested names, and
paths outside the configured cache root. New files are removed when persistence
fails; obsolete provider-owned files are removed after a successful replacement.
Manual artwork is never removed by enrichment cleanup.

## Consequences

Artwork stays usable offline and remote filenames cannot control local paths.
The explicit decode adds bounded CPU and memory cost during user-initiated
refresh. Animated artwork and formats outside PNG/JPEG/WebP are intentionally
unsupported in the alpha.
