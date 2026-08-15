# ADR-0009: Honest Home, Library, and Saves navigation

- Status: accepted
- Date: 2026-08-13

NovaLauncher adopts an independently branded sidebar with Home, Library, and
Saves surfaces. Home may show favorites and recently added games, but must not
label anything recently played until an authorized play-history source exists.
The Saves page exposes NovaLauncher backup/restore and a disabled cross-device
link control. It must not imply game-save discovery, upload, synchronization, or
conflict resolution exists.

This permits the visual information architecture to mature before cloud-save
security is designed, while keeping every unavailable capability explicit and
accessible.
