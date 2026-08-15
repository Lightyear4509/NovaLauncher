---
type: dashboard
status: active
aliases:
  - NovaLauncher HQ
created: 2026-07-29
updated: 2026-07-29
---

# 🎮 NovaLauncher HQ

> One launcher. Every game. Your way.

## Current direction

- Version: **0.1 Alpha**
- Current focus: **Artwork system**
- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Planning/Sprints/Sprint Board|Sprint Board]]
- [[Planning/Backlog|Backlog]]

## Command center

- [[Dashboard/Project Dashboard|Project Dashboard]]
- [[Dashboard/Release Dashboard|Release Dashboard]]
- [[Dashboard/Development Dashboard|Development Dashboard]]
- [[Dashboard/Architecture Dashboard|Architecture Dashboard]]

## Project knowledge

- [[Product/Product Index|Product Index]]
- [[Product/Vision/Project Bible|Project Bible]]
- [[Product/Design/Master Design Document|Master Design Document]]
- [[Product/Requirements/Product Requirements Document|Product Requirements Document]]
- [[Planning/Roadmap/Master Roadmap|Master Roadmap]]
- [[Decisions/Decision Index|Decision Index]]

## Engineering

- [[Engineering/Architecture/Architecture Index|Architecture Index]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Engineering/Standards/Coding Standards|Coding Standards]]
- [[Guides/Developer Handbook|Developer Handbook]]

## Active work

```dataview
TABLE type, priority, release, progress
FROM "Product" OR "Operations/Bugs"
WHERE status = "active" OR status = "in-progress" OR status = "blocked"
SORT priority DESC, file.name ASC
```

## Recent decisions

```dataview
TABLE status, updated
FROM "Decisions"
WHERE type = "decision"
SORT updated DESC
LIMIT 5
```

> Build the launcher you wish already existed.
