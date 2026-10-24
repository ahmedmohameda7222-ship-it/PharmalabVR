# PR #2 M3 — Independent Native study: source integration and local Unity acceptance

**Status:** SOURCE INTEGRATED; **Unity Editor/PlayMode/Windows Player/VR/Android XR NOT_RUN**. The owner-approved baseline remains 66 tasks: 15 VerifiedLocal / 51 Open. G-R1/G-U1 OPEN; PR #2 Draft and unmerged.

## End-to-end behavior to verify

1. In Unity **6000.3.25f1**, generate assets, open the native-driven Lab, select Desktop or tracked XR. The scene is a real Unity build, not the headless panel harness. Prepare an authenticated supported acid source (for example HCl 0.05 mol/L), perform a deliberate physical Native bottle/pipette action and confirm committed receiver volume in the Native snapshot. Record the real native receiver pH only when Supported/Current/Ready.
2. In Notebook write an explicit learner note tied to the exact Native branch/event. Pause and activate **Save current + start new Native research run** (Desktop) or XR separated offline recovery page **New Native run**, with VR burette parked.
3. Confirm first `.plv.json` and `.plv-notes` are durable; confirm active Native branch changes to `study-<UUID>` and active local save stem to `run-<UUID>`. Fresh source/receiver/burette must be empty, new Notebook empty, recorded branch/event different from prior experiment, and **Native SessionLoad Hold** active. No old input/physical grip can transfer automatically.
4. Restore **fresh neutral** tracked Desktop or XR baseline and explicitly Continue. The Native `ResumeSetup` command must be Accepted and release Hold **without** advancing chemistry, removing Native Pause, or closing stock preparation (`setupClosed=false`). Source-only UI returns to the legitimate Setup/Lab stock chooser. Hold recovery without fresh neutral must refuse.
5. Select a genuinely new supported stock, let its actual Native `PrepareStock` and subsequent `Continue` commands close setup, then make a new physical `TransferFixed` into receiver. Verify Native receipts, source debit + receiver credit and no fabricated display/measurement.
6. Record student hypothesis/valid current Native pH/note in this new branch; Save, Review, use the **first saved Native study already pinned as the recorded comparison baseline** after the deliberate New Native Run action, Compare. The student can still explicitly browse/choose another local saved baseline. The lineage must say **different Native branches**, not call two saved copies independent experiments. The independent immutable comparison CSV uses `schemaVersion=2`, 24 fields, real baseline file stem, exact model/provider/event identity and strict current/matching-provenance pH delta availability.
7. Pause, load the earlier Native run and confirm original branch/event/Notebook reappears under genuine Native SessionLoad Hold. Load back the second run; check its distinct Notebook, vessel amounts, Review and saved records. Never silently inherit another branch's notes or input.
9. From a **real Native Hold** (FocusLoss/SessionLoad), enter recorded-only Review. On Desktop, use **Return to Native Hold / recovery**; on XR use the **Return to Hold** world-space button. Both must close the read-only Review and return to the identical unreleased Native Hold, with unchanged command receipts, material, event and Notebook. The ordinary Return to lab control must be absent/denied until native recovery actually completes. Reopen Review and repeat; after the return, a fresh-neutral Continue remains an independent deliberate Native action.

8. Repeat on actual XR controller/grip/bottle and burette dock. Confirm disjoint ray-hit regions for Save Same Run/New Native Run, real physical manipulation, no tool input while Review/Notebook, and physical neutral rearm after Hold. Capture screenshots and exact device/build commit.

## Negative admission / disk / scientific conditions

- Before an old study is saved, corrupt its local Notebook journal and attempt New Native Run: must **refuse**, preserve currently active operating Native handle, branch/event and confirmed old save. Never quietly repair or discard learner notes. The PlayMode regression `M3_CorruptJournalBlocksNewNativeRunWithoutRetiringOriginal` is authored, **NOT_RUN**.
- Refuse NewStudy outside Pause, in Hold, in live Review, and when VR burette undocked. If fresh save/Notebook staging fails, **do not call Native import**; old live context must remain operable.
- The Native ABI allows only restricted `stagedResearchBootstrap` operations `CreateVessel`, `CreateSink`, `PlaceTool` and `Pause` until validated Import. Reject a second stage, simulation stepping, actuator/input batches, timed material transfer, checkpoint export and checkpoint configuration. Original operating Native may continue issuing native commands during the stage; it is retired only by the final Import handover.
- Native `ResumeSetup` refuses non-SessionLoad holds, prepared/closed setup, non-neutral tracking or premature recovery. After a successful Native `PrepareStock`/Continue, it never reopens scientific setup. Only Native commits chemical material.
- Missing/mismatched pinned IPhreeqc/MINTEQ package or malformed saved Native JSON must refuse live scientific import; recorded-only Review remains separately labeled, not a certified result.

## Reproducible local campaign (owner Windows Unity installation)

Run commands from repository root with Unity Editor path corresponding to `6000.3.25f1`:

