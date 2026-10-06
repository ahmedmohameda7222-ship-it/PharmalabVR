# PharmaLabVR

PharmaLabVR is a VR-first, offline research laboratory foundation with a complete Windows mouse-and-keyboard mode. Both modes share one native material, transport, scientific-observation, and session core.

The initial research profile is deliberately restricted to homogeneous aqueous water, hydrochloric acid, sodium hydroxide, sodium chloride, acetic acid, and sodium acetate at documented reference conditions. It is not a universal chemistry simulator, certified assessment system, or experimentally validated laboratory substitute.

Public implementation authority and evidence boundaries are recorded in `docs/implementation-package-v3/`. The six original user source documents are intentionally excluded from this public repository.

## Verified scope

The native C ABI, deterministic material/transport authority, conservative tool parcels, bounded observation scheduler and worker, explicit holds, checksummed journal recovery, ordered command/session restoration, production IPhreeqc adapter, and separate ideal aqueous reference adapter are compiler-tested on Windows/MSVC and Linux/GCC in GitHub Actions. The production adapter executes the bundled pinned `minteq.v4.dat`; the independent fixture verifier separately covers 40 ideal cases.

The Unity 6.3 source pins OpenXR, XR Interaction Toolkit, Input System, and URP; provides an explicit desktop/VR boot selector, shared logical input adapters, mouse camera and tool handling, generated inspectable research-tool/panel prefabs, committed-volume liquid presentation, English/Arabic localization with licensed fonts and RTL shaping, research capability gates, atomic local saves, protocol examples, performance recording, and strict batch build entry points. Unity Player builds, rendered screenshot inspection, Android packages, and physical-headset runs remain explicitly unverified until the pinned Editor and target hardware are available. Exactly one input rig is enabled at a time.

## Reproduce

Run `scripts/doctor.ps1`, `scripts/build-native.ps1`, and `science/reference/verify_ideal_fixtures.py`. With Unity 6000.3.25f1 installed, run `scripts/test-unity.ps1` and `scripts/build-unity.ps1`; the manual licensed Unity workflow uses the same commands. `scripts/package-artifacts.ps1` hashes only real files, and `scripts/analyze-performance.py` refuses incomplete Player captures. Acceptance status files under `docs/evidence/` distinguish source availability from actual Player or hardware evidence.
