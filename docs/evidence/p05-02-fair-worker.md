# P05.02 — fair bounded equilibrium work

Functional code: `57006a4dc83f49c7216268018b68d0be6e73f83a`. This is task-scoped Research software evidence.

## Red, correction, and product behavior

- A controlled-delay `P05_02` worker test first rejected replacement of B revision 1 with B revision 2 when A was solving and C was queued at capacity. The worker now replaces the pending B job in place, keeps three outstanding requests, then completes A, B revision 2, C in order. An older B request is rejected.
- A second red test showed `ObservationScheduler::offer` allowed A revision 2 to replace pending A revision 3. The scheduler now rejects regressive revision/time requests before changing authoritative observation state or queue order.
- The Native product ABI test uses the pinned minteq IPhreeqc package for three acid vessels. A fixed transfer changes A/B, retains the unrelated Current pH/value, advances one event, and both changed observations reach Current revision 2 while that event sequence stays stable. `KineticSolve` is rejected without adding a material event.
- The Unity PlayMode Lab scene test performs a live pour through the actual native plugin with a third prepared vessel. That vessel's observation remains Current and the whole material ledger includes its 1 ml preparation. The O06 measurement integration checks Current numeric Research and an empty vessel as Unsupported with no displayed number.

## Verification

| Check | Result |
|---|---|
| Native Release | 88/88 cases, 16,080/16,080 assertions in saved run; CTest 1/1. |
| Unity 6000.3.25f1 | PlayMode 21/21; EditMode 22/22. |
| WindowsDesktop | Fresh Player build log reports `Build Finished, Result: Success.` Windows and Android ARM64 native plugins were rebuilt and staged. |
| Raw artifacts | `artifact-manifest-p05-02.json` gives SHA-256 and bytes for XML, logs, plugins and Windows executable under ignored `artifacts/evidence-p05-02/`. |

**Limits:** O06 Failed and Stale states were not driven end to end through Unity UI in this round; O07 kinetic/multiphase behavior is an explicit unsupported command, not a simulated model. The scene is a Unity PlayMode action test, not a fresh standalone Player or headset journey. No controlled maximum-flow, long-duration performance, physical calibration or experimental science result is claimed. Those catalog criteria remain Partial; G-R1 stays Open.
