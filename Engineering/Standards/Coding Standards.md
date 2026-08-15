---
type: standard
status: active
created: 2026-07-29
updated: 2026-07-29
---

# NovaLauncher Coding Standards

---

# General Principles

Always write code that is:

- Readable
- Testable
- Maintainable
- Documented

Never optimize readability away.

---

# Architecture

NovaLauncher uses:

- MVVM
- Dependency Injection
- Provider Pattern
- Service Layer
- Repository Pattern (when appropriate)

---

# Naming

Classes

PascalCase

Example

ArtworkService

ArtworkProviderManager

---

Interfaces

Always begin with I.

Examples

IArtworkProvider

IGameImporter

IMetadataProvider

---

Methods

PascalCase

GetArtworkAsync()

ImportSteamLibrary()

LoadMetadata()

---

Private Fields

_beginWithUnderscore

Example

_artworkCache

_httpClient

_providerManager

---

Properties

PascalCase

GameName

LibraryPath

CoverImage

---

# Async

Async methods must end with Async.

Example

GetGameAsync()

LoadArtworkAsync()

---

# Dependency Injection

Avoid

new HttpClient()

inside services.

Inject dependencies.

Good

ArtworkService

↓

IArtworkProvider

↓

SteamProvider

Bad

ArtworkService

↓

new SteamProvider()

---

# Comments

Explain WHY.

Avoid explaining WHAT.

Good

// Steam limits requests to 100/minute.

Bad

// Increment i

---

# Folder Structure

Core

Domain

Services

Providers

ViewModels

Views

Models

Assets

Themes

Utilities

Plugins

---

# Git

Commit often.

Keep commits small.

Example

feat: add SteamGridDB provider

fix: artwork cache bug

refactor: provider manager

docs: update architecture

---

# Documentation

Every new service should receive:

Architecture note

Service documentation

Roadmap update (if applicable)

Decision record (if architectural)

## Related

- [[Engineering/Architecture/Architecture Index|Architecture Index]]
- [[Engineering/Services/Service Catalog|Service Catalog]]
- [[Templates/Architecture|Architecture Template]]
- [[Templates/ADR|ADR Template]]
- [[Planning/Releases/Changelog|Changelog]]
