---
type: feature
status: planned
priority: medium
related_epic: "[[Product/Epics/Library|Library]]"
release: post-alpha
created: 2026-07-30
updated: 2026-07-30
progress: 0
---

# Authorized Acquisition and Installation

## Goal

Download, verify, install, and onboard games or mods from sources that grant
the user and NovaLauncher permission to do so.

## Supported sources

- Official stores and libraries
- User-owned installer files
- Authorized direct downloads
- Open-source, freeware, and homebrew catalogs
- Nexus Mods and other authorized mod services
- Existing local installations

## Excluded sources

NovaLauncher will not integrate piracy, repack, cracked-game, DRM-bypass,
credential theft, or copyright-circumvention services.

## Pipeline

1. Provider returns an acquisition descriptor and provenance.
2. User reviews source, permissions, size, destination, and license.
3. Download manager stages the content with progress and cancellation.
4. Hash/signature and archive safety checks run.
5. User confirms any executable installer or elevation request.
6. Installation is tracked and recoverable.
7. NovaLauncher imports the executable, metadata, artwork, and save mapping.

## Security requirements

- Resumable downloads
- Size and disk-space limits
- Hash/signature validation
- Quarantine and malware-scanner hook
- Archive traversal and decompression-bomb defense
- Symlink/reparse-point defense
- Explicit executable/elevation consent
- Cleanup and rollback
- Provider/license/receipt provenance

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Decisions/ADR-017 Authorized Content Acquisition Boundary|ADR-017 Authorized Content Acquisition Boundary]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
