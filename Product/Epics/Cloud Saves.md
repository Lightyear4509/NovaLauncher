---
type: epic
status: planned
priority: critical
release: post-alpha
progress: 0
aliases:
  - Epic - Cloud Saves
created: 2026-07-29
updated: 2026-07-30
---

# Epic - Cloud Saves

## Goal

Provide safe, seamless continuation across devices while keeping the user in
control of save data.

## Features

- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- Local snapshots and version history
- Ludusavi save discovery
- Atomic restore
- Device identity and leases
- Conflict detection and keep-both recovery
- End-to-end encryption
- Offline queue
- Tailscale peer transport
- Local folder and NAS transport
- Future WebDAV/object-store adapters

## Explicit non-goals

- Silent last-write-wins
- Tailscale lock-in
- Uploading unstable files while a game is running
- Deleting the last known-good generation during sync

## Related

- [[Product/Features/Seamless Cloud Saves|Seamless Cloud Saves]]
- [[Decisions/ADR-016 Cloud Saves Transport Separation|ADR-016]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
