---
type: index
area: features
status: active
created: 2026-07-29
updated: 2026-07-29
---

# Feature Index

Feature notes should be created from [[Templates/Feature|Feature Template]].

```dataview
TABLE status, priority, release, owner, progress
FROM "Product/Features"
WHERE type = "feature"
SORT status ASC, priority DESC, file.name ASC
```

## Related

- [[Planning/Backlog|Backlog]]
- [[Product/Product Index|Product Index]]

