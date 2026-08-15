---
type: dashboard
area: project
status: active
created: 2026-07-29
updated: 2026-07-29
---

# Project Dashboard

## Milestones

✅ Architecture

✅ Documentation

🟨 Steam Import

🟨 Artwork System

⬜ First-party achievements

⬜ ROM Support

⬜ Themes

⬜ Cloud Saves

⬜ Version 1.0

---

## Current Statistics

```dataview
TABLE WITHOUT ID
  length(filter(rows, (r) => r.type = "epic")) AS Epics,
  length(filter(rows, (r) => r.type = "service")) AS Services,
  length(filter(rows, (r) => r.type = "decision")) AS Decisions
FROM "Product/Epics" OR "Engineering/Services" OR "Decisions"
GROUP BY true
```

---

## Next Major Goal

SteamGridDB Integration

## Canonical planning

- [[Planning/Roadmap/Master Roadmap|Master Roadmap]]
- [[Planning/Releases/Versions|Versions]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]
