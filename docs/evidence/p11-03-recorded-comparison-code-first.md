# P09.02 / P11.03 — Recorded pH truth and two-run comparison (2026-10-16)

**Status:** bounded code-first increment; **P09.02 and P11.03 Open**. No Unity Editor/Player/XR run at these SHAs. PR #1 is open, unmerged; product is permanently English-only.

## Recorded scientific truth (P09.02)

- RED `2f97d1ecaa166bfb4b96a31e427d9d7b1885ed2e`: headless linked-production regression reported **"Recorded-only Review showed stale Native pH as a current number"** in push [38862918814](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38862918814).
- GREEN `44bda21d803fdef087ed8f4a0958d78b63751836`: `RecordedSessionReview.RecordedObservation.DisplayValue` now requires Native **Supported / Ready / Current / valueAvailable / finite** simultaneously. Existing failed/missing scientific states remain hidden. GitHub push [38863039090](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863039090) and PR [38863044223](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863044223) all three headless/Native jobs SUCCESS. Unity EditMode regression authored, NOT_RUN.

## Recorded two-run comparison (P11.03)

- RED `478b73fdf50991315ec936c9d598f46616603f1e`: actual production-linked .NET contract compiles a required `RecordedRunComparison` source file before it exists and fails in push [38863229291](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863229291).
- GREEN `eae473c9ebb1374776ac17ad6ef749a54302ddbd`: `RecordedRunComparison.Compare` joins recorded Native observations by vessel+observable, preserves availability (Current/Stale/Failed/Pending/Unsupported/Absent), rejects duplicate Native observation keys, and computes a signed numeric **candidate minus saved baseline** only if both pH values are Current/Ready/finite and scientific package, provider model and database identity match. Otherwise it returns unavailable delta and explicit reason; it never infers missing data, interpolates events or invokes a solver. [Push 38863465109](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863465109) and [PR 38863471270](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863471270) SUCCESS all three headless/Native jobs.
- RED `dc23cba8796c456db324b122e1bb6d6ff6998f22`: headless shared routing test fails absent `PanelAction.CompareSaved` and real SessionController stub in push [38863540678](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863540678).
- GREEN `cd6676f9b3813baeae29c745e0e3c8cf7bc20379`: recorded Review exposes **Compare with last saved Native checkpoint**, retrieves a genuine `.plv.json` recorded baseline (with confirmed `.bak` fallback), and displays the saved-vs-current side-by-side facts in Desktop and XR without Native import, revision, receipt or implicit Save. The comparison is read-only and never claims a recomputation. Headless shared routing passed in [push 38863790282](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38863790282); full Windows/Ubuntu jobs were pending when this note was created.
- Unity EditMode `P11_03_RecordedRunComparisonNeverComputesDeltaForStaleOrDifferentModels` and PlayMode `P11_02_DesktopNotebookRecordsNativeEventWithoutMaterialRevision` have new comparison assertions at `587ec4ec1b1fd4b4b88cd2fbbc9b3ad5a2e882b0`, **authored but NOT_RUN**.

## Ruling / remaining blockers

This is a **two recorded snapshots** comparison, not a validated experiment replay, calibrated prediction, mixed-model numerical subtraction or complete cross-session comparison product. Only the saved checkpoint selected by the active `saveName` is supported, not arbitrary user-chosen runs or imported `.plvpack`. Do not promote release readiness. Portable archive import, full branch/fork workflow, event/material timeline, complete UI and Unity Player/XR verification remain **Open**.
