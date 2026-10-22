# P06.02 / P07.01 / P07.02 — XR Boot and interaction admission, development-first

**Date:** 2026-10-15  
**Starting HEAD:** `4c5ae3ce693d4a10ceae969b523df1a68dd01805`  
**Functional source SHA:** `da403bf89e3bcc0c78c79ece07fae9227f70704d` (evidence-only commit follows)  
**PR:** [#1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) open/unmerged  
**Planner statuses:** P06.02, P07.01, P07.02 Open/NotReviewed; G-R1 Open.

## Actual source changes

- Test-first C# `af1e0f202799c7e9f1f5adaec9d0646f76dcda92` specified pure `BootLaunchAvailability` admission; CI compiled RED for missing production type, then `37304cd3a61b26b4ef62f3f7649cfb13228319d0` added the production launch policy and `BootMenu.ConfirmSetup()` true/false result. `dfa74dbb605bcddd60fc2583263eaaabe3dc5e9f` corrected the test import.
- Unity EditMode source test `75da17c5ae28db55a1e1ded5e63bf30575436cc7` requires Boot scene XR origin, two rays, world Canvas, tracked-device raycaster, one EventSystem, physical buttons. Production `fde822df625b7953bdb5001389dd3619849d5a0f` adds serialized XR Boot rig and UI; Android starts VR Setup while PC has an honest Desktop fallback. No actual Unity compile, tracked ray press or headset result has been witnessed.
- Native/audio presentation regression test source `8ef48fa9b4ec7690bc5782d4c65b06cb3f38ec06`, fix `8c1b55de9c10b0ae9fd744fed8fb58b6d691d61a` supplies exactly one enabled camera AudioListener per initial Boot/Desktop Lab mode and moves world panel on changed hand preference; Unity test pending.
- P07.02 test `e5ea316bcf63f35b35b16a6b94b57a3c278a3ae8` (EditMode deferred) requires independent receiver XR grab + nonthrowable liquid instruments. Production `d13488b7f8afb88f48fe415b25b9011a03aa57d1` creates receiver Rigidbody/XRGrabInteractable and disables throw inheritance on both receiver and burette/ResearchBurette prefab.
- P07.02 headless policy test `63cfa58b45fb53afd17afcce53f5b363c0ffe8f3` produced RED missing `InteractionOwnership.cs`; production `da403bf89e3bcc0c78c79ece07fae9227f70704d` permits valve actuation only for **direct tracked body grip** with **no UI ray selection**. The Native Core still owns liquid commands and material balances.
- `fbde2fb41679b874894637dc7418b6eee4858cfb` adds a second mutation test ensuring Boot action denial cannot report success. `tools/panel-contract/check_regression.py` reverts temporary source mutations exactly before GREEN.

## Verified / pending

- [RED Boot policy run](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38697693752): panel-routing C# compile missing `BootLaunchAvailability.cs` as intended; not a runtime Unity test.
- [GREEN 47-headless-assertion run](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38698650774): actual `ScreenRouter`/`LabPanelController` and pure launch policy source compiled with limited test adapters; Windows/Ubuntu native CI separately ran on the SHA.
- [RED grip policy run](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38699691828): C# compile failed on purpose for missing `InteractionOwnership.cs` production file.
- [GREEN headless P07.02 run](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38699826367): headless panel-routing job **success**, **54 routing assertions**, two intentionally reverted mutation RED exits 134 and restored GREEN. The two platform native jobs may still be in progress; inspect exact job conclusions before claiming all-platform success.
- All new **Unity EditMode/PlayMode** tests and DesktopPlayer/WindowsVR/AndroidVR tracked-ray actions **NOT_RUN**. Real world panel placement, XRI press, event-system dispatch and visible renderer correctness cannot be inferred from headless checks.

**Next implementation:** P07.02 real mount/release + independent receiver geometry and XR action ownership; then P07.03 comfort/recovery and P03 laboratory tool workflows. Keep final Unity campaign deferred and update 66-row register at each code SHA.
