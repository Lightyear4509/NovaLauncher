---
type: roadmap
status: active
version: "0.2"
created: 2026-07-29
updated: 2026-07-30
---

# NovaLauncher Master Roadmap

# Vision

Build the best open-source game launcher.

One launcher.

Every game.

Your way.

---

# Release Timeline

## Alpha

Core launcher

### Implemented foundations

- [x] Provider manager with deterministic priority ordering
- [x] [[Product/Features/SteamGridDB Artwork Provider|SteamGridDB artwork provider]]
- [x] Steam CDN artwork fallback
- [x] Artwork hardening Increment 1 foundation
- [x] Artwork hardening Increment 2 provider/download resilience
- [x] Artwork hardening Increment 3 cache lifecycle
- [x] Artwork hardening Increment 4 placeholders
- [x] Artwork hardening Increment 5 progress reporting
- [x] Artwork hardening Increment 6 UI cancellation
- [x] Metadata Pipeline Increment 1 domain and persistence foundation
- [x] Metadata Pipeline Increment 2 provider contracts and orchestration
- [x] Metadata Pipeline Increment 3 Steam metadata provider
- [x] Metadata Pipeline Increment 4 merge, provenance, and manual overrides
- [x] Metadata Pipeline Increment 5 atomic refresh coordination
- [x] Metadata Pipeline Increment 6 cache freshness and stale fallback
- [x] Game Details Increment 1 metadata state foundation
- [x] Game Details Increment 2 metadata presentation and refresh controls
- [x] Game Details Increment 3 theme and responsive polish
- [x] Game Details Increment 4 manual editing and provenance
- [x] Search Increment 1 shared query service
- [x] Search Increment 2 result-state UX
- [x] Collections Increment 1 persistence
- [x] Collections Increment 2 coordination
- [x] Collections Increment 3 page integration
- [x] Themes Increment 1 startup and persistence
- [x] Themes Increment 2 UI and resource validation

### Next artwork checkpoint

- [ ] Live artwork-hardening smoke test

### Next metadata checkpoint

- [ ] Decide whether to prioritize persistent metadata cache or a second
  metadata provider

### Alpha integration candidates

- [ ] UI Design and Enhancement: independently branded Home, virtualized
  Library, and Saves navigation shell with honest unavailable states
- [ ] [[Product/Features/Achievements|First-party achievements]]
- [ ] Steam achievement provider and offline cache
- [ ] RetroAchievements feasibility checkpoint after emulator identity exists
- [ ] First-party SteamGridDB adapter
- [ ] [[Product/Epics/Emulator Support|Emulator profile and ROM-import MVP]]
- [ ] Experimental local cloud-save snapshot engine
- [ ] User-owned installer/import boundary
- [ ] Alpha backup/export and release checklist

Detailed dependency order, increments, security gates, and post-alpha work are
defined in [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]].

## Beta

Cloud transport, broader integrations, and power-user features:

- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- Tailscale transport
- First-party authorized integrations evaluated individually
- [[Product/Features/Authorized Acquisition and Installation|Authorized Acquisition and Installation]]

## Version 1.0

Feature complete

## Version 2.0

Broader first-party integrations

---

# Epics

- [[Product/Epics/Foundation|Epic - Foundation]]
- [[Product/Epics/Library|Epic - Library]]
- [[Product/Epics/Artwork|Epic - Artwork]]
- [[Product/Epics/Metadata|Epic - Metadata]]
- [[Product/Epics/Themes|Epic - Themes]]
- [[Product/Epics/Achievements|Epic - Achievements]]
- [[Product/Epics/Emulator Support|Epic - Emulator Support]]
- [[Product/Epics/Cloud Saves|Epic - Cloud Saves]]
- [[Product/Epics/User Interface|Epic - User Interface]]
- [[Product/Epics/Infrastructure|Epic - Infrastructure]]

## Related

- [[Planning/Releases/Versions|Versions]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]

> Detailed execution now lives in [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]].
