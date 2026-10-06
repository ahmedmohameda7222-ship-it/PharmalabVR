# VR-first scene and UI/UX specification

## Visual identity and scene composition

Reference: `visuals/approved-vr-concept.png`. Preserve its neutral credible lab, restrained teal selection, simple stable side panel and visible scale/valve/receiver. It is illustrative: repair repeated/incorrect numbers, bottle caps and other generated geometry; do not treat image pixels as instrument calibration. Do not use the flattened concept as the application's background scene.

Use metres. Default bench: width 1.8 m, depth 0.75 m, height 0.80 m adjustable 0.65–1.05 m without resizing instruments. Default lab frame origin at bench centre/front floor projection; working surface covers x −0.9…0.9, z 0.25…1.0. Setup lets the user move/rotate the whole bench relative to their tracked floor. Wall/shelf assets remain comfortably reachable through a retrieval tray. Prevent placement through the bench and provide clear preview. Units on equipment are ml; native quantities remain m³/mol/kg.

Place burette stand left of centre (x=−0.35), working receiver centre (x=−0.05), stock tray right (x=0.45), rinse/waste at right rear (x=0.70), retrieval tray front right. Initial rigid model dimensions: stand base 0.18×0.14 m; 50 ml burette measuring section 0.012 m internal diameter implies about 0.442 m liquid-column length under the cylindrical research geometry. Include tip and stopcock separately; ticks 0–50 ml increase downward and invert readings correctly. A 25 ml graduated pipette and 250 ml conical receiver are separate calibrated assets; vessel capacity differs from labelled measuring graduation. Label all synthetic research geometry and validate physical tool calibration before publication.

Use analytic mesh generation from explicit instrument profiles for cylinders, necks, tapered sections, ring bases and graduations; do not stop at Unity cubes. `InstrumentMeshBuilder` creates meshes once in the editor; `BuildLabAssets` writes persistent prefabs/materials/profiles and `.meta` GUIDs. Source-generated assets avoid unlicensed downloaded meshes. Provisional shape profiles are research geometry, not calibrated physical manufacture. Serialize built scenes; a runtime-only builder cannot substitute for an inspectable scene/prefab setup.

Color tokens: bench #D9E0E3; panel #F4F7F8; text #132D3D; secondary #516574; teal #168D96; warning #9B621B; error #A63737. Use opaque labels, readable SDF text, icons plus text status, soft baked environment with restrained realtime key light. Colorless stock solutions stay colorless. No permanent floating pH/volume answers. Keep menus away from meniscus/outlet/receiver. No camera shake, neon reagent bottles or decorative explosions.

URP forward rendering, conservative transparent glass with measured overdraw. Begin 2× MSAA on Android, 4× on PC; bounded adjustable render scale 0.8–1.0 initially; no mandatory bloom/motion blur/refraction. Implement liquid surface/meniscus using geometry compatible with stereo, liquid level derived from native reference volume and asset shape; clip to vessel interior and preserve separate tip/droplet inventory. A mobile-safe glass variant may omit screen-space refraction if it corrupts reading/performance. Lighting/display affects endpoint appearance, so hold optical claims at Approximate until independently validated.

## Scenes and root objects

`Boot.unity`: configuration, local save discovery, capability/package checks, start menu and optional XR initialization.

`Lab.unity`: `LabRoot`, `BenchRoot`, `ToolRegistry`, `VesselRegistry`, `CoreDriver`, `LabGeometryAdapter`, `XRPlayerRig`, `DesktopPlayerRig`, `LabPanels`, `AudioFeedback`, `SessionController`, `PerformanceRecorder`. Exactly one gameplay adapter is active. `Review.unity`: session journal/observations and explicit Recompute action; no hidden migration.

Boot flow on PC: primary action Enter VR (available if runtime/device initialized), secondary Enter desktop. Missing runtime/device explains VR availability and enables Desktop; it does not initialize XR unconditionally or exit. Android headset build enters VR setup directly; no compulsory login. In Editor provide separate Play mode options XR Simulator and Desktop. XRI simulator is development-only and excluded from production standalone builds; its evidence is labeled SimulatedXR.

Use `XRSettingsController` to disable automatic initialization on PC and explicitly initialize/start only the selected mode. On failure cleanly return to mode selection. Switching modes occurs only paused with neutral tools and reset baselines; no mid-pour switch. Avoid a proprietary vendor SDK unless required for an explicit build/runtime capability; isolate that feature.

## XR interaction

Use XRI 3.3.2 controller/action-based interactors. Grip selects a tool body; trigger actuates a highlighted valve/pump component; direct and ray interactions have distinct UI/collision layers. XRI attachment/transforms propose input but cannot change liquid amounts. XRI built-in throw motion is disabled for liquid tools during initial research tasks; release places/drops through the defined geometry path, with explicit spill accounting instead of teleporting mass. Two controllers can independently own receiver and valve; component actuation cannot simultaneously drag the mounted burette.

