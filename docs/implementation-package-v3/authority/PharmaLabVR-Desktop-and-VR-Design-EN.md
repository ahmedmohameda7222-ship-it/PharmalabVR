# PharmaLabVR — Desktop and VR Operation

Date: 6 October 2026. Revision 2.2. Binding extension to the foundation design, implementation plan and UX specification. User requirement: a normal laptop with mouse/keyboard must operate the laboratory without a headset. Existing VR visual direction remains approved; desktop adapts that lab rather than presenting a separate scientific product.

## Product and architecture decision

Ship a native Windows desktop lab and a VR mode using the same Unity scene assets, authoritative native material core, scientific packages, journals and session format. Desktop is a first-class product mode, not a development-only debug camera. The Windows desktop build must launch without a connected headset or installed VR runtime. Headset initialization is optional and explicit; unavailable XR cannot block desktop startup. Standalone headset builds remain separate platform builds of the same foundation.

DesktopInputAdapter and XRInputAdapter both produce the logical tool/input commands defined by the core. Camera movement, pointer hit testing and device mapping belong to presentation/input adapters. Material quantities, flow, mixing, science, capacities and conservation retain one authority. No desktop-specific chemistry or duplicated reaction code.

Desktop uses a monitor camera and a compact screen-space toolbar; VR uses the approved spatial panels. Shared information and actions include Inspect, Help, Pause, capabilities and observation status. Mode-specific layout does not alter measurement units or material state.

Windows mouse/keyboard is the first desktop target. Mac/Linux, touch and gamepad support are future explicit decisions, not implied by the word desktop. We will establish actual minimum laptop CPU/GPU/RAM requirements through measurements; no claim that every laptop can run it.

## Desktop interaction specification

Use a visible pointer, clear object/component highlights and context-specific help. Begin with a stable bench camera and an optional close-up instrument view. The camera remains user-controlled; no compulsory first-person walking or camera bob.

| Input | Initial behavior |
|---|---|
| Left click on tool body | Select and pick up an available tool; click an explicit Place action to release at the previewed location |
| Left drag while holding | Move the tool on a visible bench-parallel manipulation plane; show the height and placement preview |
| Mouse wheel while holding | Raise/lower the tool, bounded to the supported workspace; it does not simultaneously zoom |
| Q / E while holding | Continuously tilt the tool in opposite directions; show the orientation and stop tilt when released |
| Right drag | Orbit/look at the bench; held tool pose remains fixed in lab coordinates |
| Mouse wheel without a held tool | Zoom within readable, comfortable camera bounds |
| Left drag on highlighted stopcock | Capture valve component exclusively; adjust its physical actuator position with visible feedback |
| Esc | Pause and open the stable menu; reset actuation/input baselines before resume |
| Toolbar actions | Inspect, measurement view, help, retrieve, recenter view and save/pause |

Bindings are an initial mouse/keyboard design and may be remapped. Tool-body and valve targets do not overlap ambiguously. Picking up a tool does not teleport its fluid into another vessel. Placement preview must show whether a position is supported and collision-free; invalid placement keeps the held tool rather than deleting it or forcing a spill. Tilt and contact drive the same flow/capture model as VR. Never snap to a correct experimental quantity or automatically stop at an endpoint.

Precision tasks can use bench placement and explicit clamps/stands. The student places the receiver or mounts a tool before adjusting another component with the pointer. These supports do not silently move equipment into a scientifically correct arrangement. If a task requires simultaneous motion of both hands that desktop controls cannot reproduce, classify it as adapted or VR-specific rather than pretending equal motor-skill fidelity.

Pipette pump/control uses its explicit highlighted actuator. Inspecting or moving the camera cannot accidentally operate it. Help displays the currently relevant inputs, not every shortcut at once. A compact toolbar sits outside the principal working region; context cards do not cover graduations or outlet/receiver alignment.

## Scientific parity and teaching differences

Identical initial material plus identical committed operations and package identity must produce equivalent scientific observations within tolerance in either mode. Compare semantic events, not raw mouse versus controller trajectories. Tool models and conservation are shared; interface assistance is recorded.

Desktop teaches procedure, chemistry, instrument interpretation and consequences within the supported profile. VR adds spatial interaction practice. Neither mode alone proves transfer of real laboratory skills. A magnified view preserves graduation reading; it does not reveal the numeric answer. Record mode and assistance in session metadata and assessment evidence. Desktop mode does not make unsupported chemistry valid.

Switching between available modes requires pause, saved/shared state, neutral actuators and re-established input/pose baselines. Desktop can load an eligible VR session and vice versa without reinterpreting material. Incompatible task/input requirements are explained. Do not require live mid-pour switching in the first release.

