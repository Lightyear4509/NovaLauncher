---
type: architecture
status: active
created: 2026-07-29
updated: 2026-07-29
---

# System Map

```text
                 NovaLauncher

                      │

      ┌───────────────┼───────────────┐

      │               │               │

      UI          Services         Plugins

                      │

     ┌────────────┬───────────────┐

     │            │               │

 Artwork     Metadata      Library

     │

ArtworkProviderManager

     │

Steam
SteamGridDB
Local
Plugins
```

## Related

- [[Engineering/Architecture/Architecture Overview|Architecture Overview]]
- [[Engineering/Architecture/Artwork System|Artwork System]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
