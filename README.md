# PharmaLabVR

PharmaLabVR is a VR-first, offline research laboratory foundation with a complete Windows mouse-and-keyboard mode. Both modes share one native material, transport, scientific-observation, and session core.

The initial research profile is deliberately restricted to homogeneous aqueous water, hydrochloric acid, sodium hydroxide, sodium chloride, acetic acid, and sodium acetate at documented reference conditions. It is not a universal chemistry simulator, certified assessment system, or experimentally validated laboratory substitute.

Implementation authority and evidence boundaries are recorded in `docs/implementation-package-v3/`. Original user reference documents are intentionally excluded from this public repository.

## Verified scope

The native C ABI, deterministic material/transport authority, conservation checks, bounded event queue, snapshot import/export, and independent ideal aqueous reference adapter are compiler-tested on Windows/MSVC and Linux/GCC in GitHub Actions. The independent fixture verifier covers 40 approved ideal cases.

The Unity 6.3 source pins OpenXR, XR Interaction Toolkit, Input System, and URP; provides shared desktop/XR logical input adapters, research capability gates, atomic local saves, localization data, performance recording, and batch build entry points. Unity Player builds, rendered UI/scene inspection, Android packages, and physical-headset runs remain explicitly unverified until the pinned Editor and target hardware are available. The generated Lab scene starts in desktop mode with the XR rig inactive; a future boot selector must switch adapters without enabling both.

## Reproduce

Run `scripts/doctor.ps1`, `scripts/build-native.ps1`, and `science/reference/verify_ideal_fixtures.py`. With Unity 6000.3.25f1 installed, run `scripts/test-unity.ps1` and `scripts/build-unity.ps1`. Acceptance status files under `docs/evidence/` distinguish source availability from actual Player or hardware evidence.
