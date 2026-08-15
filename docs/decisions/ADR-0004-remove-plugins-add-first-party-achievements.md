# ADR-0004: Remove plugins and add first-party achievements

- Status: Accepted
- Date: 2026-08-13

## Decision

NovaLauncher will not ship a plugin SDK, plugin host, plugin catalog,
marketplace, scripting surface, or downloaded-code execution. Prior plugin
documents and reports are historical research, not an active product plan.

Achievements become a first-party, read-only feature. Provider adapters are
reviewed, compiled with NovaLauncher, and accessed through typed application
interfaces. The initial target is Steam after Steam identity/import and metadata
foundations exist. RetroAchievements requires a separate feasibility and privacy
decision after emulator identity exists.

## Rationale

Removing third-party execution substantially reduces supply-chain, permission,
update, isolation, and support risk. A first-party achievement pipeline supplies
the desired user value while retaining a bounded and auditable trust surface.

## Consequences

- Historical plugin completion claims do not authorize plugin code in the
  active solution.
- Integrations require repository review, tests, release scanning, and an
  authorized data source.
- Achievement state remains provider-owned and cannot be written or fabricated.
- Any future reversal requires a new ADR and explicit product-owner approval.
