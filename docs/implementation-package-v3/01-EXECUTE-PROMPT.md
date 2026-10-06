# Send this prompt to the implementation chat

You are implementing PharmaLabVR, not replanning it. The user supplied this ZIP and authorized complete foundation implementation and pushing it to `https://github.com/ahmedmohameda7222-ship-it/PharmalabVR`. Read `00-START-HERE.md` and all numbered documents completely. The latest priority is **VR-first, with complete desktop mouse/keyboard support**, not desktop-only or browser-based.

Use your existing model settings. No subagent or model change is required. Use `superpowers:executing-plans`, TDD and verification-before-completion if available. The package is the execution plan; do not generate a substitute product roadmap or ask the user to reapprove its existing decisions. Make routine coding choices consistent with exact interfaces here. Diagnose compile/test failures rather than following broken instructions blindly; record any material deviation with the failing evidence and smallest compatible fix.

## Start and continue

1. Verify the ZIP using the included verifier. Inspect the actual repository, remote, default branch, clean/dirty state and existing AGENTS.md. Reconcile changes since the packaged inspection. Clone the named repository into the assigned project workspace if absent; never create a replacement repository or work in the planner's vendor checkout.
2. Put this package's numbered authority docs/manifest in `docs/implementation-package-v3/`. Keep original user documents local reference-only; do not upload them to the public repository unless the user later asks. Copy the approved concept to `docs/design/approved-vr-concept.png`, identified as concept-only. Preserve vendor source/license notices as described in the source manifest.
3. Inspect Unity/compiler/CMake/Python/Android modules and Git auth. Use pinned versions. Do not invent an installed Editor or a successful build. The user has no headset; this is expected and does not block code, simulator or desktop work.
4. Execute P00–P16 from `05-EXACT-TASKS.md`, including the tests mapped in `06-TEST-CATALOG.md`. Implement VR rig, action mapping and spatial UI before polishing desktop UI. The same native material authority must drive both input modes.
5. After every task, write Plan, Review, Critic and Alternatives/Long-term notes, fix defects, run affected checks and make focused local commits. Continue until the authorized foundation is implemented or an actual external dependency prevents further dependent work. Do independent work rather than stop everything for a headset or measurement gap.
6. Run the complete verification/build matrix, create an exact-head completion report and push as specified in `07-BUILD-CI-AND-PUSH.md`. Resolve applicable CI failures caused by your changes. Do not merge. Return the branch/commit/PR URL, artifact paths, passed checks, failed checks and missing evidence so the planner can review the exact delivered revision.

## Required result

A genuine Unity VR laboratory with seated/standing setup, tracked controllers, direct tool manipulation, two-hand valve/receiver operation, ray menus, clear capability status, measurement view, pause/recenter/recovery, save/load/review and a polished bench matching the approved direction. Also a real Windows mouse/keyboard executable that works without headset/runtime. Standalone Android ARM64 build support and PC OpenXR mode must be configured and built when tooling permits; lack of a physical headset must not be hidden.

Science and quantities come from the native core and explicit reviewed models/data, not `if experiment == 1` outcomes or last-added-color logic. Publish no fabricated scientific range. The initial research profile and its approximate indicator/mixing capabilities remain clearly labeled. Do not replace an unavailable engine with guessed formulas silently; the independent ideal reference path has its own explicit identity and status.

No required login/server/cloud, no billing, no telemetry service and no purchases. No secret tokens or activation credentials in Git, logs, screenshots or ZIP artifacts. Use existing authorized Git authentication. Installing licensed/system tools may require the environment's approval; obey it and report the exact blocker without claiming complete readiness.

## Nonnegotiable behaviors

- One state authority; atomic source/receiver/spill updates; no negative inventory or double execution.
- Separate per-vessel revisions, observation freshness/support/computation/package maturity and per-observable dependencies.
- Bounded queues, responsive head/menu tracking, no material jump on frame catch-up or recovery.
- Real calibrated tool quantities plus explicit research volume assumptions; graphics quality never changes chemistry.
- Durable Save and honest last-recoverable boundary; load into a fresh validated context; pinned model identity and no silent recompute migration.
- No-XR desktop startup; camera/UI input does not accidentally pour or move tools.
- OpenXR device abstraction and isolated vendor features; missing hardware means EvidenceMissing.
- Tests run against actual code, not a duplicated test-only algorithm or a mock that declares itself accurate.
- All severe correctness/build/interaction failures fixed before calling implementation complete. Missing experimental/physical evidence is explicit and prevents the affected release claim, even if software is implemented.

The user wants action, tests, UI/UX and a pushed working foundation in one continuing assignment. Do not return only a plan, skeleton or screenshot. If the environment cannot compile a target, finish the source, other real checks and exact setup/build instructions, then label that target BuildNotVerified. Do not say “all done” when it is not.
