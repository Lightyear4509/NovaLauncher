---
type: backlog
status: active
created: 2026-07-29
updated: 2026-07-29
---

# Backlog

This is the canonical backlog view. Work items remain in their product or operations folders and appear here through their properties.

```dataview
TABLE type, priority, release, owner
FROM "Product/Features" OR "Operations/Bugs"
WHERE status = "backlog"
SORT priority DESC, file.name ASC
```

## Related

- [[Planning/Sprints/Current Sprint|Current Sprint]]
- [[Planning/Sprints/Sprint Board|Sprint Board]]
- [[Product/Product Index|Product Index]]

