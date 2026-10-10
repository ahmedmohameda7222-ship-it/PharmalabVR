# PharmaLabVR implementation decision ledger

## P00 Checkout authority and build prerequisites

**Plan.** Establish the empty repository's minimal `main`, create `feat/foundation-vr-first`, preserve the v3 numbered authority and approved concept, keep original DOCX/PDF references local, and make environment discovery executable and testable.

**Review.** The package verifier passed its preparation-only scope. The live remote had no refs. The bootstrap was pushed to `main`, the feature branch was created, and C00 tests exercise required-tool failure, explicit missing-Unity allowance, and structured evidence output. The actual environment report records Git 2.54.0, Python 3.12.14, and missing CMake, C++ compiler, Unity, and physical VR hardware.

**Critic.** The host cannot compile native or Unity code yet, and GitHub CLI authentication is invalid. `--allow-missing-unity` does not hide missing native prerequisites: the real doctor run exits 1. No product, Player, simulator, headset, or scientific validation claim follows from P00.

**Alternatives and long term.** Continue source-first independent work, native reference verification through the bundled Python runtime, and CI definitions. Use GitHub-hosted native CI for compiler-backed evidence once pushed. Do not install or activate licensed Unity tooling silently; use the pinned Editor when available and record exact compatibility results.

**Ruling.** A fresh clone on the named feature branch is the isolated implementation workspace because the remote was empty and required a root `main` bootstrap before linked-worktree creation. Cost if wrong: isolation is provided by the dedicated clone rather than worktree metadata.

**Ruling.** Original source documents remain only under the local extracted package. The public repository contains v3 authority, manifest, preparation verification, and the concept image marked concept-only. Cost if wrong: later reviewers must use the local package for the original references.
