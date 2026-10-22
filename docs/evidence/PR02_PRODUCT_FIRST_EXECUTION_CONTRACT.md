# PharmaLabVR — Product-First Implementation Contract (PR #2)

**Adopted by project owner: 22 October 2026.** This document governs development priority and reporting; it does not alter formal scientific, Unity, student-pilot, or release acceptance criteria.

## Baseline and repository topology

- PR #1 was squash-merged into `main` as development-only checkpoint `d570035dd55a7650c6790f5c3306a0fe4059eeff`.
- This continuation branch `feat/product-first-lab-milestones` starts from that checkpoint. PR #1's historical branch is not the active branch.
- Baseline before this continuation: 66 original tasks; 15 VerifiedLocal, 51 Open; formal next P06.02; G-R1 Open; U01–U05 academic workstream separate, G-U1 Open; Unity Editor/Player/XR verification deferred.
- `docs/evidence/CODE_COMPLETION_MATRIX.md` is an independent source audit (initial snapshot 15 CompleteSource, 28 Partial, 23 Missing). It is **not current acceptance evidence** and must be refreshed from actual production code as milestones progress.

## Non-negotiable owner intent

**Finish an actual usable pharmacy laboratory first.** A green CI run, a passing isolated helper, a dashboard control, a source file, or a hundred commits is not a functioning laboratory. Source work is not Unity runtime verification. Do not build unrelated university portal screens, add large chemistry catalogs, refactor for aesthetics, or repeat tiny test/documentation loops while primary learner actions are incomplete.

Retain existing code. No resets, history rewrites, deletions of scientific evidence, unapproved schema changes, release tags, or fictitious completion claims.

Product permanently English-only. Native C++17 + pinned IPhreeqc/MINTEQ remains sole chemical authority. Unity owns user interaction and rendering only. Never fabricate values, receipt identity, events, timestamps, model validation, successful sync, grades or student pilot outcomes. Preserve Native SessionLoad Hold, Paused/Hold recovery and checkpoint isolation. Offline-first always.

Preserve U01–U05 and deployed Supabase; freeze **discretionary** cloud/portal growth until integrated learner milestones. No writes/migrations/Edge deployments to owner's live Supabase without specific owner approval. Never ship a service-role/secret key in Unity or browser; tenant RLS and immutable unverified evidence quarantine must remain in place.

No new Unity cloud CI. Native/Node/.NET tests and disposable isolated Supabase CI are permitted. Author genuine EditMode/PlayMode/Player/XR checks for final local Windows Codex Unity campaign; do not mark them executed without proof.

## Delivery milestones (sequential product priority, parallel safe coding allowed)

**M1 — Visible interactive lab entry and UI (P06.02, P08.01–P08.03, relevant P07.01/P10):**
Boot -> select Desktop or VR -> initialize pinned Native scientific package -> Setup/Prepare -> enter persistent Lab -> see workbench, distinguishable instruments, material identities, measurement panel, Pause/Hold/Settings. True scene wiring and input actions, not only manually invoked controller methods. Compile/test sources, scene/prefab/build configuration and deterministic screenshots harness. `P08.04` visual confirmation deferred until actual Unity rendering. Never call M1 UnityVerified prematurely.

**M2 — Real bench action loop (P03.01–P03.03, P07.01–P07.03, P08.02/P08.03, P09/P10):**
A user can grab/position bottle, pipette, burette, dock/place; prepare authorized stock; transfer liquid via Desktop and XR; see Native-committed volumes/meniscus, select receiver, obtain only current supported pH; closed valve final drop, aperture/capture/moving receiver/overflow/rinse/contamination/volume conservation and pause/recovery all fail safely. Every action must be tied to the real Native command/event, not presentation-only fake transfer. Desktop and XR use the same scientific authority.

