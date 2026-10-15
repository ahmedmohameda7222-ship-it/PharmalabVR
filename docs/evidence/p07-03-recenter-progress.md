# P07.03 — guarded XR view recenter source checkpoint

**Date:** 2026-10-15 | **Branch:** `feat/foundation-vr-first` | [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) remains unmerged

| Reference | SHA |
|---|---|
| Observed starting HEAD | `7337dda08c09ad7c031090bb1be52b021a498cbd` |
| RED headless tests authored | `bdd6e5ecd80832554099a4d8abcf4e9ce54e746b` |
| Initial functional commit | `f7fd8a642c545f949edb3ff528dc3f2baf977d0c` |
| Compatibility-corrected functional code | `475a21ca6ac68d1e707e80b2ded1c656b5dbba09` |
| Deferred Unity scene-geometry test | `f9cc38c525a532012d71dee5c302af82900b73bd` |
| Mutation regression / evidence input SHA | `d08b8d7c85fb4e99a97e4e372c3eb20e6d7e3355` |

## Scope and scientific boundary

Added `RecoveryAlignmentPolicy.cs` as pure runtime admission: VR mode, Native Pause or time Hold, tracked head and parked/docked tool are ALL necessary. `ScreenRouter` surfaces explicit Recenter on VR Pause/Hold; `LabPanelController` routes it into `SessionController.TryRecenterView`. That method rechecks actual CoreDriver state, XR head tracking and `ToolMount.IsMounted` before delegating to `RecoveryAlignment` attached to the generated Lab XR rig. Camera XZ is placed approximately 0.78 m horizontally from the docked instrument; tracked eye height, lab instrument world transform and transform scales stay unchanged. The method does not call `CoreSession.Submit`, `Continue`, `Save` or any scientific recomputation. Native Core alone judges fresh neutral baselines after recenter.

**Limits:** The physical reach distance is a research comfort starting value, not a device-calibrated result. No 30° snap turn, recovery after session load, reach tray or body-to-world acceptance yet. Active/undocked tool is denied rather than force-dropped. Existing source has no Unity-generated scene/model compilation evidence at this SHA.

## RED/GREEN evidence

- [RED push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38703751397): CI `panel-routing` C# compilation failed **CS2001**, missing exactly `RecoveryAlignmentPolicy.cs` before implementation.
- [First source GREEN push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38703987685): `panel-routing` job successful, 79 headless assertions. Older Review and Boot intentional mutations each RED exit 134, restored GREEN.
- [Mutation checkpoint push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38704367715): observed panel-routing job exit success, 79 headless routing assertions; intentional false-success Recenter mutation **RED exit 134**, along with Review/Boot mutations RED, restoring exact source **GREEN**. Native Windows/Ubuntu jobs still in progress at last observation. These test adapters do NOT execute Unity, a real Native session, or tracked XR input.
- Generated-scene EditMode test `P07_03_RecenterIsHorizontalAndNeverScalesCalibratedTool` is **NOT_RUN**, written to test prefab/rig wiring, horizontal translation, floor-Y and instrument dimension conservation. Neither the Unity editor nor a licensed Unity runtime is available in this Classic session.
- Native Windows/Ubuntu at earlier source SHA `7337dda08c09ad7c031090bb1be52b021a498cbd` passed; current-code Native statuses require per-SHA confirmation. No Player builds/screenshots or physical XR results fabricated.

## Next continuation

Keep P07.03 Open and official P06.02 nextTask per existing ledger. Next development-first code increment should implement explicit, bounded snap turn and parked-tool recovery after load, then P03.01 final drop and P03.02 distinct stock/pipette models without inventing science. Leave all 66 task Unity checks at `NOT_RUN` until the coordinated Unity 6000.3.25f1 Windows/XR campaign. **15 VerifiedLocal / 51 Open, G-R1 Open, no Planner acceptance, no PR merge.**
