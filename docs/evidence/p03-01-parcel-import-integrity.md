# P03.01 — in-flight parcel identity at Native session import

**Date:** 2026-10-15 | **Branch:** `feat/foundation-vr-first` | [PR #1](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/pull/1), open/unmerged

| Checkpoint | SHA |
|---|---|
| Observed start HEAD | `0073a768a90a80c6d98bc962718aab50087f4087` |
| Initial RED test, incorrect fixture | `56bc9c0799e19d6ffe8c0ef0307fac1a1ffabf4c` |
| Corrected RED test | `4a851ebb92b31bb48ff52037b54875bdb25a276f` |
| Functional GREEN Native code | `5d7c12dfdabc7927c2f2eaa125a236ea773e3660` |

## Proven product defect and resolution

The product ABI `plv_import` accepted a represented, nonzero pending burette parcel even if its `inFlightParcelId` was empty, pointed to another branch/tool or named an emission event later than the saved event sequence. This breaks the provenance identity used to link `LiveTransferCommitted`, the pending drop renderer, and `InFlightLanded`.

The new Native regression creates real vessels, stock and a placed research burette, submits a tracked actuator input, advances a real 20 ms Native transport step and exports a valid pending parcel. A valid import remains admitted. Replacing its ID with (1) empty, (2) wrong branch, (3) wrong tool, or (4) impossible future emission must return `PLV_INVALID_ARGUMENT`, not take over the Native session, and leave the exported original unchanged.

Only `native/core/src/abi.cpp` was functionally changed: `parseTools` verifies positive pending inventory maps to a nonempty `branchId/eventSequence/toolId`-shaped token, an eventSequence of 1..saved event sequence, and matching tool/branch strings. Zero pending material still requires an empty token. This is deterministic **provenance-format validation**, not cryptographic authentication, physics or visual proof.

## TDD and verification

- [Initial RED push 38711919167](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38711919167), [PR 38711926355](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38711926355): the new test incorrectly looked up a JSON field `referenceVolumeM3`; actual ABI exports `researchAdditiveVolumeM3`. It threw before cleanup, causing 15 unrelated later `PLV_BUSY` create failures. **Invalid RED baseline**; corrected transparently.
- [Corrected RED PR 38712269879](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38712269879): Native CTest compiled and ran **97 cases, 96 passed, one failed**, exactly **4 failed assertions**; all malformed identities returned `PLV_OK` instead of `PLV_INVALID_ARGUMENT`. No cascade.
- [GREEN push CI 38712685535](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38712685535): native Ubuntu job **SUCCESS**, CTest **1/1 pass, zero failed**, using functional SHA `5d7c12dfdabc7927c2f2eaa125a236ea773e3660`. C++ compilation, source-level ABI, ideal/reference fixtures and repository tooling run under the native workflow. The Windows job was **still building** when this record was written.
- [GREEN PR CI 38712694119](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38712694119): Ubuntu native job **SUCCESS** at the same source SHA; Windows status requires independent check. The headless shared-panel suite is separate from Native science and does not prove Unity.

## Limits, ruling and subsequent work

The physical fluid material pools, volumes, transport equations, vessel revisions and IPhreeqc solver were **not changed**. The scoped fix applies to import/export validity and final-drop ID continuity for represented Native parcels. No Unity Editor/PlayMode, Desktop or XR Player action, official XR simulator or physical headset test was run in this environment. P03.01 still needs visible drop/link/landing and actual held/lab/receiver journeys; **P07.03 remains Open** as its dependency.

**Ruling:** Owner requested development-first source progress before the final Unity testing campaign. Proceed with bounded Native regression while the formal selector remains `P06.02`; do not mark P03.01 VerifiedLocal or AcceptedPlanner. Ledger totals **15 VerifiedLocal / 51 Open**, G-R1 Open. Next source increment should tighten Native-linked DropPresenter event presentation and continue P03.02 research bottle and pipette profile modeling, without fabricating physical calibration.
