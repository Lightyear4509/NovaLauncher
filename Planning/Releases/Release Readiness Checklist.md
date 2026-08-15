---
type: release-checklist
status: canonical
version: "1.0"
created: 2026-08-13
updated: 2026-08-13
---

# Release Readiness Checklist

No NovaLauncher build may be called downloadable, ready to use, or released
until all required items below are checked with linked evidence.

## Source and legal

- [ ] Source solution and tests are present in the release commit.
- [x] Repository has an explicit MIT license and third-party notices.
- [ ] SDK and dependencies are pinned; lock files exist but no Git commit exists yet.
- [x] Dependency vulnerability and documented license review passes.
- [ ] No secret, credential, personal path, or user library data is present.

## Product

- [ ] Every in-scope journey in the Alpha Release Specification is demonstrated.
- [ ] Deferred features are absent or visibly labeled unavailable.
- [ ] First-run, empty, loading, offline, partial-failure, and recovery states work.
- [ ] Destructive actions cannot delete installed games or out-of-scope files.
- [ ] Privacy defaults are local-only and network activity is user-initiated.

## Quality and safety

- [ ] All CI gates in the Safety and Test Plan pass from a clean checkout.
- [ ] Zero test failures; skips have named owners and written release approval.
- [ ] Persistence fault/recovery and backup/restore drills pass.
- [ ] Security/adversarial suite and secret-canary scan pass.
- [ ] Accessibility keyboard and 200%-scale checks pass.
- [ ] Performance gates pass on the documented reference system.
- [ ] No unresolved critical/high defect; accepted lower risks are documented.

## Packaging

- [x] `win-x64` self-contained Release publish succeeds.
- [ ] Installer and portable ZIP start on a clean supported Windows VM.
- [ ] Install, launch, upgrade with preserved data, repair, and uninstall pass.
- [ ] Uninstall preserves user library/backup data unless the user explicitly
  selects its removal.
- [x] App version, diagnostics version, filenames, and release notes agree.
- [x] SHA-256 checksums and a CycloneDX SBOM are generated beside artifacts.
- [x] Binaries/installer are signed, or the unsigned alpha warning and checksum
  verification steps are prominent.

## Final smoke matrix

- [ ] Two standard-user Windows devices pair over a real tailnet without admin/API tokens
- [ ] Changed-file push, offline retry, pull-before-launch, and all conflict choices pass on real devices
- [ ] Transfer interruption, process exit during sync, disk-full restore, and Windows Firewall behavior are verified

- [ ] Empty profile, no network
- [ ] Steam absent
- [ ] Steam installed with small and large libraries
- [ ] Invalid/corrupt manifests
- [ ] Missing/moved game executable
- [ ] Read-only or full data volume
- [ ] Corrupt primary store with valid backup
- [ ] Corrupt primary and backup
- [ ] Metadata/artwork timeout and cancellation
- [ ] Standard user account (no administrator rights)
- [ ] Windows 10 22H2 and current Windows 11

## Evidence record

Release notes must link the CI run and include:

- commit and version;
- test totals, coverage, and approved skips;
- Windows versions and VM images tested;
- known issues and recovery guidance;
- artifact filenames, sizes, and SHA-256 values;
- signing status and SBOM filename.
