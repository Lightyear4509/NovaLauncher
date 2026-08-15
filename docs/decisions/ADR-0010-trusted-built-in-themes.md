# ADR-0010: Five trusted built-in themes only

- Status: accepted
- Date: 2026-08-13

NovaLauncher ships five compiled palettes behind a UI-independent theme service
and an Avalonia host. Theme changes apply on the UI thread and persist through
the atomic settings store. A failed save restores both the prior runtime palette
and settings state. Unknown IDs fall back to Nova Dark.

No community package, CSS, script, downloaded asset, or assembly is loaded as a
theme. All primary shell colors resolve through dynamic resources so light and
dark palettes update without rebuilding views.
