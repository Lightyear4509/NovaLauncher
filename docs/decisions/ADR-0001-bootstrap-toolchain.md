# ADR-0001: Bootstrap toolchain

Status: Accepted for Increment 0

Date: 2026-08-13

## Decision

NovaLauncher targets .NET 10 LTS and Avalonia 12.1.1 for the Windows x64 alpha.
The SDK is pinned to 10.0.302. NuGet versions are centrally pinned and restore
uses committed lock files. Application boundaries are Domain, Application,
Infrastructure, and App; dependencies point inward.

The initial local log is newline-delimited structured JSON under the current
user's local application-data folder. Increment 0 records no user library data,
credentials, environment values, or launch arguments.

## Consequences

- Development requires the pinned .NET SDK or a compatible later patch.
- The project owner must select the final license before distribution.
- Avalonia and dependency upgrades require a separate reviewed change with a
  successful Debug/Release and startup gate.
