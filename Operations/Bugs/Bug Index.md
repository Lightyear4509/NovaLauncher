---
type: index
area: bugs
status: active
created: 2026-07-29
updated: 2026-07-29
---

# Bug Index

Bug notes should be created from [[Templates/Bug|Bug Template]].

```dataview
TABLE status, priority, release, owner, updated
FROM "Operations/Bugs"
WHERE type = "bug"
SORT priority DESC, status ASC, file.name ASC
```

## Related

- [[Planning/Backlog|Backlog]]
- [[Dashboard/Development Dashboard|Development Dashboard]]

