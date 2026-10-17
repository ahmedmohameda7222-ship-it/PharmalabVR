# P10.02 local ergonomic preferences and actual Unity audio gain — source progress

**Functional source SHA:** `4fc012d0218dbb9418258fa0b79b3e5f2cda5e69`. Feature branch `feat/foundation-vr-first`, PR #1 open/unmerged. **P10.02 Open/NotReviewed.** No Unity Editor, PlayMode, Player, Android or real headset testing at this SHA. All 66 deferred Unity verification rows remain NOT_RUN. UI preference persistence is not Native session persistence.

## Source implementation

The existing immutable `LabSetupPreferences` (posture, dominant hand, bench height, text scale, captions, reduced motion, contrast, audio volume) and `SetupPreferencesSelection.Changed` were only static Boot→Lab state. New pure `SetupPreferencesCodec.Encode/TryDecode` round-trips all eight bounded choices in a schema 1 value, culture-invariant float encoding and strict 9-field delimiter parser. It rejects unsupported versions, NaN/Infinity, invalid/extra fields, unsupported posture/hand and out-of-range values. The decoding refusal retains safe defaults; no stored options are silently applied to chemistry.

`SetupPreferencesPersistence.Initialize`, invoked in real `BootMenu.Awake`, restores valid local PlayerPrefs values before `ScreenRouter.Configure` and subscribes to the shared immutable selection stream. Changes selected in Boot/Settings are written to PlayerPrefs only, across Boot scene unloading and Lab. Any preference I/O error logs a warning without affecting material authority or starting an unlicensed Native session. This is non-chemical local ergonomics; no Native stock/measurement/review/inventory/session bytes are stored here.

The selected audio slider previously changed only `LabSetupPreferences.AudioVolume`, not the actual Unity audio system. A pure `AudioPreferenceRouting.Gain` now feeds a live `AudioPreferencesOutput` MonoBehaviour, attached to Boot and the generated Lab scene in `BuildPipelineEntry.CreateLabScene`. `OnEnable` applies current preferences, and `Changed` updates `AudioListener.volume` during active usage. `OnDisable` unregisters. This is source wiring only; actual headphone/headset gain and Android suspend/relaunch remains to verify.

## RED/GREEN evidence

- Preferences TDD RED `dddaa5f1bb157c8593b6588351ab2207006c40f7`: [GitHub push 38823240529](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38823240529), `CS2001` missing production codec source. GREEN `0d283d9f98f39297339dd42f5c24d9b0d66dd35e`: [push 38823403731](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38823403731) Native Ubuntu/Windows plus .NET headless CI SUCCESS. Headless C# tested under fr-FR locale, all eight options, invalid schema, injected separators, NaN and blank defaults.
- Audio TDD RED `c271307f423efa587e2be9efa8b3adad3edbcd96`: [push 38823565693](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38823565693), `CS2001` missing production audio policy. GREEN `4fc012d0218dbb9418258fa0b79b3e5f2cda5e69`: [push 38823690022](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38823690022) and [PR 38823695265](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38823695265) had headless C# SUCCESS, Native Windows/Ubuntu still in progress when the checkpoint was written.
- Unity NUnit source `SetupPreferencesCodecTests` and `AudioPreferencesOutputTests` is authored for the deferred Unity 6000.3.25f1 campaign, NOT_RUN.

## Remaining P10.02 acceptance gaps

No actual Player restart/PlayerPrefs restoration, Unity audio route, slider-performance/K01 persistence I/O profiling, full accessibility render, real seated/left-hand reach, optional water practice, skip/replay, controller remap or hardware headset evidence. Complete those in code and gather final Unity XML/Player logs/screenshots/audio traces after the remaining phases are source-implemented. These are not silently labeled Passed. Downstream P10.03 Arabic/English production is also Open. P06.02 remains the formally selected dependency, P10.02 source is not certified, G-R1 Open, PR unmerged.
