# P03.01 — Native receipt-driven pending-drop presentation

**Date:** 2026-10-15 | **Branch:** `feat/foundation-vr-first`; [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) open/unmerged.

| Checkpoint | SHA |
|---|---|
| Start of this visual subincrement | `5982830ec034e62130db5908c67ccf9c8e589803` |
| Initial missing-type RED | `870a2065bba7052150a5197254a6085948d5d65d` |
| Functional C# source | `0e00684b365d116ff267144b46ac85e902662776` |
| Real stale-replay mutation test | `8246e2b0de22fc36da42e6399fa6a9841a1525af` |
| Prior Native parcel import GREEN | `5d7c12dfdabc7927c2f2eaa125a236ea773e3660` |

## Product change

- `Runtime/Tools/DropParcelState.cs` is a pure state of the **committed** Native in-flight parcel, not a material ledger: positive finite volume and nonempty ID show a pending drop; matching `InFlightLanded` ID clears it exactly once; an unrelated or duplicate receipt cannot clear another parcel, and an old snapshot cannot resurrect the last landed parcel. Freshly emitted IDs still work.
- `Runtime/Tools/DropPresenter.cs` consumes the actual native snapshot and event JSON, updates `ActiveParcelId`, `CommittedVolumeM3` and the visual object's active state. No fabricated drop count or local chemistry.
- `Runtime/Core/NativeLabVisuals.cs` subscribes to `CoreDriver.NativeEventReceived` for the same source `DropPresenter`; unsubscribes event and snapshot handlers on reconfiguration/destruction. The sequence is Native commits landing, publishes receipt, then updates snapshot. The renderer never initiates material transfers.

## Verification and boundaries

- [RED headless push 38713548193](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38713548193): the production class was intentionally absent; `CS2001` surfaced the missing file before implementation.
- [Functional headless push 38713791890](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38713791890) / [PR 38713798458](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38713798458): panel-routing job **SUCCESS**, **122 assertions** against the actual compiled C# policy, plus previous 4 routing-mutation REDs and exact-source GREEN.
- [Mutation push 38714105993](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38714105993): temporarily bypassing stale-replay suppression **FAILED intentionally with named diagnostic** (exit 134); all five intentional mutations RED, exact source restored byte-for-byte, headless 122 assertions GREEN. Native Windows/Ubuntu jobs at latest SHA require separate verification; these source tests do not execute Unity.
- Previous [Native push 38712685535](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38712685535) and [Native PR 38712694119](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38712694119) both **SUCCESS on Windows and Ubuntu**; real C++ product ABI blocks fabricated/anomalous in-flight parcel IDs at import.
- **NOT_RUN** Unity 6000.3.25f1 EditMode/PlayMode, Desktop Player, WindowsVR/AndroidVR Player, official XR simulator, physical headset, rendered visual or user-action acceptance. Prior old-SHA Unity smoke tests do not verify this new code.

**Status/ruling:** Scope is source-only P03.01, dependency P07.03 Open. Keep P03.01 `Open`, Planner `NotReviewed`, G-R1 `Open`, and 15 VerifiedLocal/51 Open; all 66 deferred Unity task register rows remain NOT_RUN. Next P03.02 separate bottle/pipette research device profiles and verified material/residual operations; do not equate view-state policy checks with real final-drop animation or hardware.

## Follow-up — deliberate Native restore epoch can revive an earlier parcel ID

- **RED:** `945e6974246155f9388dfb85504a2554b4dc440e`, [CI 38714692146](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38714692146). Headless C# compilation failed on missing `DropParcelState.ResetSession` before implementation (`CS1061`).
- **GREEN functional source:** `aa0f42259212cbc1dca84e884997d24a090c2808`. `DropPresenter` reads actual Native snapshot `hold.reason` and resets last-landed identity only during `SessionLoad` or `CheckpointRestart` replay epochs. `DropParcelState.ResetSession` makes saved pre-landing parcel visible again, even if it landed in the preceding live session. The normal stale same-session parcel suppression remains intact and matching landing receipts still own the visual transition.
- [GREEN CI push 38714853023](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38714853023): **124 headless assertions pass**, five mutations deliberately RED then exact-original-source GREEN, including stale-drop bypass. Native platform CI and PR job conclusions at latest SHA must be checked separately; no Unity renderer is exercised here.
- **Next required acceptance:** Native real Save/Checkpoint of outstanding drop → land → Load/Restart pre-landing → Native SessionLoad/CheckpointRestart hold → real visible same-ID pending drop → fresh neutral continue → valid one-time landing into receiver or spill. Unity and Player/XR remain **NOT_RUN**, P03.01 Open/NotReviewed, 15/51 and G-R1 Open unchanged.

## Restore epoch cannot reset repeatedly during a held Native session

| Reference | SHA |
|---|---|
| Test-first RED | `98d79cec3aeefc7dec3c2e5724d3686bfd6a82fc` |
| Functional code | `15a067e17ee00995e914442a15f0c8c76c77e83a` |
| Negative mutation test | `666b9188897e418273b464f54e45e2389c403ca9` |

- [RED CI push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38716087729): C# `CS1061`, real production `DropParcelState.ApplyNativeSnapshot` missing before implementation.
- [GREEN source push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38716320883) / [GREEN PR](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38716327900): `panel-routing` passed **136 headless C# assertions**. This verifies the pure production state model and unrelated existing panel routes, not Unity renderer code or a real Native session import.
- [Mutation push](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38716458968): the explicit `reason != lastHoldReason` guard was temporarily disabled on CI runner. Named repeated-held-snapshot diagnostic failed **exit 134**; after restoring exact source, all 136 assertions passed. Five earlier mutations also RED then exact-source GREEN. Native Windows/Ubuntu status separately pending at checkpoint creation.
- `DropParcelState.ApplyNativeSnapshot` recognizes `SessionLoad` or `CheckpointRestart` **only on hold transition**, instead of resetting on each repeated held snapshot. `CoreDriver.NativeSessionRestored` explicitly signals only accepted `RestartCheckpoint` and `ReplaceSession` before snapshot publication, allowing a saved parcel to reappear if a second import occurs while the same reason is still held. `NativeLabVisuals` subscribes/unsubscribes `DropPresenter.ResetSession`. No Native material, transport or science equations changed.
- Unity 6000.3.25f1 assembly/scene, PlayMode real Native Save→Land→Load/Restart→Land, Desktop/VR Player, tracked XR rays and device acceptance remain **NOT_RUN**. Formal P03.01 and dependency P07.03 **Open**, G-R1 Open, 15 VerifiedLocal/51 Open. No new AcceptedPlanner.
