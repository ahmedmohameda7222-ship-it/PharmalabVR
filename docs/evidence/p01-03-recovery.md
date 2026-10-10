# P01.03 — local recovery boundary evidence

Functional source SHA: `24638ad35c2ce47fdce9c36d674755638273939d` (Windows Native Release, Unity 6000.3.25f1, Research six-stock profile).

`P01_03` first reproduced an expired neutral sample being accepted by Continue: a baseline captured at 1.000 s and checked at 1.010 s still released the hold at the 1.200 s command boundary. The production command now requires a monotonic timestamp for held sessions with tools, checks nondecreasing time and revalidates tracking, neutral actuator and ≤100 ms freshness at Continue. Unity supplies a new monotonic time immediately before submitting Continue. SetActuator, PlaceTool and material-affecting commands clear cached readiness.

A second `P01_03` regression reproduced a RinseTool transfer of 1 ml into waste while FocusLoss hold was active. The hold now rejects preparation, fixed transfer, disposal, rinse and inventory creation without material/time change, and clears readiness after a material command attempt. A fresh neutral sample and explicit Continue are required before rinse. The older checkpoint test was updated to perform that genuine recovery before rinsing.

Verification: native Release 68/68 doctest cases, 9201/9201 assertions and CTest 1/1; Unity PlayMode 14/14 on the staged current Windows DLL. The test is actual Native ABI and Unity session integration. It does not prove physical tracking or complete VR recovery UI.