```powershell
pwsh -File scripts/test-unity.ps1 -UnityEditor "C:\Path\To\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" -Platform EditMode
pwsh -File scripts/test-unity.ps1 -UnityEditor "C:\Path\To\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" -Platform PlayMode
```

The script regenerates source-backed Unity assets before tests; collect `artifacts/test-results/{GenerateAssets.log,EditMode.xml,EditMode.log,PlayMode.xml,PlayMode.log}`, any Player build logs, screenshots, actual Native event/command receipts, captured Unity Console, firmware/device IDs and exact commit SHA. A .NET/Node/native workflow, source contract, fixture, or headless test does **not** substitute for Unity, real Player, hardware or pilot acceptance.

## Source references and verification separation

- Unity: `Runtime/Core/{CoreDriver,CoreSession}.cs`, `Runtime/Session/SessionController.cs`, `Runtime/UI/{LabPanelController,ScreenRouter,SpatialPanelPresenter}.cs`, `Tests/PlayMode/NativeRuntimeSmokeTests.cs` (relative to `unity/Assets/PharmaLabVR`).
- Native: `native/core/src/abi.cpp` and `native/tests/test_abi.cpp`; `tools/panel-contract/Program.cs` and stubs provide headless action guards.
- Original first-slice commit `a81cbf8`: Native PR run `40064085102` passed panel-routing + Windows/Ubuntu native. Further Native staged handover, `ResumeSetup`, real second-run transfer and corrupted-journal regressions are newly authored source and must be checked on their latest exact SHAs. Unity/Player/XR checks remain **NOT_RUN**, not Passed.

**Do not merge PR #2 or promote formal task statuses from this source handoff.**

## 24 October 2026 — two-run comparison and closed-setup guards

- A successful **New Native Run** captures the just-saved first study's recorded Native branch and event, validates its recorded file/preview, and pins the first file as the default **read-only** comparison baseline *after* `NativeSessionRestored` clears previous UI authority. Desktop Compare displays that exact local filename and explicitly labels it non-attested; XR Compare already names the baseline. Students can still choose another local Native save, but selecting/previewing never imports it or claims valid pH.
- The authored PlayMode test `M3_SpatialOfflinePageStartsSecondStudyAndPinsFirstRecordedBaseline` presses the actual XR recovery-page button, verifies disjoint SaveCopy/NewRun hit areas, Native Hold, distinct branch, first file label and Notebook separation. `M3_IndependentNativeRunPreservesPreviousStudy` uses **actual Load selected Native save** to restore experiment 1, then chooses and restores experiment 2 again, checking both Native event/Notebook identities. Both remain **Unity NOT_RUN**.
- Native `setupClosed` is now observed by `CoreDriver.SourceNeedsPreparation`. An empty revision-zero source in an already closed experiment never reopens the stock chooser or wrongly dispatches `ResumeSetup`. The authored `M3_ClosedNativeSetupWithEmptyStockCannotReopenPreparation` PlayMode test exercises explicit Native Continue→Pause with no stock. **Unity NOT_RUN**; Native `ResumeSetup` guards remain authoritative.
- Exact TDD: [RED baseline selection `7a4d6a8`](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/40077083378) panel-routing failed the selected-prior-run assertion; production `f59637b` + source assertion adjustment `02d200e` made [headless + native CI `40077316807`](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/40077316807) succeed on panel-routing/Windows/Ubuntu. [RED setupClosed `58ad777`](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/40077400886) failed specifically for offering Native preparation on closed setup; GREEN `85cc4b6` [run `40077511428`](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/40077511428) succeeded for panel-routing/Windows/Ubuntu. Final PlayMode source updated at `dafac3e`, separately checked under its own workflow; do not infer a Unity pass.

## 24 October 2026 — inescapable held Review regression

- **P06.02/P07.03/P11.03 Partial source:** Native Hold allows recorded-only Review without touching current chemistry. Previously that Review had no authorized path back to Hold, creating a navigation dead-end and hiding recovery. `PanelAction.Hold` is now a deliberately guarded *presentation-only* return permitted from Review only while Native still holds; it closes cached Review, never calls Native Continue/Pause/Save, and reopens the original recovery screen. The Desktop Review and actual world-space XR Review panel now expose separate **Return to Native Hold / recovery** actions; normal unheld Review still uses Return to lab. Native held Review cannot silently call Lab.
- **TDD:** RED `9675f05` [run 40078017614](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/40078017614) failed on missing Review `Hold` action, as intended. Implementation `cc06bdc` added shared routing and both interfaces plus real Native FocusLoss Unity PlayMode regression `M3_NativeHeldReviewReturnsToRecoveryWithoutChangingScience` (**NOT_RUN**). The first GREEN attempt exposed an incorrectly fixed historical test counter; `b1ccf11` now checks before/after counters of the same operation. Panel-routing CI on [run 40078235347](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/40078235347) completed **SUCCESS**; Windows/Ubuntu jobs are separately pending as of this evidence entry.