## UI and accessibility

Preserve the approved neutral lab, restrained teal selection cues, readable labels and realistic material appearance. Desktop panels can use screen space for predictable mouse selection. VR panels retain their stable world-space layout. Both show per-observable support/freshness and separate compute/tracking/input faults from student errors.

Provide remappable bindings, text scaling, toggle-to-hold rather than mandatory sustained mouse grip, camera sensitivity and explicit reset-view control. Benchmark readability at 1280×720 and 1920×1080 with applicable UI scaling. These are test configurations, not a promise that every display is accepted. Selecting a UI action does not leak the same click into scene manipulation. Focus loss stops actuation/material advancement and resets inputs; returning requires explicit resume. Tools remain preserved in lab coordinates.

## Changes to the implementation sequence

| Existing task | Required desktop addition |
|---|---|
| 0 — environment | Build/run Windows without XR runtime or headset; record actual laptop specifications and rendering profile; keep Android/XR targets in the matrix |
| 1 — contracts | Shared logical input, mode/assistance metadata, exclusive component capture; adapters cannot bypass core material authority |
| 2 — science | Run independent scientific fixtures headlessly/on Windows now; headset absence does not block these checks |
| 3 — early interaction | First operational bench probe is mouse/keyboard: water bottle, receiver, burette, placement/tilt/valve, reading and pause. VR probe remains pending hardware |
| 4–5 — transport/scheduler | Same model/results; camera/UI changes cannot alter material; focus-loss and mode-transition recovery tested alongside solver faults |
| 6 — instruments | Distinguish pointer assistance from scientific accuracy and physical skill; retain calibration/mixing/optical evidence requirements |
| 7 — UX | Build and test actual desktop journeys and laptop scale/glass readability; conduct physical VR tests when hardware becomes available |
| 8 — sessions | Round-trip sessions across desktop/XR adapters; record mode/assistance; no dependence on one device's raw tracking state |
| 9 — performance | Separate desktop and headset acceptance profiles; desktop provisional goal 60 FPS on the measured target laptop at its accepted settings. Measure science latency and material accuracy as well as frames |
| 10 — publication | Desktop can be accepted independently after its own gates. VR remains EvidenceMissing until physical validation; no inference of headset success from desktop results |
| 11 — protocols | Declare input-mode requirements and assistance/assessment limits per task; no separate desktop chemistry implementation |

We can proceed with desktop builds, chemistry, core contracts and mouse interaction without owning a headset. Headset rendering, control mapping, tracking recovery, comfort and device timing cannot be fully validated on a monitor. The architecture preserves those tests rather than pretending they passed.

## Acceptance cases

1. Clean Windows launch with no XR runtime/headset reaches a usable lab; failure to initialize optional XR cannot crash/block it.
2. Pick up, move, tilt, pour, place, rinse and dispose using only mouse/keyboard; all material balances and capacities remain correct.
3. Rotate/zoom the camera, open a panel or move over UI while holding a tool: no unintended tool movement/actuation/material transfer.
4. Operate burette stopcock with receiver placed by the user; read its scale through the measurement view without automatic volume/end-point answers.
5. Lose window focus mid-actuation: freeze safely, preserve material, clear stale input and resume explicitly without a catch-up pour.
6. Replay matched committed operation fixtures through each available adapter: equivalent scientific outcomes and conserved pools. Adapter-only tests cannot substitute for real headset tests.
7. Save/load shared state across modes with assistance metadata intact; unavailable mode or task requirements produce a truthful explanation.
8. Measure laptop frame times, native memory, solver delay and observation-volume budgets; low graphics settings cannot alter chemistry or calibrated tool geometry.

## Four-stage review

**Plan:** Add desktop startup/input/panels early; use the user's current laptop for the first working lab while retaining future VR gates.

**Review:** One core and shared scientific state prevent divergent chemistry. Optional XR initialization makes no-headset operation a real acceptance case. Desktop recovery and session metadata are explicit.

**Critic:** One pointer cannot reproduce every two-hand VR task. Camera/tool input conflicts, screen-size reading and graphics limits need testing. We must not claim that mouse manipulation provides identical physical skill practice or that unspecified laptop hardware is sufficient.

**Alternatives/long term:** Recommend native desktop plus VR adapters in the existing Unity architecture. A separate browser laboratory would add another renderer/input stack and native integration path; it is not needed for this laptop requirement. A VR emulator alone would not provide the accessible ordinary-computer product requested. Shared assets/core with mode-specific interaction is the simpler continuing design.

**Decision:** Desktop plus VR replaces the previous VR-primary access assumption. Desktop implementation can begin without a headset; physical VR acceptance remains pending. This update changes the plan and does not itself implement the application.
