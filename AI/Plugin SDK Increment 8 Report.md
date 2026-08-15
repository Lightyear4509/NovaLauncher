---
type: implementation-report
status: complete
area: plugins
increment: 8
created: 2026-07-30
updated: 2026-07-30
---

# Plugin SDK Increment 8 Report

## Outcome

Increment 8 is complete. NovaLauncher now has an isolated deny-by-default
broker policy foundation for future plugin resource access. It grants no
authority, performs no resource operation, changes no runtime message, and is
referenced by neither launcher nor worker.

## Implemented

- Added `NovaLauncher.PluginBroker`, referencing only the public Plugin SDK.
- Added separate typed requests for canonical local file access, exact HTTPS
  network requests, trusted process profiles, opaque credential use, and
  game-scoped save data.
- Added exact plugin ID, semantic version, policy version, manifest permission,
  scope, quota, timeout, and validity binding.
- Added disabled-by-default policy state and a maximum 90-day policy window.
- Made per-operation consent the only consent mode.
- Added five-minute maximum consent receipts bound to request ID, identity,
  resource kind, complete trusted policy, declared byte ceiling, and an
  unambiguous length-prefixed SHA-256 fingerprint.
- Added canonical local-path validation with UNC/device and alternate-stream
  rejection.
- Added exact public DNS host, HTTPS port, and method rules.
- Added process profile IDs and bounded named parameters instead of plugin
  executable paths or raw command strings.
- Added opaque credential handles; no secret value exists in a request.
- Added operation and byte quotas plus bounded operation timeouts.
- Added redacted audit decisions with no path, URL, parameter, handle, purpose,
  save path, or secret.
- Added explicit final-handle path-verification requirements to allowed file
  and save-data decisions.
- Added cancellation propagation.
- Added the Windows restricted-identity proof plan and adversarial denial gate.

## Verification

- Debug solution rebuild: passed, 0 errors.
- Release solution build: passed, 0 errors.
- Debug tests: 372 passed, 0 failed.
- Release tests: 372 passed, 0 failed.
- Focused broker tests: 32 passed, 0 failed.
- SDK package: `NovaLauncher.PluginSdk.1.0.0-alpha.1.nupkg` created.
- Debug launcher: responsive, nonzero window handle, correct title.
- Release launcher: responsive, nonzero window handle, correct title.
- Broker project references only `NovaLauncher.PluginSdk`.
- Launcher does not reference `NovaLauncher.PluginBroker`.
- Brain internal-link audit: 130 Markdown notes, 857 wiki links, 0 broken
  links.
- Four pre-existing nullable warnings remain in `CinematicHero.axaml.cs`.
- No new warning was introduced.

## Safety boundary

- No broker executor exists.
- No broker transport exists.
- The host protocol is unchanged.
- No plugin can submit a broker request.
- No filesystem, network, process, credential, save-data, secret, registry, or
  native operation is performed.
- Policy allowance is not treated as proof of OS isolation.

## Remaining risks

- The ordinary plugin worker retains the current user's operating-system
  authority.
- Policy alone cannot prevent direct .NET/OS API access.
- Syntactic path checks cannot prevent reparse-point or
  time-of-check/time-of-use escape; final opened-handle verification is still
  required.
- Exact hostname rules cannot prevent DNS rebinding; address classification,
  connection pinning, and redirect reauthorization remain required.
- Quotas are trustworthy only when a future broker owns atomic usage state;
  the policy evaluator currently consumes a trusted usage snapshot.
- No broker wire format, authenticated transport, or executor exists yet.
- AppContainer compatibility with the .NET worker and authenticated IPC has
  not been proven.
- Process job limits, restricted identity, handle inheritance, registry
  denial, and network denial have not been tested adversarially.
- No production catalog roots, rotation, or emergency revocation channel is
  configured.

## Recommended next step

Implement Increment 9 as a Windows restriction prototype and adversarial test
harness only. Start the self-contained worker under AppContainer or an
equivalently enforceable identity, retain authenticated private IPC, deny broad
capabilities and direct network, apply kill-on-close job containment, and prove
denial of user files, save roots, launcher data, credentials, registry,
arbitrary processes, and network. Do not add provider operations in that
increment.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[Decisions/ADR-015 Plugin Trust and Isolation|ADR-015 Plugin Trust and Isolation]]
- [[Planning/Roadmap/Alpha Ecosystem and Cloud Roadmap|Alpha Ecosystem and Cloud Roadmap]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/AI Context|AI Context]]