**M3 — One complete offline learner journey (P11.01–P11.03, P12.01–P12.03):**
Cold start -> valid supported experiment -> prediction -> prepare -> perform Native transport -> measure -> record immutable branch/event-scoped Notebook evidence -> explain/compare -> Save -> Load -> Review -> export + restore. Then cover a second genuine supported research lesson with honest capability gating. A restored checkpoint may not credit future-event Notebook entries. Model unavailability is explicit. No grades or cloud attestations invented.

**M4 — Instructor/expert use of actual learner evidence (P17 small supported pack, P23.01–P23.03, selected U01–U05 integration):**
Instructor authors/imports supported lesson, student executes, instructor reviews real recorded attempt and provides bounded feedback; expert compares two real runs, including model/version/units and uncertainty. Reuse deployed Supabase APIs only when required and after local learner path. Any staged uploaded evidence remains `staged_unverified` until a real Native-origin verification + server acknowledgement exists. Real campus pilot remains external.

**M5 — Code complete + reproducible handoff (P13–P16, P14/P15 evidence source):**
Unity 6000.3.25f1 editor/build scripts, Windows Desktop/Windows VR/Android VR/Pico target prep, asset manifest and licenses, exact-sha CI, offline safety tests, generated scenes/prefabs, explicit technical debt list, deterministic guided final Codex campaign. `CodeComplete` does not equal `UnityVerified`. Final local Codex Editor/PlayMode/Player/XR campaign and device/human/scientific validation are separate acceptance gates.

**M6 — Remaining P17–P24 expansion:** continue scientifically supported packages and lesson coverage after the first-release core, never falsely advertise unsupported chemistry. Do not let expansion postpone the first usable product.

## Anti-drift execution loop

1. At startup read the newest `main` squash checkpoint, active branch, actual production files, `CODE_COMPLETION_MATRIX.md`, `docs/evidence/execution-state.json`, and `docs/premium-master` phase dependencies. This source audit must be finite (one brief pass) and immediately followed by code.
2. Select **one missing end-to-end user action** along the M1->M2->M3 journey. Explain precise before/after behavior and necessary dependencies in <=150 words; implement the action **in the same working turn**.
3. Wire source, scene/prefab, native adapter, UI and error states as appropriate; author bounded relevant regression tests; run available CI and collect exact SHA evidence.
4. Make one coherent production-feature commit or small purposeful series, not recurring test-only/notes-only bursts. Update matrix once per meaningful milestone, not once per assertion.
5. Report in 6 fields: user-visible capability; production integration path; tested exact commit & actual pass scope; source gaps; Unity deferred checks; next end-to-end user action.
6. After at most **two consecutive non-user-visible maintenance fixes**, return to the primary M1/M2/M3 journey. Exception: safety/regression issues that block responsible continuation.
7. No new planning chat, repeated user approvals, standalone screen polish or university feature expansion when source blocker is known. Continue the prioritized product source until a true external permission, destructive action, data cost or credential boundary prevents it.
8. Preserve original 66 task acceptance statuses and gate states unless their specified evidence actually exists. Track separate `SourceComplete | Partial | Missing | ExternalBlocked` statuses with file references. `NOT_RUN` is distinct from failed or passed.
9. If source completion can't proceed without a local Unity check, implement defensively and record concrete targeted Unity tests in the final Codex queue. Do not call Unity tests passed based on a C# stub.
10. Do not merge this PR or imply release without owner instruction.

## First immediate implementation priority

Refresh `docs/evidence/CODE_COMPLETION_MATRIX.md` **only for already changed M1/M2/M3 tasks**, based on current actual source, then implement the **highest impact missing wiring** to complete `Boot -> Setup -> Lab -> pick real instrument -> Native transfer -> supported observation -> Save/Review`, including Desktop/XR state invariants. Do not spend the next batch only writing a matrix or postmortem.

**Success is an independently replayable user journey with exact source/build prerequisites; final runtime validation belongs to the planned local Codex campaign.**
