# P06.02 continuation — Boot/Setup routing and one XR manager

Functional code: `614097bc4a5761badeb32aef69a9794066c713fe`. Local Windows, Unity 6000.3.25f1. **P06.02 remains Open** and G-R1 remains Open.

## Red, change, green

- A PlayMode compile regression required `EnterDesktop`, `EnterVR` and `Home` panel actions. `BootMenu` now uses the same `ScreenRouter` and `LabPanelController` as the Lab scene. Home mode choice enters Setup, Back returns Home, and Confirm loads the native-backed Lab. An actual Boot scene test checks both mode choices and the Setup action list; a second test confirms Desktop Setup loads a real Lab scene with a live `CoreSession` and the selected mode. The Setup panel is presently a short prompt, not the complete posture, hand, reach, recenter, language and accessibility setup from the UI contract.
- A Windows Player selected VR from Home and entered Lab, but its log warned about two active `XRInteractionManager` components. A new PlayMode test reproduced two managers. The scene and scene generator now put one manager under the active Lab root and explicitly bind the grab body plus both direct/ray interactors to it. The green PlayMode test counted one active manager, while EditMode checked all five serialized references. A fresh Windows Player Boot→Setup(VR)→Lab log has no duplicate-manager warning after scene load. This is software registration evidence, not controller or headset interaction evidence.

## Verification and limits

At the functional SHA: Native Release CTest 1/1; Unity PlayMode 31/31; EditMode 22/22; fresh WindowsDesktop, WindowsVR and AndroidVR builds each reported `Build Finished, Result: Success`. Raw XML, logs, three actual Player screenshots and binary SHA-256 values are in `artifact-manifest-p06-02-boot-xr.json`. The actual VR-mode mirror shows a bare room with **no world-space panel**. The current Home/Setup presenter is Desktop IMGUI; Setup choices, tracked-ray UI, full Review, Player Inspect, complete student journey and physical VR/Android execution remain open. The earlier `p06-02-progress.md` and its manifest preserve the preceding `dd60197` evidence and the unsuccessful Inspect click. The historical 95-item acceptance catalog remains 18 Passed, 73 Partial, 4 EvidenceMissing.
