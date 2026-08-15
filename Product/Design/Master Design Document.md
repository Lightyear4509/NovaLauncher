---
type: design-document
status: active
version: "0.1"
role: canonical-product-design
created: 2026-07-29
updated: 2026-07-29
---

# NovaLauncher
## Master Design Document (MDD)

Version: 0.1

Author: Buzz Lightyear

Status: Active

---

# Table of Contents

1. Executive Summary
2. Vision
3. Product Goals
4. User Experience
5. Feature Roadmap
6. Architecture
7. UI Design
8. Theme System
9. Plugin SDK
10. Artwork Pipeline
11. Metadata Pipeline
12. Library System
13. ROM Support
14. Cloud Saves
15. Database
16. Performance Goals
17. Security
18. Coding Standards
19. Release Plan
20. Future Ideas

---

# 1. Executive Summary

NovaLauncher is a modern, open-source game launcher built around one simple idea:

Every game should exist in one beautiful library regardless of platform.

The launcher should be fast, customizable, extensible, and enjoyable to use.

Rather than replacing existing launchers, NovaLauncher unifies them into one experience.

---

# 2. Vision

Players should never need to remember where they bought a game.

NovaLauncher manages that.

Players simply choose a game and play.

---

# 3. Product Goals

Primary Goals

✔ Beautiful UI

✔ Fast startup

✔ Responsive search

✔ Plugin architecture

✔ Universal game library

✔ Community themes

✔ Controller support

✔ Cross-platform support

---

Secondary Goals

Achievements

Cloud Saves

Statistics

Recommendations

Trailer support

Screenshots

Big Picture Mode

---

# 4. User Experience

Opening NovaLauncher should feel similar to opening Steam on a powerful gaming PC.

The launcher should never feel slow.

Navigation should require as few clicks as possible.

Everything important should be visible immediately.

---

# 5. Supported Platforms

Steam

Epic Games

GOG

Ubisoft

EA

Xbox

Battle.net

Amazon Games

Itch.io

ROM Collections

Manually Added Games

---

# 6. Core Architecture

UI

↓

MVVM

↓

Services

↓

Managers

↓

Providers

↓

External APIs

Each layer has one responsibility.

---

# 7. Service Architecture

Core Services

ArtworkService

MetadataService

LibraryService

ImportService

ThemeService

SettingsService

PluginService

EmulatorService

DownloadService

NotificationService

Future services should follow the same architecture.

---

# 8. Artwork Pipeline

Game

↓

ArtworkService

↓

ArtworkProviderManager

↓

Steam

SteamGridDB

Local Artwork

Plugins

↓

Cache

↓

UI

---

# 9. Metadata Pipeline

Game

↓

MetadataService

↓

Steam

IGDB

RAWG

Plugins

↓

Metadata Cache

↓

Database

---

# 10. Theme Engine

Goals

Dark Mode

Light Mode

Community Themes

Animated Themes

Custom CSS

Dynamic Colors

Wallpaper Integration

Accent Colors

---

# 11. Plugin SDK

Everything possible should become a plugin.

Importers

Artwork Providers

Metadata Providers

Game Actions

Widgets

Themes

Library Views

Download Sources

AI Integrations

---

# 12. Library System

Games are stored independently of launcher.

Every game receives:

Unique ID

Platform

Executable

Artwork

Metadata

Tags

Collections

Play Time

Achievements

Settings

This allows games to move between launchers.

---

# 13. ROM Support

Automatic Scanning

Metadata

Artwork

Emulator Detection

Save Management

Controller Profiles

Platform Detection

Multiple Emulator Support

---

# 14. Cloud Saves

Future

Multiple providers

Dropbox

Google Drive

OneDrive

NAS

Syncthing

User-hosted servers

---

# 15. Database

Current

SQLite

Future

Multiple databases if necessary.

Caching should reduce unnecessary API calls.

---

# 16. Performance Goals

Cold startup under 2 seconds.

Search under 100 ms.

Scrolling should remain smooth.

Artwork loading should be asynchronous.

Large libraries should remain responsive.

---

# 17. Security

No telemetry without consent.

No unnecessary background processes.

No user data collection.

Plugin permissions.

Secure API key storage.

---

# 18. Development Principles

Clean Architecture

Dependency Injection

MVVM

SOLID

Provider Pattern

Plugin-first

Incremental Development

Complete Documentation

---

# 19. Release Plan

Alpha

Steam

Artwork

Themes

Library

Metadata

---

Beta

Plugin SDK

ROM Support

Collections

Cloud Saves

---

Version 1.0

Feature Complete

Stable

Open Source Launch

---

# 20. Long-Term Vision

NovaLauncher should become the launcher that players install first.

Not because it has every feature.

Because it is the launcher that simply feels the best to use.

Everything should be polished.

Everything should feel intentional.

Everything should be customizable.

Every design decision should improve the player's experience.

---

# Success Criteria

Players recommend NovaLauncher over other launchers.

Developers enjoy contributing.

Themes flourish.

Plugins flourish.

The project becomes self-sustaining.

---

# Motto

"One launcher.

Every game.

Your way."

## Related documents

- [[Product/Vision/Project Bible|Project Bible]]
- [[Product/Requirements/Product Requirements Document|Product Requirements Document]]
- [[Engineering/Architecture/Technical Architecture Specification|Technical Architecture Specification]]
- [[Planning/Roadmap/Master Roadmap|Master Roadmap]]
