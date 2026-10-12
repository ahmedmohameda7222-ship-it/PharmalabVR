# P06.01 — native scene lifecycle and package failure

Functional code: `7abf0af2d763dfd74d98a37379a858e390aa8a37`. Unity 6000.3.25f1 on the local Windows laptop. This is task-scoped Research software evidence.

## Red, change, green

- Re-enabling the live `CoreDriver` in a Unity Lab scene created duplicate measurement/liquid/recorder components. A PlayMode regression first failed with two components. `CoreDriver` now reuses one component of each type, disposes the old `CoreSession`, clears the published view on disable/failure, and publishes a new immutable `LabViewModel` from the native snapshot. `NativeLabVisuals` consumes this view and subscribes once; the recorder's configuration is idempotent. The actual Lab scene test checks session replacement, closed old handle, renewed view, and one liquid/recorder instance after reactivation.
- A missing pinned scientific package is injected at the product `CoreDriver` startup path in PlayMode. Startup logs the error, disables the driver, leaves no live session, and retains the message for the Lab UI.
- Several older PlayMode journeys exposed genuine time holds during slow Editor frames. Material commands during those holds were correctly denied, but tests leaked sessions on assertion failure and produced cascading Busy errors. The tests now issue operations before waits where possible, use the real Continue path when a fresh baseline is available, and dispose roots on failure. X03 records whether TrackingLoss or an earlier TimeDiscontinuity caused the hold; either way the selected hand's invalid tracking sample prevents recovery. This does not claim TrackingLoss always supersedes an existing time hold.

## Local verification at the functional code

| Check | Result |
|---|---|
| Native Release CTest | `ctest --test-dir build/native -C Release --output-on-failure`: 1/1; native code unchanged in this commit. |
| Unity PlayMode | 26/26, including the real Lab scene lifecycle and missing package path. |
| Unity EditMode | 22/22. |
| WindowsDesktop, WindowsVR, AndroidVR | Fresh `Build Finished, Result: Success` in all three logs. Android APK contains ARM64 IL2CPP, native core, Unity runtime and minteq entries. |
| Actual Windows Player missing package | Copied the built Player into ignored `artifacts/evidence-p06-01/missing-package-player/` while excluding only `minteq.v4.dat`; all 185 other source files were present. Started its executable, selected Desktop on Boot, and observed `Native scientific core unavailable: Scientific database bytes are unavailable or invalid.` in the Player log and `Lab unavailable` in the rendered Lab panel. Screenshot `artifacts/evidence-p06-01/missing-package-player-error.png`. The original built Player retained its package. |

Raw XML, Player/build logs, screenshot, binary hashes and sizes are in `artifact-manifest-p06-01.json`. The desktop click and screenshot were from the actual Windows Player. This proves the missing package fails closed; it does not prove all normal student controls, headset startup, Android device behavior or complete P06.02 navigation. U01/U02 remain Partial in the 95-item catalog until their full journeys and negative plugin/device conditions are covered. G-R1 remains Open.
