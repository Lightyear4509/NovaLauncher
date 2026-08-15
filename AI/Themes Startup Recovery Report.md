---
type: implementation-report
status: complete
feature: "[[Product/Features/Theme Reliability|Theme Reliability]]"
created: 2026-07-30
updated: 2026-07-30
source_commit: f2d0c3a
---

# Themes Startup Recovery Report

## Incident

Visual Studio successfully launched the NovaLauncher process, but no main
window appeared. The debugger output contained normal module-load messages and
no exception. Process-only smoke tests incorrectly treated the live process as
a successful startup.

## Root cause

`App.OnFrameworkInitializationCompleted` synchronously waited on
`ThemeService.InitializeAsync`. The settings load yielded while retaining the
Avalonia synchronization context, and its continuation could not run because
the UI thread was blocked. The process remained alive and responsive at the
process level, but window construction never began.

## Correction

- Theme preference loading and fallback normalization now complete off the
  Avalonia UI thread.
- The resolved canonical theme is then applied on the UI thread.
- Main-window construction continues synchronously after theme application.
- Theme-service tests now exercise preference loading and application as
  separate boundaries.

## Verification

- Visual Studio 2022 loaded and built the executable project successfully.
- Debug build: passed with four pre-existing nullable warnings.
- Debug tests: 163 passed, 0 failed.
- Debug window check: responsive, nonzero handle, title `NovaLauncher`.
- Release build: passed with four pre-existing nullable warnings.
- Release tests: 163 passed, 0 failed.
- Release window check: responsive, nonzero handle, title `NovaLauncher`.

## Process improvement

Launcher startup validation must require all of the following:

1. process remains alive
2. process is responding
3. top-level window handle is nonzero
4. main-window title is `NovaLauncher`

A process-only liveness check is insufficient for desktop UI validation.

## Related

- [[Product/Features/Theme Reliability|Theme Reliability]]
- [[Decisions/ADR-014 Atomic Theme Application and Persistence|ADR-014]]
- [[AI/Themes Increment 1 Report|Themes Increment 1 Report]]
- [[AI/Themes Increment 2 Report|Themes Increment 2 Report]]
