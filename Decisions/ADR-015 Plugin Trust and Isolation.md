---
type: decision
status: accepted
created: 2026-07-30
updated: 2026-07-30
---

# ADR-015 Plugin Trust and Isolation

## Context

NovaLauncher needs third-party customization, but an in-process .NET assembly
can access the user account with the same operating-system permissions as the
launcher. A manifest permission list alone is not a security sandbox.

## Decision

NovaLauncher will not load third-party plugin assemblies into the launcher
process. The alpha runtime direction is one supervised worker process per
plugin, a versioned allowlisted protocol, managed-code-only loading, explicit
consent, startup quarantine, and update rollback.

The worker process is a crash and dependency boundary, not automatically a
permission sandbox. Resource access must use brokered capabilities or a
separately proven OS-level sandbox before permission declarations can be
described as enforceable. Native code remains prohibited in the alpha plan.

## Consequences

- Alpha SDK can ship without falsely promising sandbox security.
- Process termination provides a definitive unload fallback and isolates
  dependency conflicts from NovaLauncher.
- Curated packages still require review, hashes, consent, disable, and rollback.
- Filesystem, network, process, credential, and native-code capabilities need
  explicit out-of-process design before untrusted distribution.

Increment 1 implements the disclosure and pre-execution validation boundary.
It deliberately does not load plugins.

Increment 2 implements persistent lifecycle state, exact staged-package
validation, SHA-256 integrity, failure quarantine, and retained-version
rollback while continuing to prohibit plugin execution. Hashes establish
integrity, not publisher identity; signatures remain future work.

Increment 4 implements publisher identity for configured trust roots through
purpose-scoped RSA-PSS keys. Catalog signatures authenticate canonical catalog
disclosure; publisher signatures authenticate exact package bytes. Consent is
requested only after both trust layers, checksum, structure, SDK, and manifest
disclosure validate. This does not make future worker execution sandboxed.

Increment 5 implements the non-executing host boundary: bounded strict
messages, authenticated sessions, health checks, deadlines, cancellation,
typed failures, forced worker-tree termination, and safe staging cleanup. The
launcher still references no runtime project, and the host cannot receive or
load a plugin package.

Increment 6 implements verified managed loading inside one worker per plugin.
Both sides verify package integrity and identity; the worker rejects native
permission, native binaries, and declared P/Invoke before extraction/loading.
Initialization and shutdown are deadline-bound, failures feed persisted
quarantine, and process termination is the definitive unload fallback. These
checks are defense in depth and do not turn an ordinary user process into an
OS permission sandbox.

Increment 7 integrates the trusted catalog, inventory, lifecycle, and runtime
coordinators behind one launcher service and a visible command center.
Enablement and runtime start remain separate explicit actions; automatic
startup is opt-in and off by default. Exact signed consent follows all trust
and package validation. The launcher deploys and supervises the host but never
loads third-party assemblies. Provider operations remain absent until brokered
authority or a proven OS restriction design is separately accepted.

Increment 8 defines policy without claiming authority. The isolated broker
project can validate and decide exact typed file, HTTPS, process-profile,
credential-handle, and save-data requests with short-lived per-operation
consent, quotas, timeouts, and redacted audit. It performs no I/O and is
referenced by neither launcher nor worker. File allowance explicitly requires
future final-handle verification, and network allowance requires future
address classification, connection pinning, and redirect reauthorization.
The host protocol remains unchanged.

## Related

- [[Product/Features/Plugin SDK and Catalog|Plugin SDK and Catalog]]
- [[Engineering/Architecture/Plugin System|Plugin System]]
- [[AI/Plugin SDK Increment 7 Report|Plugin SDK Increment 7 Report]]
- [[AI/Plugin SDK Increment 8 Report|Plugin SDK Increment 8 Report]]
