# October 10 intake and finding disposition

Intake HEAD `f712fc0f3c308454098919b5b26f107d219e441b` on `feat/foundation-vr-first`; worktree was clean. PR #1 was open at that same SHA. No reset or merge. The Oct10 review is about this HEAD; earlier Oct9 defects already fixed there remain credited. The reviewed functional source was `10d6dae3d62faf2c25d85364b0c29f26a97fb382`.

The v2 handoff ZIP SHA-256 is `1E51FAF0E32B31425566CA23D9646F20A84077FD9DB8796C31D723B648C90CAD`; its verifier checked 69 entries with no errors. The recovered v3 archive verified 275 original hashes with ten explicitly missing originals, including six private source documents. It is reference evidence, not a substitute for the missing originals. The v2 plan/spec/design/phase files are mirrored in `docs/premium-master/`; private recovered references and reviewer working files are not published.

`scripts/doctor.ps1` succeeded on Windows 11 with Unity 6000.3.25f1, MSVC 19.44, CMake 3.31.6-msvc6, Python 3.14.7, Git 2.55 and authenticated gh 2.102 after supplying installed MSVC/CMake paths through `PLV_CXX` and `PLV_CMAKE`. The first invocation without those PATH overrides reported both tools missing; that was discovery scope, not absence of the installed toolchain. `environment.json` is the successful invocation. Physical VR remains EvidenceMissing.

## Native review

| Finding | Current disposition | Required closure/evidence |
|---|---|---|
| O10-N1 checkpoint scientific rollback | Open, reproduced in review | P02 checkpoint generation and real worker rollback test; old revision result may not relabel restored state. |
| O10-N2 discrete commands bypass science budget | Open | P02 common precommit admission for TransferFixed, rinse and disposal with actual gross amounts. |
| O10-N3 partial multi-tool tick | Fixed in current working change; focused red/green proof | `P01_01` ABI test failed before fix: 5 live commits/eventSequence 3→8 and spill 4.9942511601926056e-8 m³ at simulationTime 0. Staged full tick now denies without inventory/time/event mutation; focused test and CTest pass. Commit SHA pending. |
| Worker stall/context teardown | Open | P01.02/P02 lifecycle and blocked-worker diagnostics, worker-owned IPhreeqc destruction. |
| One session/owner thread/limits | Open | P01.02 real over-limit/repeated lifecycle tests. |
| Continue readiness invalidation | Open beyond repaired input replacement | P01.03 boundary freshness expiry and SetActuator/Rinse invalidation. |
| Model-aware import and recorded review | Open | P11 provenance/hash validation and recorded-only path. |
| Tool parcels, journal, science provenance/performance | Open | P03/P11/P02/P16 respective production integration and end-to-end evidence. |

## Unity review

| Finding | Current disposition | Planned closure |
|---|---|---|
| Esc disables desktop control without native Pause | Open | P10 recovery journey and injected key scene test. |
| VR Boot/spatial menu/hold controls absent | Open | P04.01–03 VR scene and ray action journey. |
| Six-stock free laboratory absent | Open | P05/P06 actual stock preparation and placement. |
| Primitive/unreadable apparatus and room | Open | P05 calibrated assets, scale/meniscus and rendered Player views. |
| Measurement bound only to source | Open | P08 contextual receiver observation/instrument selection. |
| NaCl/water rendered blue | Open | P05 colorless presentation from state. |
| Body grip and valve ownership conflated | Open | P04 separate capture, receiver and two-hand ownership. |
| Hardcoded capture profile/outlet point | Open | P03/P04 shared calibrated profile. |
| Desktop map/help/placement incomplete | Open | P10 full adapter and usable Help. |
| Load/recovery visible pose not aligned | Open | P01.03/P04 virtual docking and neutral re-grab. |
| Review/protocol/Arabic/accessibility absent | Open | P11/P12/P13/P15 functional journeys. |

## Acceptance review corrections

The historical 60 Native / 22 EditMode / 14 PlayMode tests and three successful builds establish scoped foundation, not G-R1. `acceptance-95.json` reports 19 Passed, 72 Partial and 4 EvidenceMissing at intake; these are unaudited historical classifications. B02 current no-runtime start, H03 virtual alignment, remaining science/tool integration and all learner/instructor/expert journeys need fresh exact-source proof. Historical `task-reviews.md` counts and `implementedTaskIds` wording must be corrected in the evidence phase; no acceptance status is promoted by this intake. G-R1 remains Open; P18–P22 remain gated. No headset, experimental calibration, participants or institutional adoption are inferred.
