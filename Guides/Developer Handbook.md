---
type: guide
audience: contributors
status: active
created: 2026-07-29
updated: 2026-07-29
---

# NovaLauncher Developer Handbook

Welcome to NovaLauncher.

This handbook explains how contributors work on the project.

---

# Philosophy

Write code for the next developer.

That developer might be you six months from now.

---

# Before Writing Code

Read:

- [[Product/Vision/Project Bible|Project Bible]]
- [[Product/Design/Master Design Document|Master Design Document]]
- [[Engineering/Architecture/Architecture Index|Architecture Notes]]
- [[Engineering/Standards/Coding Standards|Coding Standards]]
- [[Planning/Sprints/Current Sprint|Current Sprint]]

---

# Workflow

Idea

↓

Research

↓

Design

↓

Implementation

↓

Testing

↓

Documentation

↓

Commit

↓

Pull Request

---

# Branch Naming

feature/artwork-cache

feature/plugin-sdk

bugfix/cache-crash

refactor/provider-manager

docs/architecture

---

# Commit Messages

feat:

fix:

refactor:

docs:

style:

perf:

test:

---

# Code Reviews

Ask:

Is this readable?

Is this maintainable?

Is it testable?

Can it become a plugin?

Does it violate architecture?

---

# Documentation Rule

Every significant feature updates:

Architecture

Roadmap

Changelog

Service docs

Decision Record

---

# AI Collaboration

AI should:

Explain architecture.

Prefer clean code.

Generate complete files when practical.

Avoid unnecessary rewrites.

Document major decisions.

Keep solutions incremental.

---

# Pull Requests

Include:

Purpose

Screenshots (if UI)

Testing

Files changed

Documentation updated

Future improvements

---

# Long-Term Goal

NovaLauncher should remain understandable, maintainable, and welcoming to contributors of all experience levels.

## Related

- [[Guides/AI Collaboration Guide|AI Collaboration Guide]]
- [[Planning/Releases/Changelog|Changelog]]
- [[Decisions/Decision Index|Decision Index]]
