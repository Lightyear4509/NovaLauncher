# ADR-0008: Opt-in read-only Steam achievements

- Status: accepted
- Date: 2026-08-13

NovaLauncher uses only Valve's documented `GetPlayerAchievements` and
`GetSchemaForGame` Web API methods. The integration is disabled unless the user
explicitly supplies `NOVALAUNCHER_STEAM_WEB_API_KEY` and
`NOVALAUNCHER_STEAM_ID`. Those values are read at startup, never persisted, and
never logged. The cache stores only a SHA-256 account fingerprint so cached data
from one configured account cannot appear for another.

Achievement refresh is read-only, bounded, cancellable, rate-limit aware, and
isolated from the core library. Provider or cache failure cannot block launching
or fabricate/mutate unlocks. Valid cache data remains available as explicitly
stale for 30 days. Private profiles and games without achievements produce an
unavailable state.
