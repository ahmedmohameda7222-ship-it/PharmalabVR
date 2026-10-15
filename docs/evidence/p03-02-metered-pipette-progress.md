# P03.02 — Native-owned metered research pipette and separate residual

**Date:** 2026-10-15 | **Branch:** `feat/foundation-vr-first` | [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) OPEN/unmerged.

| Milestone | Git SHA |
|---|---|
| Observed start HEAD | `5ecbe7e8aad342ef15a777ef7dcd9dcadda0a74d` |
| First TDD RED tests | `a53ff552493c93225e423bc961307a6d631c5aab` |
| Corrected Bulk-budget test fixtures | `e3d3100cb07704267fe1435014f8c4ed663395aa` |
| Research profile admission | `3cb88d90921fdfbf8751c887e568ea25958a1424` |
| Native metered commands | `8dd167a329a3a3a667c18bc29eb35871a02a5f39` |
| Separate residual RED | `75572fb6c478af6fa1b4867a46b31237a5d1e6e6` |
| Functional residual source | `259b0b96cbf4216df588532fe48d3b632efe7048` |

## Actual Native product behavior

`PlaceTool` can register `pipette-25ml-research-v1`, with independent `PipetteProfile` research capacity 25 mL. The live tool refuses `SetActuator` and positive raw tracked valve input for that profile; pipette cannot accidentally flow through `simulateBuretteDelivery`. Native commands `AspiratePipette` and `DispensePipette` accept ordered real C ABI requests with expected tool inventory and touched vessel material revisions. They stage stock-to-tip and tip-to-receiver fractions through the existing pinned science-budget and material ledger. Excess fill, insufficient material, stale revisions, full receiver, invalid metering and dispensing represented residual are denied without partially consuming source material. A successful rinse consumes separately prepared analyte-free water, sends retained contamination into a represented sink and keeps a pipette-specific bounded research tip residue of **1e-7 m³ (0.1 mL)**, distinct from the burette's **5e-8 m³ (0.05 mL)**.

**Numbers are research placeholders, not physical pipette calibration.** Native input tick remains the burette's valve/orifice research model, not a pipette substitute. There is no newly invented pH, chemical result or direct IPhreeqc bypass.

## Verification

- [RED corrected test CI push 38719239548](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38719239548): Ubuntu and Windows native CTest failed as intended in exactly the new `P03_02 distinct 25 ml pipette aspirates aliquot and dispenses conserved material` case, **97/98 cases passed, 6 failed assertions**. Before code, PlaceTool denied pipette, metered commands did not exist and SetActuator had no distinct rejection. The original test fixture `a53ff55` was proactively corrected to small `Bulk` budget transfers before implementation: no oversized experimental flow is smuggled through the scientific admission gate.
- [Native code GREEN push 38719760416](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38719760416) and [PR 38719769670](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38719769670): all Windows 2022/Ubuntu 24.04 Native jobs SUCCESS at the metered-source commit. Native CTest, ideal research fixtures and Python tooling were executed. The pipette ABI case verifies accepted fill of 3e-7 m³ from acid stock in a stock vessel, cap rejection at 25 mL, actual metered dispense 1e-7 m³ to receiver, chloride in receiver/tip, unchanged total represented reference volume, chloride and solvent-water mass across both operations, then water rinse to sink.
- [Separate research residual RED CI push 38720128482](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38720128482): real Native `P03_02 research pipette has separate represented residual and capacity` failed while both instruments retained the same 5e-8 m³. **98/99** cases passed; one explicit distinct-residual assertion failed (5e-8 > 5e-8 was false).
- [Residual GREEN CI push 38720458723](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38720458723): Ubuntu Native CTest **1/1 PASS** at exact final functional code `259b0b96cbf4216df588532fe48d3b632efe7048`; Windows and PR jobs still building when recorded. `RinseTool` now uses the registered instrument profile's own hold-up boundary. Full science fixtures and script checks are separate CI jobs from the headless panel routing.
- **NOT_RUN** Unity Editor compilation, EditMode/PlayMode, WindowsDesktop/WindowsVR/AndroidVR Player, XR simulator ray-grab/fill/rinse and real hardware at this SHA. Prior PlayMode historical evidence predates this Native extension. The science package currently declares a research pipette profile, but the stock bottle is still just a prepared Native source vessel; there is no independently realized bottle geometry/profile/actuator, user-facing `PipetteActuator.cs`, cross-stock switch/refill journey, or validated full 25 mL aspiration under pinned budgets.

## Ruling / next work

**Ruling:** Product Owner requested source-first implementation across phases, with final Unity testing later. P03.01 remains formally Open (depends on P07.03), therefore P03.02 also **Open / NotReviewed**; this is a Native source slice, not a task completion or planner acceptance. Keep **15 VerifiedLocal / 51 Open**, **G-R1 Open**, and 66 task-level Unity checks marked **NOT_RUN**; keep PR #1 open, main untouched. Next bounded implementation: register a research stock bottle profile with guarded fill/revision rules, expose pipette command wiring through Unity CoreDriver and Shared Panel UI, and test refill/contamination after source switching without weakening pinned mass/science budgets.

## Follow-up — Native prepared-stock source switch with conserved tip contamination

| Checkpoint | SHA |
|---|---|
| Test-first RED | `659dd0eb3e4aa0e6ae84c4e4877e0498a979eda7` |
| Functional C++ source | `482e2e2c97cd280e832d887d4bf4aa9fe023366a` |

- [RED CI push 38721263996](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38721263996): **99/100 Native test cases passed**. The new named C ABI case had exactly 7 failed assertions: `SwitchPipetteSource` absent and downstream bottle/receiver material checks failed. No unrelated Native test failures.
- [GREEN CI push 38721647508](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38721647508) / [PR 38721653481](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38721653481): real Ubuntu CTest **1/1 PASS**, Windows jobs still building at time of evidence entry.
- Production adds `SwitchPipetteSource` only for the metered pipette profile, denied while Native held, requires an existing different prepared stock source and exact expected tool inventory and stock material revisions. Updates source identity/tool revision but does **not** consume, create or replace any represented material or clear tip contamination. The next real `AspiratePipette` mixes source aliquot with residual chloride, and `DispensePipette` produces a new clean receiver containing represented sodium **and** chloride. Native materialLedger water/Na/Cl/reference volume remains equal to pre-transfer amounts. Unknown stock is explicitly denied, after-state material ledger unchanged.
- Remaining boundaries: bottle stock source is a represented vessel, not a separate validated physical tool profile/mesh/actuator. Research pipette 25mL capacity and 0.1mL residual remain **pending physical calibration**, and per-tick science budget throttles the amounts achievable. No C# pipette controls/VR tracked user route or Unity Player tests at latest SHA. P03.02 still **Open/NotReviewed**, no task status promotion, no PR merge.
