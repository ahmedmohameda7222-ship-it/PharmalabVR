# PharmaLabVR

PharmaLabVR is a VR-first, offline research laboratory foundation with an additional Windows mouse-and-keyboard mode. Both modes share one native material, transport, and session core; the standalone scientific-observation engine is tested, while its complete in-Player measurement/review journey remains partial.

The initial research profile is deliberately restricted to homogeneous aqueous water, hydrochloric acid, sodium hydroxide, sodium chloride, acetic acid, and sodium acetate at documented reference conditions. It is not a universal chemistry simulator, certified assessment system, or experimentally validated laboratory substitute.

Public implementation authority and evidence boundaries are recorded in `docs/implementation-package-v3/`. The six original user source documents are intentionally excluded from this public repository.

## Verified scope

The native C ABI, deterministic material/transport authority, conservative tool parcels, bounded observation scheduler and worker, explicit holds, checksummed journal recovery, ordered command/session restoration, production IPhreeqc adapter, and separate ideal aqueous reference adapter are compiler-tested on Windows/MSVC and Linux/GCC in GitHub Actions. The production adapter executes the bundled pinned `minteq.v4.dat`; the independent fixture verifier separately covers 40 ideal cases.

The Unity 6.3 source pins OpenXR, XR Interaction Toolkit, Input System, and URP; provides an explicit desktop/VR boot selector, shared logical input adapters, mouse camera and tool handling, generated inspectable research-tool/panel prefabs, committed-volume liquid presentation, English/Arabic localization with licensed fonts and RTL shaping, research capability gates, atomic local saves, protocol examples, performance recording, and strict batch build entry points. Unity 6000.3.25f1 now passes EditMode and PlayMode tests locally. WindowsDesktop, WindowsVR, and Android ARM64 IL2CPP players build; the Desktop player was started and entered the Lab, and the WindowsVR player was started on a machine without an OpenXR runtime. Physical-headset and Android-device runs remain explicitly unverified. Exactly one input rig is enabled at a time.

## Reproduce

Run `scripts/doctor.ps1`, `scripts/build-native.ps1`, and `science/reference/verify_ideal_fixtures.py`. With Unity 6000.3.25f1 installed, run `scripts/test-unity.ps1` and `scripts/build-unity.ps1`; the manual licensed Unity workflow uses the same commands. `scripts/package-artifacts.ps1` hashes only real files, and `scripts/analyze-performance.py` refuses incomplete Player captures. The current P00–P16 status, all 95 acceptance cases, artifact fingerprints, and explicit evidence gaps are under `docs/evidence/`.
