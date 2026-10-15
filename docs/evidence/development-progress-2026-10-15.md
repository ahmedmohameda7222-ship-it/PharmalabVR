# PharmaLabVR development-first checkpoint — 2026-10-15

**Baseline remote HEAD:** `f7d573c9e45c94ca5620e06439f4dd07e85a7eac`  
**Most recent source SHA:** `a3f0ba222368ba1d7d2cb2b0b803be24e54ffa07` (evidence commit will advance branch HEAD)  
**PR:** [#1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1), unmerged  
**Release gate:** G-R1 Open; no scientific/device/Planner acceptance promoted.

## Implementation batches

| Scope | Test/source commits | Development state | Evidence |
|---|---|---|---|
| Native P04.01 lifecycle regression | `92fb159` | Native verified at that SHA; historical task status untouched | Previous Windows push CI 38681542507 failed 7 native cases because P04 rollback transfer rejected, then other test creates returned PLV_BUSY. Both restored acid **and base** now required Current/Ready before second transfer. [green push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38692195900), [green PR](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38692201399). Not proof all possible Windows lifecycle paths are fixed. |
| P06.02 VR Settings pages | RED test-source `8f44153`, functional `d3e4aa8` | **Partial source, PendingUnityVerification** | Paged world-space UI can change posture, dominant hand, bench/view height, text size, captions, reduced motion flag, contrast, audio mute. Existing Boot/Settings shared state and native Pause/Review preserved; actual XR trigger test NOT_RUN. |
| P08.01 premium lab shell | Test-source `70ee0dd`, functional `98dfdd8` | **Partial source, PendingUnityVerification** | `BuildLabAssets.GenerateEnvironment` authors prefabs/materials/task lights; `BuildPipelineEntry.AllAssets` serializes shell and bench into newly generated Lab scene, while old runtime bench is a fallback only. Checked-in Lab scene has not been regenerated in Unity at this SHA; render quality NOT_RUN. |
| P08.02 graduated burette and meniscus | Test-source `6e8fb98`, `f4e0608`, `ba5a360`; code `5b4433e`, `0f2f00c`, `6e9b86f`, `a3f0ba2` | **Partial source, PendingUnityVerification** | 0–50 ml top-to-bottom major/minor ticks, labelled major marks; committed-volume concave meniscus, zero fill hidden; editor-created persistent mesh/material/ink assets. P08.02 still requires pipette/flask/stopcock complete geometry and real rendered probes. |
| P10.01 Desktop Esc route | Test-source `69bac67`, code `2a8071c` | **Partial source, PendingUnityVerification** | Injected keyboard test source and input adapter now dispatch Esc to shared `LabPanelController.Pause` / Native `Pause`, avoiding mistaken focus-loss hold. No actual Player event executed. |
| Deferred Unity register | RED `5d01ff6` (prior test file missing), GREEN `bbfa43b` | **Tooling verified at GREEN SHA** | `DEFERRED_UNITY_VERIFICATION.md` contains all 66 task IDs/NOT_RUN statuses. Ubuntu CI after the register exists: CTest 1/1, Python tooling 11/11. [push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38693446980) / [PR](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38693452650) both green. |

## Source truth versus verification

- **Official ledger:** 15 VerifiedLocal, 51 Open. All newly touched tasks remain `Open`, `plannerStatus=NotReviewed`. `CodeImplemented` is **not** asserted for full P06/P08/P10 tasks; the source slices are partial and require more implementation.
- **Actual Unity:** no Unity 6000.3.25f1 Editor, EditMode, PlayMode, Player, simulator, screen capture or headset run in this Classic container. Authored tests have no witnessed Unity RED/GREEN. No static/headless test substitutes for Native-backed scene execution.
- **Scientific authority:** no new species/data, no Python chemistry or Unity material pool rewrite; visual marks and meniscus read committed state. P03 material/geometry dependencies and P07 tracked-ray lifecycle remain unresolved.
- **Build assets:** prefab/material generation source exists, but generated prefabs/scene references and real build outputs require later Unity `AllAssets` with native DLL/.so staging; do not advertise Players.
- **Native and CI:** exact new source SHA checks are tracked independently; green earlier SHA must not be copied forward as current-code Unity evidence.

## Immediate development continuation

1. Continue P06.02 shared panel/full accessibility and P07.01 spatial Boot/real headset availability path; source-only readiness and tests first, no Unity pause.
2. Finish P08.01/P08.02 generated lab environment and real instrument placement, P07.02/P07.03 ownership, then P03.01/P03.02/P03.03 connected bottle/pipette/receiver parcels.
3. Continue the canonical priority chain through P08.03–P24, keeping unsupported science packs behind truthful `ReferenceOnly/DataPending` boundaries and G-R1 release guard.
4. Maintain the 66-row deferred Unity campaign after every relevant source commit. Only after all implementation scope is coded create `FINAL_CODE_COMPLETE_HANDOFF.md`; do not create or claim that final handoff prematurely.

## Follow-up visual-capacity consistency

Test-first Unity EditMode source `82ae2877c6678a35f98602f8d683b1c77afb115a` checks that `LiquidPresenter.Configure` updates an existing `MeniscusPresenter` from temporary capacity 1.0 m³ to the actual Native 0.00005 m³ capacity, preserving 0.5 fill at 0.000025 m³. Implemented `MeniscusPresenter.SetProfile` with no mesh regeneration at `e5d917b`; compatibility follow-up `a3f0ba222368ba1d7d2cb2b0b803be24e54ffa07`. **Unity test unexecuted; no real rendered probe**. The official P08.02 remains Open.
