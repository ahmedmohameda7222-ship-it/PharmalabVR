# P07.02–P07.03 — dock ownership and tracked neutral recovery

**Date:** 2026-10-15  
**Start of turn:** `4c5ae3ce693d4a10ceae969b523df1a68dd01805`  
**Latest functional SHA:** `fc293fe93dfa39c6cc93b54ece011e6e71ae3919`  
**Branch/PR:** `feat/foundation-vr-first` / [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1), open and unmerged.  
**Gate/status:** No tasks newly VerifiedLocal/AcceptedPlanner; P06.02/P07.01/P07.02/P07.03 Open, G-R1 Open.

## Code increments

- `f7131709e975979dc1cab12eb6bcaec9dc9c2b5f` Unity Editor test source requires a real `BuretteDockAnchor`, `ToolMount` and lock/release state; no Unity run.
- `73b9e1f3a3dd851a91411853b63aab57ccc7ab4b` adds headless shared-panel ReleaseTool assertions. Actual CI RED C# compilation because `PanelAction.ReleaseTool` absent.
- `7acc36c26f84e6569638f4148c5a82a56b4d3829` adds physical ToolMount with pose lock, kinematic rigidbody and grab-disabled mounted state, explicit ReleaseTool via SessionController/LabPanelController/ScreenRouter/spatial panel, and VR-mode dock initialization. No Native chemistry is duplicated; `PlaceTool` already registers the physical research tool at Native bootstrap.
- `82d6b37c0b5f5ccfccb8dbf74050f931e8fb842d` adds pure recovery-policy tests; real CI RED missing new source file.
- `c485f8a88a2d5fcc0328e674e9811a306d01129e` wires `XRTrackingAdmission` into actual XRInputAdapter with tracked-head plus both tracked-hands admission. An unselected tool no longer yields `trackingValid=true` without controller/head tracking.
- `fc293fe93dfa39c6cc93b54ece011e6e71ae3919` avoids editor-time scene serialization of active mount ownership in ModeCoordinator.

## Verification and boundaries

- [P07.02 RED CI](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38700691867): headless failure is the intentional missing `PanelAction.ReleaseTool`. [GREEN push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38700901622), [GREEN PR](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38700907996) both success Windows/Ubuntu native + headless.
- [P07.03 RED CI](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38701111292): headless failed intentionally due to missing `XRTrackingAdmission.cs`.
- [Current code push CI](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38701397558): panel-routing job confirmed **63 headless assertions passing**, RED under two prior mutations (review bypass + unavailable Boot confirmation), GREEN after restoring exact source. Native Windows/Ubuntu compilation/test jobs were still running when recorded; latest job conclusions must be verified before an all-platform claim.

**Not validated at this source SHA:** Unity Editor compilation, EditorIntegration scene-generation tests, Unity PlayMode, Windows Desktop/WindowsVR/AndroidVR Player actions, official XR simulator, physical device, audio, interaction visuals, and mounted-tool physical correctness. Native tests prove only the unchanged Native code path; headless stubs do not verify device safety. This source slice is **partial implementation**, not task completion.

**Scientific constraints:** Native ABI alone creates/transfers fluids and observations. No model/compound/calibration changes. Docking is a local presentation/physics lock, not a new scientific `PlaceTool` command, and no volume is created on release.

**Next development:** Implement P07.03 recenter/reach/real neutral recovery and P03.01 burette outlet/parcel geometry, then distinct P03.02 pipette/bottle behavior and P03.03 geometry profiles. Keep real Unity execution and evidence `NOT_RUN` until the final campaign, per Product Owner directive.
