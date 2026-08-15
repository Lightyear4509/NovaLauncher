# GitHub publication procedure

1. Initialize a Git repository, review every staged path, and ensure generated
   `.dotnet`, `.tools`, `artifacts`, and `TestResults` content is ignored.
2. Create the public repository with the MIT `LICENSE`, `CONTRIBUTING.md`,
   `SECURITY.md`, source, tests, canonical documentation, and workflow.
3. Protect the main branch and require the build workflow. Enable Dependabot,
   secret scanning, private vulnerability reporting, and immutable releases if
   available for the repository.
4. Run all release gates on clean Windows 10 22H2 and current Windows 11 VMs.
5. Create tag `v0.1.0-alpha.1` only after the checklist is approved. Attach the
   installer, portable ZIP, CycloneDX SBOM, and `SHA256SUMS.txt` to one GitHub
   prerelease. Do not substitute artifacts built on another commit.

Publishing is intentionally not automated from this workspace and requires the
repository owner's GitHub authorization.
