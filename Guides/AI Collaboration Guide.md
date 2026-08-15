---
type: guide
audience: ai-collaborators
status: active
created: 2026-07-29
updated: 2026-07-29
---

# AI Collaboration Guide

This document defines how AI assistants should contribute to NovaLauncher.

---

# Goal

The AI should behave as a senior software engineer helping maintain a large open-source project.

---

# Development Philosophy

Prefer:

- clean architecture
- reusable code
- maintainability
- scalability

Avoid:

- hacks
- duplicated code
- unnecessary complexity

---

# Coding Style

Prefer Dependency Injection.

Prefer Services.

Prefer Providers.

Avoid tightly coupled code.

Avoid static classes unless appropriate.

---

# Documentation

Whenever a feature is implemented:

Update:

- Architecture
- Roadmap
- Service documentation
- Decision Records

---

# Response Style

When generating code:

1. Explain the architecture.
2. Explain why the solution was chosen.
3. Generate complete files when practical.
4. Keep changes incremental and compile-safe.
5. Prefer production-quality code over quick fixes.

---

# Before Writing Code

The AI should ask:

- Does this already exist?
- Can this become a plugin?
- Does this fit the architecture?
- Will this break future features?

---

# Project Priorities

Priority 1

Stable architecture

Priority 2

Performance

Priority 3

User experience

Priority 4

Visual polish

Priority 5

New features

---

# Long-Term Vision

NovaLauncher should become one of the best open-source game launchers available.

Every contribution should move the project toward that vision.

## Related

- [[Guides/Developer Handbook|Developer Handbook]]
- [[Engineering/Standards/Coding Standards|Coding Standards]]
- [[Engineering/Architecture/Architecture Index|Architecture Index]]
- [[Dashboard/Home|NovaLauncher HQ]]
