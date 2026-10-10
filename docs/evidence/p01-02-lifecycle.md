# P01.02 — local lifecycle evidence

Functional source SHA: `e5c6b69c8cf89a20b8e41bf0b17fb362c385118d` (Windows 11, MSVC 19.44, CMake 3.31.6-msvc6, Unity 6000.3.25f1). Research profile, pinned `minteq.v4.dat`.

The new `P01_02` ABI test failed before the fix: a second mutable native context was accepted, a foreign thread submitted a material command, and import left the old context mutable. After the fix, one active context accepts mutation; successful import activates the replacement only after validation. Snapshot/export of an older context remain available for comparison. At most three retired contexts can coexist with the active one while their current calls finish; a fifth context returns Busy. Foreign submit/input/step return Busy. Three real IPhreeqc create → acid solve → export/import → current solve → destroy cycles passed, including double-destroy rejection.

The callback ownership test failed before the fix because its final reference was destroyed on the caller thread. The SolverWorker now releases the callback and IPhreeqc adapter on its own thread. A blocked callback injection proves `requestStop()` returns Busy without detaching/deleting its context and stops new submissions. Native `plv_destroy` retains an active call and returns Busy; Unity `CoreSafeHandle` retains that handle in a bounded retirement queue and retries on a persistent runtime pump. A current solve is never forcibly canceled. A stalled engine still requires an explicit application restart if it does not return; actual pathological IPhreeqc convergence/cancellation proof belongs to P02.03 and remains open.

Verification on current source:

- Native Release: 66/66 doctest cases, 9093/9093 assertions; CTest 1/1.
- Unity EditMode: 22/22 (`artifacts/test-results/EditMode.xml`); PlayMode: 14/14 (`artifacts/test-results/PlayMode.xml`) after staging the current Windows DLL. An intermediate PlayMode run failed 9/14 because a two-context cap rejected import while earlier solves retired; the bounded retirement allowance fixed that real integration failure.
- Doctor and ledger Python tests: 5/5. Doctor now identifies the actual pinned Editor at `C:/Program Files/Unity 6000.3.25f1/Editor/Unity.exe`, not the unrelated Unity CLI executable.

These are local software tests. They do not establish physical VR, Android runtime, experimental science, or a hard cancellation bound for an actual hung IPhreeqc call.
