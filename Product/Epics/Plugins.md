---
type: epic
status: removed
priority: none
release: alpha
progress: 0
aliases:
  - Plugins SDK
  - Epic - Plugins
created: 2026-07-29
updated: 2026-08-13
---

# Epic - Plugins

> Removed from the product plan. See
> [[docs/decisions/ADR-0004-remove-plugins-add-first-party-achievements|ADR-0004]].
> Remaining content is historical only.

## Features

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- Plugin Loader
- Plugin API
- Curated Catalog
- Plugin Updates and Rollback
- Plugin Capabilities and Permissions
- Plugin Documentation
- Developer SDK and Templates
- Compatibility Test Harness
- Failure Quarantine
- Out-of-Process Plugin Host

## Progress

- [x] SDK contract assembly and semantic-version rules
- [x] Manifest and compatibility validation
- [x] Read-only package safety validation
- [x] Contract test harness
- [x] Plugin inventory and lifecycle state
- [x] Staged installation, hash verification, quarantine, and rollback
- [x] Health tracking and conservative uninstall
- [x] Isolated managed loading foundation
- [x] Developer template and compile-tested sample
- [x] Deterministic package builder and local CLI
- [x] Typed operation outcomes and compatibility policy
- [x] Signed catalog and publisher-verification foundation
- [x] GitHub Release source, staged download, and exact consent
- [x] Disabled catalog update and rollback coordination
- [x] Non-executing host protocol, supervisor, health, deadlines, and safe
  staging cleanup
- [x] Verified managed loading and lifecycle quarantine integration
- [x] In-launcher catalog, exact consent, lifecycle, recovery, and runtime UI
- [ ] Brokered provider operations and enforceable resource boundaries
- [x] Deny-by-default broker policy, exact consent, quotas, and redacted audit
- [ ] Windows restricted-identity prototype and adversarial denial harness
- [ ] First curated reference plugins

## Reference plugins

- Discord Rich Presence
- PCGamingWiki
- Playnite Import
- SteamGridDB adapter

## Related

- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Engineering/Services/Plugin Service|Plugin Service]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[AI/Plugin SDK Increment 2 Report|Plugin SDK Increment 2 Report]]
- [[AI/Plugin SDK Increment 3 Report|Plugin SDK Increment 3 Report]]
- [[AI/Plugin SDK Increment 4 Report|Plugin SDK Increment 4 Report]]
- [[AI/Plugin SDK Increment 5 Report|Plugin SDK Increment 5 Report]]
- [[AI/Plugin SDK Increment 6 Report|Plugin SDK Increment 6 Report]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/Plugin SDK Increment 8 Report|Plugin SDK Increment 8 Report]]
