---
type: planning-report
status: complete
created: 2026-07-30
updated: 2026-07-30
---

# Alpha Ecosystem Roadmap Report

## Outcome

The final pre-release direction is organized into four independently testable
pillars: plugin ecosystem, emulator support, seamless cloud saves, and
authorized acquisition/installation.

## Main recommendation

Ship the first alpha after proving the Plugin SDK and lifecycle with a small
set of reference integrations. Do not make every external provider, Tailscale
sync, or a full download manager prerequisites for the first public alpha.

## Key architecture decisions

- Trusted in-process plugins first; out-of-process isolation later.
- Capabilities are disclosed but are not falsely presented as an in-process
  security sandbox.
- Cloud-save state/version/conflict logic is independent of transport.
- Tailscale is an optional peer transport, not the synchronization database.
- Downloads are restricted to official, user-owned, open-source, homebrew,
  mod, and otherwise authorized sources.

## Integration findings

- Strong documented candidates: Discord, IGDB, RetroAchievements,
  PCGamingWiki, SteamGridDB, Nexus Mods, Ludusavi, Playnite.
- Conditional/experimental: ProtonDB.
- Partnership or link-out: HowLongToBeat.

## Primary risks

- Plugin trust and binary compatibility
- Provider secrets, OAuth, terms, and rate limits
- Save conflicts and cross-platform path differences
- Data loss during interrupted upload/restore
- Archive and installer attacks
- Scope expansion delaying alpha feedback

## Recommended first increment

Define the Plugin SDK contract package, manifest, lifecycle, capability model,
compatibility rules, and test harness without loading third-party code yet.

## Related

- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- [[Product/Features/Authorized Acquisition and Installation|Authorized Acquisition and Installation]]
- [[Engineering/Architecture/Integration Feasibility Matrix|Integration Feasibility Matrix]]
