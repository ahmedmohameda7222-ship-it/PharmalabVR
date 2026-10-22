# P03.02 progress — retained tip material enters the next delivery

Functional code SHA: `d8fabed1785227a700308a839c9f256e3fe79f58`. Windows, Unity 6000.3.25f1, Research aqueous package. **P03.02 remains Open.** This increment concerns the existing burette's represented material; it does not implement a bottle or graduated pipette.

## Red, production change, green

`P03_02 retained tip contaminates the next stock delivered through the live tool` uses the product C ABI. It prepares hydrochloric acid, pours with the placed burette, closes and lands the pending parcel, empties the source to its sink, prepares a sodium hydroxide stock in a separate vessel, and transfers that prepared stock into the source. The old code left chloride in the tool tip while receiver chloride stayed exactly unchanged on the next base pour (red, 61/62 assertions passed). The production tick now mixes the represented tip parcel with the arriving source parcel, retains the same bounded tip volume, and delivers the remainder. The receiver gains chloride and sodium, and all material ledger pools remain conserved within 1e-12 relative tolerance. No pH or chemical result is injected by the test or code.

The prior `PrepareStock` restriction remains: a used vessel cannot create new stock during normal play. An uncommitted `RefillStock` experiment was discarded after checking `docs/implementation-package-v3/02-CONTRACTS.md`, which requires prepared stock to be a setup action. The actual refill route still needs prepared stock inventories, user-controlled transfers into each tool, and budget-compatible observation boundaries. A 25 ml one-shot disposal is correctly denied by the current Precision science budget, so a full source-change journey is not claimed.

## Verification and remaining scope

At `d8fabed`, native Release passed 94/94 cases and 16059/16059 assertions, CTest 1/1; the red failure and green result came from the same named ABI test. Windows and Android ARM64 native libraries rebuilt and were staged into Unity. Unity PlayMode on the restored, committed source passed 33/33; EditMode passed 23/23. Fresh WindowsDesktop, WindowsVR and AndroidVR builds reported Success with the rebuilt native plugins; the Android APK contains the pinned minteq database with its expected SHA-256. CI on the pushed code SHA passed four native jobs (Windows and Ubuntu, push and PR). The previous Lab Editor PlayMode screenshot in `artifacts/evidence-p03-lab/lab-pending-drop-editor.png` predates this chemistry fix and is not image proof of the new material mix.

Distinct bottle/pipette profiles, own actuators and residual rules, fill/transfer/rinse/refill UX, actual Player action journey, calibration and physical/device evidence remain open. Do not promote T04/M05, P03.02 or G-R1 from this bounded result. The native test output is `artifacts/test-results/native-p03-02-tip-mixing.txt`; subsequent build hashes and logs are in `artifact-manifest-p03-02-tip-mixing.json`.
