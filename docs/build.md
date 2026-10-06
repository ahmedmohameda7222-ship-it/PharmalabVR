# Build and verification

The supported source baseline is Unity 6000.3.25f1 and C++17 with CMake 3.24 or newer. The Unity package lock must be produced by a successful import with that Editor; this repository does not fabricate a lock when the Editor is unavailable.

Run native verification from the repository root with `scripts/build-native.ps1 -Configuration Release`, followed by `python science/reference/verify_ideal_fixtures.py`. CI runs the same CMake, CTest, and reference checks on Windows and Ubuntu.

Run Unity asset generation with `scripts/build-unity.ps1 -UnityEditor <absolute Unity.exe> -Target AllAssets`. Use `WindowsDesktop`, `WindowsVR`, or `AndroidVR` only after package resolution and native plugin staging are verified. Unity test commands deliberately omit `-quit` until the test runner finishes.

The Windows desktop build must start without an XR runtime. Windows VR and Android VR are separate evidence targets. Simulator evidence is labeled SimulatedXR and does not establish physical-device acceptance.
