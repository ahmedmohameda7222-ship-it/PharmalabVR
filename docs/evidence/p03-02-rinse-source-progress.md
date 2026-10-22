# P03.02 Research rinse-source admission — 2026-10-15

**Scope:** Bounded native material-authority safety regression on the existing placed research burette's `RinseTool` command. **P03.02 Open; G-R1 Open.** This is not distinct bottle/pipette support and not a complete Lab experience.

## Plan / ruling
The formal dependency selector remains **P06.02**. The most recent explicit user priority was the P03 laboratory workstream. I completed a small isolated P03.02 native correction without claiming that dependent P03.01 (blocked by P07.03) or P03.02 can be closed. No existing stock is created or chemically overwritten by this patch; no science observations are fabricated.

## Test first (RED)
At source commit `880239680456f1ecd4411c9154325b4b9f5a673d`, the new product C ABI test `P03_02 rinse rejects solute-bearing stock without debiting the material ledger`:
- Prepares NaCl source stock, separate water rinse stock, a waste sink, and a placed burette through actual commands.
- Submits a chemically loaded source as a rinse. Requires explicit denial and no change to vessels, sinks, tool inventories or materialLedger.
- Submits genuine prepared water as the subsequent valid rinse. Requires source debit, bounded represented tip retention and conserved aqueous material pools.

Ubuntu CI **failed as expected** in that named case: `95/96` cases passed; `16231/16238` assertions passed; 7 failed assertions in the new case. The native engine returned `Accepted` for the NaCl rinse and removed `1e-6 m³` (1 mL) from stock. Build succeeded. [RED CI](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38677376470).

## Production change (GREEN)
At source commit `c7dc1894864f2d9ffb16fab3eaacf34dacfd55a3`, `native/core/src/abi.cpp` rejects a `RinseTool` source containing any represented positive sodium, chloride or acetate pool with `InvalidRinseSource`, before the staged transport. Clean water continues along the existing admission and conserved residual/waste path. This changes neither the immutable package nor IPhreeqc assumptions.

## Verified CI commands and evidence
Commands executed by the repository's `.github/workflows/native.yml` at the exact GREEN source commit:
```sh
cmake -S . -B build/native -DBUILD_TESTING=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build/native --config Release --parallel 2
ctest --test-dir build/native -C Release --output-on-failure
python science/reference/verify_ideal_fixtures.py
python -m unittest discover -s scripts/tests -v
```
**Push workflow:** [38677649560](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38677649560), Windows 2022 and Ubuntu 24.04 both `success`; `CTest 1/1`, 0 failed each. Ideal reference fixtures command `success`; Python tooling `9/9` each. **PR workflow:** [38677654246](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38677654246), Windows 2022 and Ubuntu 24.04 both `success`. GitHub Actions jobs' exit status is `success` (0); direct local commands could not run because no local Git worktree was available and external Git clone DNS was inaccessible.

## Review / critic / longer-term alternative
Review: native tool rinse now denies a material source that is chemically unfit for rinsing, without creating new inventory. Critic: eligibility only covers explicitly represented Na/Cl/acetate pools; it is **not** a validated universal purity determination. No new Unity PlayMode/EditMode execution, Windows Player action, simulated XR interaction or physical headset testing occurred at this SHA. No calibration, experimental validation, full P03 tool profile or user journey is claimed. The broader `P03.02` must still add distinct bottle and pipette profiles and actuators, prepared-stock fill/dispense/rinse interactions, and live Desktop/VR paths. Complete formal `P06.02` Setup/preferences and genuine Player/VR action routing first, then the dependent VR/geometry tasks. `plannerStatus` remains `NotReviewed` and G-R1 is not promoted.
