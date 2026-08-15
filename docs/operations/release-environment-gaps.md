# Release environment gaps

Observed on 2026-08-13:

- Host: Microsoft Windows NT 10.0.26200.0 (Windows 11 preview/current branch)
- Narrator executable: present
- NVDA executable: not found
- 200% display-scale observation: not performed; changing the user's display
  setting is outside automated test authority
- Windows 10 22H2 VM: unavailable
- Separate clean Windows 11 VM: unavailable
- Code-signing certificate: not supplied
- Publisher identity: not supplied
- Approved open-source license: not supplied; `LICENSE` remains a non-grant placeholder
- Installer toolchain (`makeappx`, WiX, Inno Setup, NSIS): not found

These are release blockers, not test skips that can be waived by implementation.
An unsigned portable preview may be generated only after the owner approves the
license and prominent unsigned-build labeling.
