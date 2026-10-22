# P05.01 — all-transport scientific admission

Functional code `be2864faa29ce02bb4497ffcfe5560a530e9ca48`; test refinement `51069018c917a61f22bdc8ee16a274e8f159661d`. This is task-scoped **Research software** evidence. The product runs the pinned minteq IPhreeqc worker. It is not a physical-flow calibration, experimental chemistry validation, or a full Player journey.

## Defect and production correction

The first `P05_01` Native ABI regression submitted a fixed 10 ml transfer and observed `Accepted`, source/receiver material changes and an increased event sequence, despite the 0.05 ml Precision budget. Live tool ticks already staged the whole tick, while fixed transfer, disposal and rinse directly mutated `StateExecutor`.

`abi.cpp` now stages each discrete transfer, calculates actual gross vessel ingress/egress from the staged inventory, and calls the same precommit admission path used by live ticks. A failed admission enters Compute hold and leaves material, event sequence and simulation time uncommitted. The scheduler records gross movement only after commit. `scienceBudgetMode` is explicit and persisted on export/import: Precision is 100 ms/0.05 ml; Bulk is 200 ms/0.5 ml. Unity passes its selected profile to the same Native session; its default is Precision.

## Evidence

| Check | Result and exact scope |
|---|---|
| `P05_01 fixed ten ml transfer is denied before material commit` | Real Native product ABI waits for a Current IPhreeqc observation; 10 ml returns `ObservationVolumeHold`; vessel/sink/event state stays unchanged. |
| `P05_01 product Precision and Bulk profiles` | A 0.2 ml fixed proposal rejects in Precision and commits in Bulk; snapshot/export/import preserve the profile. |
| `P05_01 rinse and disposal share precommit admission` | Without a solved boundary, both return `ObservationRequired` with unchanged vessel, sink, tool and event state. |
| `P05_01 opposite fixed transfers` | First 0.03 ml A→B commits; immediate 0.03 ml B→A is denied before commit, despite its proposed zero net vessel volumes. |
| `P05_01 precision and bulk precommit boundaries` | Scheduler tests exact 0.05/0.5 ml and 100/200 ms acceptance boundaries, then one-unit-over age/volume denial. |
| Native Release / CTest | 85/85 cases, 15,803/15,803 assertions; CTest 1/1. The saved raw logs are in `artifacts/evidence-p05-01/`. |
| Unity | PlayMode 21/21 on the actual Lab scene and real native plugin; EditMode 22/22. Earlier scene tests were updated to wait for Current science and to use transfers within the new budget. |
| Build | WindowsDesktop Player build `Success` using the new Windows native plugin. The Windows executable, Windows/Android plugin, XML, build log and raw Native/CTest logs have SHA-256 and byte counts in `artifact-manifest-p05-01.json`. |

No current-code headset or Android device run, full Windows Player student/instructor/expert journey, calibration, measured pH validation, or long workload performance is claimed. G-R1 remains Open; the 95-item historical scoped catalog stays 18 Passed, 73 Partial and 4 EvidenceMissing. P05.02 fair bounded worker remains next.
