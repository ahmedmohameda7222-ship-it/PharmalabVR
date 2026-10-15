# P03.02 — distinct registered research bottle and measured Native transfer

**Date:** 2026-10-15. **Start HEAD:** `52529238ea7d3d30f7461598ba3732d6a31fece8`. **Branch:** `feat/foundation-vr-first`. [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1) remains open and unmerged.

| Step | SHA |
|---|---|
| Test-first RED | `26019233c93758414c60fa5fad16e8e5db0b99c8` |
| Functional Native source | `ca1aea83b24429ce0b55615754c7b8bec58e1b63` |

## Production scope

Adds `BottleProfile::researchDefault()` with a 100 mL research-only capacity profile `bottle-100ml-research-v1`, recorded in `science/packages/aqueous-six-research-v1/instruments.json` as **pending physical calibration**. This is separate from the 25 mL Native metered pipette and 50 mL head-flow burette. Stock volume remains in an actual prepared Native vessel; there is no cloned bottle-only fluid.

`PlaceTool` registers the bottle only if its source vessel exists, its capacity is within the research bottle limit and the overflow sink exists. `SetActuator` is rejected with `BottleRequiresMeasuredPour` and positive raw tracked valve input is not allowed to invoke `simulateBuretteDelivery` for this profile. A new ordered `PourBottle` command accepts a positive finite research aliquot, touched source/receiver material revisions and tool inventory revision. It stages the existing Native `TransferFixed` procedure, calls the unchanged science-budget admission, and commits only on acceptance; successful results update source/receiver inventory and material event sequences. The saved bottle tool is profile/identity/capacity-checked at import. No chemical result, IPhreeqc pH or new material is invented.

To avoid pretending cleaning is implemented, `RinseTool` for the bottle explicitly returns `BottleRinseNotImplemented`; a dedicated stock bottle cleaning/film residual model and validated tip/volume conservation remains Open.

## RED / GREEN verification

- [RED CI PR 38786863512](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38786863512): Ubuntu Native test compiled successfully. **101 test cases, 100 passed, one failed** in `P03_02 registered 100ml stock bottle pours through revised Native inventories without valve emulation`, exactly one failed assertion: the valid `PlaceTool` bottle was denied by the original profile allowlist. Old test fixtures remained intact.
- [GREEN CI PR 38787431765](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38787431765), [push 38787422220](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38787422220): Ubuntu Native job SUCCESS, CTest **1/1, zero failures** against `ca1aea83b24429ce0b55615754c7b8bec58e1b63`; existing headless C# panel-routing job SUCCESS. Windows jobs were still building when evidence written. Exact per-SHA conclusions must be rechecked before an all-platform statement.
- Named Native ABI test checks registered-capacity refusal, correct profile, independent measured pour of 20 µL, source material debit, chloride present in receiver, unchanged complete Native material ledger and stale tool-revision refusal. It does not use a Unity fake or substitute fluid simulation.

## Ruling and pending scope

**Ruling:** Since a concurrent connected workflow had already advanced `feat/foundation-vr-first` from the previous P03.01 checkpoint to `52529238` with pipette + source switch, the stale-HEAD test creation was aborted and this increment was applied on the verified newer tip. The user explicitly requested all source phases ahead of the final Unity campaign. That permits a bounded Native model but **not** claiming the P03.02 whole task accepted.

There is no distinct stock bottle film/residual or cleaning, calibrated continuous tilt/outlet flow, Unity `PipetteActuator.cs` or `BottlePourActuator.cs`, actual scene/user fill/transfer/rinse/refill, Desktop Player, official simulator or headset verification. All still **NOT_RUN** at the current SHA. P03.02 Open/NotReviewed, P03.01/P07.03 dependencies Open, 15 VerifiedLocal/51 Open, 66 task-level Unity checks NOT_RUN, G-R1 Open, no PR merge.
