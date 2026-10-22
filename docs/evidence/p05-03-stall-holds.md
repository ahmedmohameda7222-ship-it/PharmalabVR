# P05.03 — worker stall and safe hold software evidence

Functional code: `0ac90ef19bc8aaf7b545e075717508ac67dcac68`. This is scoped Research software evidence, not a headset or engine cancellation claim.

## Findings and changes

- CI on the previous P05.02 evidence HEAD `c14b2e8` failed on Ubuntu and once on Windows in the P04.01 rollback test. A restored generation with material revision 1 was rejected by `SolverWorker` because an older generation's pending revision 2 won the comparison. A controlled-delay test failed before correction. Worker replacement now compares `dependencyGeneration` before revision/time; a 20-run local replay of the P04.01 real IPhreeqc rollback then passed. The current final HEAD CI must be checked separately.
- A blocked-worker test failed because `requestStop()` cleared queued jobs without reducing `outstanding_`. Stop now removes those pending jobs from accounting, keeps the currently solving job alive, refuses new work, and never detaches a used context.
- Controlled callback injection confirms the default two-second wall stall diagnostic, bounded worker capacity, and Busy shutdown while the solve callback remains blocked. Product snapshots expose `solverWallStalled`; `plv_step` enters Compute hold if that diagnostic is active, clears unconsumed tool inputs, and keeps simulation/material from advancing until neutral, current science and explicit Continue.
- The existing `HoldPanel` offered Continue for a Compute hold based only on neutral/baseline state. Its test failed before correction. It now requires native `recoveryReady`; the active Desktop HUD reads the same native state and disables Continue while false. A real Unity Lab scene test denied an over-budget 10 ml transfer, saved during Compute hold, and verified the source material and event sequence remained unchanged.

## Local verification

| Check | Result and scope |
|---|---|
| Native Release | 92/92 cases and 16,101/16,101 assertions in saved run; CTest 1/1. |
| P04.01 rollback replay | 20/20 local Windows runs after generation correction. |
| Unity 6000.3.25f1 | PlayMode 23/23 after final native plugin staging; EditMode 22/22. |
| WindowsDesktop | Fresh Player build `Success` at this functional code. Windows and Android ARM64 native plugins rebuilt and staged. |
| Raw artifacts | SHA-256 and bytes in `artifact-manifest-p05-03.json`; ignored files under `artifacts/evidence-p05-03/`. |

**Acceptance limits:** H01 software freeze/save, H02/H05 recovery/backlog, and H04 controlled blocked-worker mechanics are covered within the named tests. The injected callback is not an actual pathological IPhreeqc solve; no cancellation or nonconvergence guarantee is inferred. H03 physical controller/tool alignment, a standalone Player hold journey, long workload and device behavior remain open. The 95-item historical catalog is unchanged, and G-R1 remains Open.