Stopcock actuation captures the selected component until trigger release. Record control baseline; constrained horizontal hand displacement maps 0.10 m to 90° actuator rotation, clamped to the profile stops. Gain/hit areas are configurable input settings to test, not flow-law calibration. New outlet flow stops when closed; existing hanging droplets remain represented. Clear target highlight, haptic confirmation where supported and minimal sound accompany capture/release. Menus never seize tool grip based on hover alone. Hand tracking is not required in this release.

Ray menu selection uses explicit trigger click; physical bench uses direct grip. UI layer cannot act as a receiver/capture sink. Trigger/grip events consumed by UI do not propagate into tools. Supported comfort defaults: seated setup first, dominant hand selection, bench placement, retrieval tray, snap turn (30°) and teleport if navigation is needed; no smooth locomotion requirement. Recenter freezes actuation, preserves inventory and remaps pose baselines. Do not assume devices share physical button labels; show profile-specific controls.

## Desktop interaction

Implement the exact desktop map in the revision 2.2 extension: select/pick, drag on manipulation plane, wheel for held height, Q/E tilt, right-drag camera only, wheel camera zoom when empty, exclusive stopcock drag, explicit Place, Esc pause. Camera transforms do not move lab-held tools. Receiver placement/clamps allow one pointer to operate a burette without automatic correct positioning. Use shared geometry/core/tool profiles. Context toolbar shows selected-object actions; numeric auto-dosing and endpoint snapping are absent. Remappable controls and toggle-to-hold are required.

## Screens, panels and states

| Panel/state | Required controls and behavior |
|---|---|
| Mode/start | Enter VR / Desktop, Continue eligible save, Settings; package identity and Offline status |
| Setup | Seated/standing, left/right hand, bench placement/height, text scale, sound/captions; confirm comfortable reach |
| Optional orientation | Grab/release, water pour, mount/valve, scale reading, pause/recenter; Skip and replay via Help |
| Free lab | Quiet physical workspace; contextual selection; stable side panel Inspect / Help / Pause |
| Inspect | Preparation, unit/concentration convention, package/model limits, observable support, measurement view |
| Measurement view | Magnified instrument region preserving scale/parallax objective; no numeric volume answer |
| Updating | Affected-vessel marker plus “Last observation — updating”; numeric readouts label stale revision |
| Unsupported | Identify unavailable quantity/phenomenon; supported exploration/dispose/checkpoint branch options only |
| Hold | Distinguish Tracking / Input focus / Time discontinuity / Calculation; material preserved; real recovery actions |
| Pause | Continue, Save, Recenter, Settings, Restart checkpoint, Exit; no ongoing material advancement |
| Review | Event list, recorded observations, mode/assistance, model identity and separate Recompute |

VR panel initial width 0.42 m at roughly 1 m, repositionable, stable world placement (not wrist/head locked). Use 0.032 m default menu text height and 0.045 m button height as prototype starting geometry, then validate angular legibility and actual controller hit areas. Desktop panel default 360 px at 1080p with minimum 44 px targets and 18 px body text; scale for 720p/high-DPI, not instrument graduations. These are design defaults pending device acceptance, not universal ergonomics.

Recovery: show tracked physical hands/controllers and parked tools. For held vessels, require visible docking/re-grab/alignment before Continue; reset actuator to neutral and pose baselines without copying old movement. Engine stuck: permit save/restart only when available, never show a fake Continue. Outside coverage: scientific quantity unavailable, not Try Again until it invents an answer. Chemical mistakes such as overshoot/spill remain actual material state, not auto-corrected.

## Localization and assistance

English and Arabic UI strings live in JSON string tables, not textures. Use a pinned OFL font family with Arabic coverage and its license; render through a tested shaping/bidi adapter with SDF atlas. Plain reversing strings is prohibited. Verify mixed formula/decimal/unit labels and language changes without resetting session. If shaping cannot pass on a target, that target/language is not accepted; do not claim English fallback is Arabic support. Audio guidance has captions, UI status is not color-only. Alternative scientific observation paths record changed assessment goals; they cannot be called identical color-discrimination skills.

## Visual acceptance evidence

Screenshots must come from actual implemented scenes. Required views: VR stereo/simulator bench; desktop bench at 720p/1080p; stock selection; two-tool action; scale/meniscus close-up; inspect support; pause; unsupported; tracking/focus recovery; saved/review. Record display/profile/build and simulated versus physical. Automated tests can check references/layout/state but not substitute for human ergonomic reading. 5–8 formative participants where available, including seated/left-handed use; record assists, missed selections, accidental actions, reading errors and discomfort. No fabricated participant results.
