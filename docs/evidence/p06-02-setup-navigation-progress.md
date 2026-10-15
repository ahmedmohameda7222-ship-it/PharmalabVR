# P06.02 — Setup preferences and panel navigation, bounded continuation

**Date:** 2026-10-15  
**Branch:** `feat/foundation-vr-first` / [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) remains unmerged  
**Observed start SHA:** `1485f145d3cd1fd7a7f42593963d884abeb072e7`  
**Functional code SHA:** `4a184c4eca61458027f3274d87fd88cdf7a97243`  
**Headless CI test SHA:** `37440f128950f5f97614b02148a5e596d433a49f`  
**Task:** `P06.02 Open`; **G-R1 Open**. No new `VerifiedLocal` or `AcceptedPlanner`.

## Plan / scope ruling
The approved P06 task depends on P06.01 and requires genuine input-driven Boot, Setup, Lab, Inspect, Measure, Pause, Hold and Review with native authority. The user explicitly selected P06.02 next. Work only on the existing feature branch. In this environment the live connected GitHub repository is accessible, but no Unity Editor / C# compiler exists in the local container and no checked-out Git worktree is provided; `git rev-parse --show-toplevel` failed outside a repository. Work therefore uses GitHub source commits and a new CI headless C# test harness, with **no fictional Unity/Player evidence**.

## Changes already committed
- `1fe1f93b91bf52d84aaee6b5733bd9cc280400e3`: authored two Unity PlayMode tests before preference production code, requiring Setup values to survive Home/Setup/scene navigation and VR panel handedness/text scale.
- `87fd90ac488e8dfc48bbe0c68a9d6883c7f1804b`: `LabSetupPreferences` immutable bounded presentation selection (posture, dominant hand, bench viewing height 0.65–1.05 m, text scale 0.85–1.35, captions, reduced motion, contrast, volume); shared selectable Boot Setup controls. Session-scoped selection survives Boot scene unload and is included in `PanelState`. Desktop camera position/audio and VR panel position/font/contrast consume the selection. **Physical reach, lab apparatus geometry, comfort and calibration are not validated; bench setting here is viewing-frame offset only.**
- `7406593e69699b91cbfe78e313065300edf033c4`: authored two more Unity PlayMode tests before navigation production code, covering Pause→Review→Lab native Continue and Pause→Settings→Back without resuming the authority.
- `b92071c348ee4f4b682ddf59427fdb8aba0a7218`: Review return uses shared `LabPanelController` instead of bypassing native held-session restrictions; return to Lab resumes native Pause from Review as well as direct Pause; Settings Back returns to previous screen without unpausing. Reachable Desktop Measure/Review/Settings controls and spatial Settings/Back callbacks. Responsive Desktop HUD bounds computed from viewport.
- `4a184c4eca61458027f3274d87fd88cdf7a97243`: selected text scale applied to actual Desktop IMGUI controls, with GUI style restored afterward.
- `2449a88f3e8bfb482623d336694940aa98a2a8cd`: new headless `net8.0` CI harness compiles the **actual** production `ScreenRouter.cs` and `LabPanelController.cs` against narrowly described fake scene/native drivers and tests their state transitions; these fakes never claim chemistry or XR acceptance.
- `37440f128950f5f97614b02148a5e596d433a49f`: adds a reversible mutation check that temporarily reinserts the original Review-return bypass in the ephemeral CI worktree, then restores exact source bytes.

## Verified results and limitations
**Headless source-level behavior:** [push workflow 38681309492](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38681309492) and [PR workflow 38681313440](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38681313440). The `panel-routing` runner executes:
```sh
dotnet run --project tools/panel-contract/PanelContract.csproj --configuration Release
python tools/panel-contract/check_regression.py
```
**Observed push runner:** 39/39 headless assertions green, mutation RED with test-process exit **134** and exact expected `Review return failed to resume the paused authority` diagnosis; byte-for-byte restoration GREEN, 39/39 headless assertions. Job exit **0**. This is actual C# routing logic with test adapters, **not Unity**. Native CI for Windows/Ubuntu on the functional source commit was also observed successful (push 38680698634, PR 38680703683). The native suite runs `cmake -S . -B build/native -DBUILD_TESTING=ON -DCMAKE_BUILD_TYPE=Release`, `cmake --build build/native --config Release --parallel 2`, `ctest --test-dir build/native -C Release --output-on-failure`, ideal fixtures, and repository unit tests. Native success does **not** compile Unity.

**Source-only check:** seven changed Unity source/test files were re-fetched at `4a184c4`; 30 expected wiring tokens and brace count zero-delta were inspected. Viewport bounding algebra at 640×400 -> 370×376 HUD; 1280×720 -> 370×620. These are not rendered pixel results.

**Not run / not demonstrated:** four new Unity PlayMode cases are authored but have **no witnessed Unity RED or GREEN**. EditMode, PlayMode, Player builds, actual IMGUI mouse events, two distinct window screenshots, XR ray-to-EventSystem trigger selection, official simulator, headset, Arabic visual shaping and end-to-end Native-backed journey are unverified at the source SHA. No device or experimental data was generated.

## Review / critic / alternatives
The code does not add chemistry or bypass the Native ABI; recorded Review still uses explicit `OpenReview` without implicit Save. A paused Review return is now guarded before changing visual screen. Headless stubs cannot establish its real IPhreeqc/session correctness; the Unity tests must run against the existing native adapter. Reduc​ed motion, full audio/caption pipelines, VR Settings editing parity, physically calibrated bench/reach alignment and detailed 720p/1080p Arabic rendering are still incomplete. Existing Desktop IMGUI remains a transitional panel, not full premium shared UI parity.

**Next exact task:** Continue `P06.02` on a Unity 6000.3.25f1 Windows workspace: compile, fix any Unity errors, execute EditMode and PlayMode including the four new tests, then demonstrate actual Boot→Setup→Lab→Inspect→Measure→Pause→Review Desktop interactions with native snapshots and save isolation at 640×400/720p/1080p. Add official simulator tracked-ray trigger-to-EventSystem action evidence, rule out duplicate UI/tool input, and keep `P06.02` Open until its full acceptance. Preserve 15 `VerifiedLocal` / 51 `Open`, `plannerStatus=NotReviewed`, G-R1 Open, PR #1 unmerged.
