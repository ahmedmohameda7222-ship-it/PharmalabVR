# P07.03 — post-load mode admission and near-dock parking

**Date:** 2026-10-15. **Branch:** `feat/foundation-vr-first`; [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) remains open/unmerged. **Starting HEAD:** `80ba76ed28e466c1ed7c46b968dc94e73036e8ca`. **Functional SHA:** `6ad0cead8070eaffe5205340ba1cad5cf013510e` (evidence commit to follow). No task or release gate newly accepted.

## First increment — load must preserve Native mode and parked XR tool

- RED test source `dfd60c9459f574913c53f625ed7708ba689db9d2`: [push CI 38707742872](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38707742872) witnessed C# `CS2001`, missing `SessionLoadAdmission.cs`.
- GREEN source `eb658e95aee91d317aace92be11935cfe1b7e3fa`: pure `SessionLoadAdmission` checks current and imported Native mode equal, rejects unknown modes, and in VR requires a registered and parked tool. `CoreDriver.ReplaceSession` calls it on `replacement.ReadSnapshot()` **before** releasing the old Native handle, throwing without changing the active session on rejection. Native import already establishes `SessionLoad` Hold and requires new neutral tracked input.
- [Push CI 38707965182](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38707965182) and [PR CI 38707971817](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38707971817): **SUCCESS**, Windows 2022 Native, Ubuntu 24.04 Native, C# headless routing **95 assertions** and 3 previously established mutation RED→GREEN checks. This proves the pure admission policy and existing native suite, not Unity replacement.
- Unity PlayMode source `fb50b4726c2b195fac9efdc8f3c8ff9b079fc840` adds cross-mode rejection/native-handle-preservation and accepted Desktop `SessionLoad` hold + revision-preservation journeys. **NOT_RUN**; Unity Editor unavailable in this environment.

## Second increment — deliberate return to a real dock

- RED test `f060ea3971be209eb3c6e1251fb3228b9ac781ea`: [push CI 38708491028](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38708491028) / PR 38708498879 witnessed `CS0117`, absent `PanelAction.ParkTool`.
- GREEN source `6ad0cead8070eaffe5205340ba1cad5cf013510e`: `ToolMount.TryMount` refuses grabs in progress, duplicate mounts and tool world poses farther than **0.12 m** from its configured dock. `ScreenRouter` exposes ParkTool in supported VR Lab/Pause/Hold screens; `LabPanelController` routes through `SessionController.ParkToolAtDock`, which accepts actual `TryMount()` and refreshes the screen only when successful. The VR panel exposes Park separately from Release; its Save, Review, Pause and recenter actions remain distinct. Parking is local presentation/physics only: it does **not** submit Native PlaceTool, change mass pools, Save, change event sequence, or auto Continue.
- [GREEN headless push 38708832562](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38708832562): observed `panel-routing` job success with **102 source-level assertions**, Review/Boot/Recenter mutations intentionally RED then exact source restored GREEN. Native Windows/Ubuntu CI pending at observation; do not treat headless as Unity.
- Unity generated-scene EditMode source `3fbbf1219f09d1cdfd5c219712c67d4eb71ac101` tests far tool refused without displacement or physics side effect, near unselected tool accepted and locked. **NOT_RUN**.

## Scientific/runtime status

No C++17 chemistry/core/solver implementation changed. `CoreDriver.ReplaceSession` preserves Native ownership on denied import and defers Native lifecycle/revision facts to the pinned Native Core. Fresh tracking and neutral baselines remain Native-gated. No verified Unity Editor/PlayMode/Player/simulator/headset screenshots or journeys at new SHAs.

**Unresolved:** user-facing VR Load action; complete hold/recenter/load/regrab journey, safe actual ray/trigger dispatch, reach tray, 0.12 m reach usability, X03/X04/H02/H03 physical/simulator acceptance, material-pool conservation across all P03 stocks, full OpenXR build. P07.03 stays **Open / NotReviewed**; G-R1 Open. Official totals unchanged, **15 VerifiedLocal / 51 Open**; deferred Unity register **66 NOT_RUN**.

**Next bounded code:** VR Load action via explicit saved-session command with admission, then P03.01 in-flight burette outlet/parcel visuals and P03.02 distinct pipette/bottle native-safe behavior. Do not merge PR #1.
