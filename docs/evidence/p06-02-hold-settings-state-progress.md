# P06.02 — Native Hold/Settings Back route safety (2026-10-16)

**Branch:** `feat/foundation-vr-first`. **Production functional SHA:** `467477a85f9670226291bd3bfbce0a968662f786`. PR #1 still Open/unmerged. P06.02 remains Open/NotReviewed; G-R1 Open. This is a source-level routing correction and does **not** claim Unity/Player/XR hardware evidence.

## Failure reproduced with the production C# routing path

The real `ScreenRouter.ReturnFromSettings` used `beforeSettings` to restore the screen that had been visible when Settings opened. If the user entered Settings while the Native session was held and recovery completed while Settings remained visible, Back resurrected the stale Hold state even though Native `IsTimeHeld=false`. The first headless regression `9818e55` [push 38816469130](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38816469130) failed as `Resolved Native Hold was resurrected by Settings Back`.

The first source candidate `05f85ee` addressed that case but exposed an invalid test path (direct Lab→Settings is not a supported panel action). The regression was rewritten to use the actual allowed `Lab→Pause→Settings`, then asynchronous Native Hold opening, Settings while held and recovery. That valid `c7e199` test [push 38816737046](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38816737046) failed as `Settings interrupted by recovered Hold lost the underlying Native Pause`.

## Production correction and partial verification

The final `ScreenRouter` `467477a85f9670226291bd3bfbce0a968662f786` captures the original Settings return destination if Native Hold interrupts Settings; Back stays in Hold only if Core is still held. After recovery it returns to the prior valid pre-Hold destination (Pause for a paused Core; Lab if the interruption was from Lab), never to obsolete Hold or Settings. The regression also checks no automatic Save, Pause or Continue is issued by these navigation transitions.

**Verified:** Pure production shared C# compiled and the complete headless `panel-routing` test step succeeded in [push 38816878655](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38816878655). Native Windows/Ubuntu jobs at that SHA still pending at creation of this document. Explicit headless tests are not Unity Editor, PlayMode, Desktop Player or XR controller tests.

**Deferred:** in pinned Unity 6000.3.25f1, execute the asynchronous real Native Hold/Settings navigation through Desktop and XR world-panel actions (not direct router calls alone), including Pause/Recovery, Review, Save, actual Native eventSequence and hold reason. Capture screenshots, Test Runner XML, Player/native logs and real tracked-ray hand actions. Do not promote P06.02 or the 66 per-task Unity verification rows until physical evidence exists.
