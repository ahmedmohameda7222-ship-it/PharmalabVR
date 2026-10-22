# Delivery review — local Windows/Unity continuation

## Verified outcome

- Native Release builds under MSVC and passes 52/52 cases, 565/565 assertions and CTest 1/1. Python tooling passes 6/6. The independently labeled ideal reference passes 40 cases with zero expected-pH error and maximum charge residual `5.204170427930421e-17 mol/L`; this is not experimental validation of the product.
- Unity 6000.3.25f1 imported and compiled the project. EditMode passes 18/18 and PlayMode passes 3/3, including controller-profile, grip/trigger separation, desktop focus/world-pose guards, actual native-library lifecycle and Desktop-adapter input ticks.
- WindowsDesktop builds and actually opens the Lab without an OpenXR loader/runtime dependency. The final Player journey logged zero native input, DLL, entry-point or OpenXR runtime errors.
- WindowsVR builds with the OpenXR loader and VR-first define. On this no-runtime/no-headset laptop the executable opens and reports the expected `XR_ERROR_RUNTIME_UNAVAILABLE`; that is availability evidence, not a physical VR pass.
- AndroidVR builds as an IL2CPP ARM64 APK. Inspection confirms application id `com.pharmalabvr.lab`, version `0.1.0-research`, min SDK 25 and `arm64-v8a` libraries including `libpharmalab_core.so`, `libil2cpp.so`, `libopenxr_loader.so` and `libUnityOpenXR.so`.
- Actual Windows Player screenshots, build/test logs, artifact sizes and SHA-256 fingerprints are retained under `artifacts/`; tracked summaries are in `docs/evidence/`.

## Important implementation findings fixed in this continuation

- XR settings initially had no manager/loaders. Build configuration now creates per-target managers and assigns/removes OpenXR explicitly.
- Desktop and VR shared one Windows target. Desktop now removes the OpenXR loader, while WindowsVR and Android receive a VR-first scripting define and active loader.
- The first real Desktop-to-Lab run exposed repeated native `InvalidArgument` input failures because the serialized adapter sent `desktop-tool` while native authority registered `research-tool`. Runtime identity is now resolved from the registered grabbable, covered by EditMode and PlayMode integration tests, and confirmed in the rebuilt Player.
- Final review found grip was also driving the valve and controller profiles were disabled. The adapter now reads trigger only from the controller selecting the tool, OpenXR profiles are enabled per target, and Desktop focus/world-pose/UI click-through guards have regression tests.

## Review, critic, alternatives and long-term result

**Review:** The available local software path is substantially stronger than the prior source-only state: real Editor tests, three Player builds, a Desktop Player journey, a WindowsVR no-runtime start and APK content inspection all passed. **Critic:** The Lab remains a foundation rather than a complete polished student product. Many XRI interaction, UI, accessibility, persistence/review and performance cases are only partial; physical headset/device, long-run performance, calibration, optical/tracer and experimental scientific evidence remain unavailable. **Alternatives/long term:** Continue on this branch/PR with official simulator journeys, complete UI and persistence flows, then run separately identified physical-device and experimental campaigns. Do not promote Research/Unsupported capabilities or infer hardware/scientific acceptance from software builds.

## Evidence boundary

The 95-case catalog is reported individually in `docs/evidence/acceptance-95.json`: 42 Passed, 49 Partial and 4 EvidenceMissing. Physical VR, Android install/thermal/power, participant comfort/accessibility, measured burette calibration, optical validation, tracer/mixing validation, publication performance and experimental scientific validation are not claimed. The PR must remain open and unmerged.
