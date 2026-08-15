---
type: service
status: planned
priority: high
related_epic: "[[Product/Epics/Emulator Support|Emulator Support]]"
created: 2026-07-29
updated: 2026-07-30
---

# Emulator Service

## Responsibility

Coordinate emulator detection, configuration, ROM launch behavior, and emulator-specific integrations.

## Foundation notes

- Emulator implementations should remain replaceable.
- Scanning and metadata retrieval belong behind service/provider boundaries.
- Launch configuration must remain testable without starting an emulator.
- Scans require a dry-run preview, stable hashes, duplicate detection, and
  reversible imports.
- Emulator profiles should become plugin extension points after the core
  contract is stable.
- NovaLauncher does not distribute ROMs, BIOS files, keys, or firmware.

## Related

- [[Product/Epics/Emulator Support|Emulator Support epic]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
