# Build and verification

The supported source baseline is Unity 6000.3.25f1 and C++17 with CMake 3.24 or newer. The Unity package lock must be produced by a successful import with that Editor; this repository does not fabricate a lock when the Editor is unavailable.

Run native verification from the repository root with `scripts/build-native.ps1 -Configuration Release`, followed by `python science/reference/verify_ideal_fixtures.py`. CI runs the same CMake, CTest, and reference checks on Windows and Ubuntu.

Run Unity asset generation with `scripts/build-unity.ps1 -UnityEditor <absolute Unity.exe> -Target AllAssets`. Use `WindowsDesktop`, `WindowsVR`, or `AndroidVR` only after package resolution and native plugin staging are verified. Unity test commands deliberately omit `-quit` until the test runner finishes.

The `Unity licensed verification` workflow is intentionally manual and targets a private self-hosted Windows runner labeled for Unity 6000.3.25f1. It fails if that exact licensed Editor is unavailable; it does not turn a missing Unity environment into a green check. The workflow stages the exact native plugin, generates assets, runs EditMode and PlayMode tests, and optionally builds one Player target.

Package only artifacts that were actually produced: `scripts/package-artifacts.ps1 -Source <artifact-directory> -Destination <output-directory> -Revision <full-commit-sha>`. The command refuses an empty source and records byte sizes and SHA-256 hashes.

The Windows desktop build must start without an XR runtime. Windows VR and Android VR are separate evidence targets. Simulator evidence is labeled SimulatedXR and does not establish physical-device acceptance.
