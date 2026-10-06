# PharmaLabVR — UI/UX Specification v2 (review revision 2.2)

**Binding interaction update:** `PharmaLabVR-Desktop-and-VR-Design-EN.md` specifies desktop pointer/keyboard controls and screen-space panels. The approved VR visual direction below remains in place; both modes share scientific observations and material state.

Date: 6 October 2026. Companion to the v2 foundation design. This specifies a prototype and its acceptance; it does not claim a finished visual design or tested ergonomics.

## 1. Student journey and screens

| State | Student sees and can do | Completion/recovery |
|---|---|---|
| Start | Enter lab, Continue session when available, Settings; active science-package summary | Select action; no required login after installation |
| Setup | Seated/standing, dominant hand, bench position/height, text scale; visible tracked controls | Confirm comfortable reach, or adjust/recenter |
| Orientation | Optional grab/release, water transfer, valve control, measurement-view and pause exercises | Skip or finish; available again from Help |
| Free lab | Physical bench, tools, labeled materials, rinse/waste, retrieval tray | Student chooses actions without a forced chemistry sequence |
| Inspect | Name/preparation, supported capability details, measurement view, relevant help | Close to return; no numeric reading answer hidden in a zoom tool |
| Updating | Small context marker on the affected vessel; inspect identifies older observation | Clears when current; no current-result grading while stale |
| Unsupported | Explain the missing modeled behavior and available next steps | Continue exploration with identified limits, dispose, or restart a saved branch |
| Calculation/device hold | Specific cause, tools parked, head/menu responsive | Catch up/recover, neutralize controls, explicit Continue |
| Pause/settings | Stable menu: Continue, Save, Recenter, Settings, Restart checkpoint, Exit | No ongoing material transfer; resume baselines are reset |
| Session review | Recorded actions and observations, separate Recompute action | Recompute identifies model/version differences |

Orientation teaches operation of the application, not a hardcoded experimental reaction. The free lab remains free after it. Setup defaults to seated configuration with adjustable bench position; the user may select standing. Dominant hand defaults right but is immediately changeable. Saved reach preferences are rechecked when the device/play space changes.

## 2. Spatial arrangement and visual direction

Credible, softly lit lab; restrained neutral surfaces; high-contrast opaque labels; clear tool silhouettes. Real material appearance is driven by supported models, not decorative reagent colors. No permanent wall of floating tooltips or numbers around the active work.

Three presentation layers: physical tools/materials; temporary context feedback for selected/affected objects; a stable world-space panel for settings and review. The panel spawns in front at an initial 1 m distance, is repositionable, and does not continuously follow the wrist or head. This is a prototype default to test, not a universal comfortable distance. Nothing blocks the source, outlet, meniscus or receiving vessel during delivery.

Primary menus use controller-ray selection. Bench work uses direct tool manipulation. Distinct cursors/highlights communicate those modes. Nearby menu actions do not capture a grip intended for a physical tool. Selecting a row never moves other actions beneath the pointer. UI animations are restrained; no camera movement added merely for style.

Meta describes interaction-role confusion and moving-wrist menu problems in its hand-input UI guidance; we adopt the principles without requiring its proprietary SDK or copying hand-target dimensions to all controllers. [Official guidance](https://developers.meta.com/vr/design/hands-ui-best-practices/).

## 3. Action vocabulary and precise controls

| Logical action | Behavior |
|---|---|
| Grab / Release | Grip action selects a tool body; visible selection and optional haptic confirmation; releases without an unintended chemical action |
| Actuate component | Trigger/select action when a highlighted valve/component is targeted; exclusive actuation until released |
| Inspect | Explicit selectable object/context action, not an unexpected menu on ordinary touch |
| Pause | Mapped menu action, always reachable without a large gesture |
| Retrieve | Select a distant shelf item into the delivery tray; then use direct manipulation |
| Measure view | Explicit magnifier/view action; preserves the reading task |

Device action mappings use OpenXR logical actions and displayed profile-specific help. They do not assume identical physical button labels.

Burette body grabs and stopcock actuation have separate targets. When stopcock actuation begins, record the pointer/hand baseline; horizontal controller displacement in a constrained plane maps to valve angle using an initially adjustable gain. Gain and hit-area size are locked in the accepted device input profile after user tests. It changes control sensitivity, not the valve's flow law. A mounted burette cannot also be accidentally dragged during valve actuation; unmount is a separate explicit action.

The visible stopcock rotates with the modeled actuator value. The other hand can hold/swirl the receiver. Closing the valve stops new outlet flow according to its model; an already hanging/in-flight droplet is accounted for separately. Larger hit zones cannot overlap the receiver or silently alter instrument dimensions. Grip and trigger-release sequencing, jitter and controller offsets are acceptance cases.

Pipette pumping uses explicit actuate input with visible liquid movement. It does not snap to a correct sample quantity. A pipette's preparation and calibrated drainage behavior are tool-model properties. Held instruments do not auto-move into a correct experimental arrangement.

## 4. Measurement and observation

Use real instrument graduations and a supported meniscus appearance. Measurement view enlarges the observed region/scale without recalibrating the tool or reporting the answer. Let users position their view for the required reading; do not silently remove parallax in an assessment of that skill. A training aid may explain reading technique on request, with its assistance recorded.

Label and scale acceptance is determined at actual working distances on both target device families. Prefer crisp text rendering and stable contrast, then test text scale/angular legibility rather than adopting a phone pixel size. [Meta rendering guidance](https://developers.meta.com/vr/resources/bp-rendering/).

Sample observations that are Pending show Updating. An old displayed color is explicitly the last observation, not certified current; the numeric inspect view clearly identifies stale data. Normal reference manipulation must meet the design's low-lag budget without protective holds. If users cannot understand this status, the interaction fails its acceptance even when the scheduler is technically correct.

## 5. Error and recovery copy

Messages are specific and localized. Representative English copy:

- Tracking: “Tracking paused. Your material is preserved. Put the controllers back in view.”
- ComputeHold: “Calculation is catching up. Transfer is paused. Close or release the control, then continue.”
- Unsupported: “This mixture is outside the supported model. Its chemical result is unavailable.”
- Missing package: “This session needs its original science package to continue. You can still view the recording.”

Only show actions that are actually available. No Try Again action for missing scientific knowledge; no student-error score for tracking/solver faults. Dismissal does not make an unsupported result valid. Dispose removes contents into a tracked waste sink. Restart checkpoint creates a recorded session branch, preserving the original history.

## 6. Comfort and accessibility

Bench height and position, not physical equipment scale, adjust to the student. Essential actions stay in tested comfortable reach. Retrieval eliminates required walking or excessive reaching. Recenter/teleport stops active material actuation and establishes new pose baselines; it cannot cause an instantaneous unintended pour. No smooth locomotion is required for core laboratory work.

UI status uses icons/text as well as color. Instruction audio has captions; interaction sounds are optional and confirm input, not the correct chemical endpoint. Text scaling does not change ml calibration. Contrast and selection-state tests include color-vision variants. Alternate chemical observation/assessment paths identify changes in the learning objective rather than claiming identical color-discrimination assessment.

Arabic needs a tested shaping/bidi/font implementation with mixed scientific symbols, decimal numbers and units. Store localized strings rather than baking text into tool textures. Language switching preserves material state. Test left/right hand, seated reach, interruptions and sustained operation. [Meta comfort guidance](https://developers.meta.com/vr/design/comfort/).

## 7. Early prototype and acceptance

Use 5–8 first-time VR/lab-interface users for formative tests, including seated and left-handed coverage where available. This small sample discovers usability problems; it does not prove educational effectiveness or universal accessibility. Obtain real device evidence for each claimed input profile.

Tasks: choose a labeled bottle; retrieve it; grasp/release a tool; operate a mounted valve while holding a receiver; read a graduation without an automatic answer; pause/recenter; understand an Updating/Unsupported/Tracking state; save and resume.

Acceptance: no unresolved severe interaction blocker in these tasks; no unintended material transfer caused by recovery/recenter; readable scales at the accepted working configuration; controls can be learned without the developer taking over; target devices meet observed transfer/latency limits. Record assisted versus independent completion, wrong selections, false activations, discomfort and incorrect interpretation of result status. Revise and retest the affected interaction rather than declaring an arbitrary success percentage from a small sample.

The first blockout contains simple bench/tools/panel/tray and the complete state flow above. Detailed meshes, glass shading and final scene styling follow these tests. This preserves an impressive final direction while finding fundamental interaction problems early.

## 8. Full-review corrections

When tools are parked during a hold, physical hands/controllers remain visibly tracked. Show where the tool is parked; resume through an explicit docking/re-grab or another tested alignment method. Resume does not jump a full vessel invisibly into a different pose or replay motion; reset actuator and pose baselines. A stuck native calculation offers only genuinely available save/restart actions, not a false Continue.

Support feedback identifies the observation that is unavailable: pH, color, density/volume or another quantity. A green pH status cannot imply a valid endpoint color. The capabilities panel and instructions distinguish these dependencies.

Test actual transparent vessel sorting, meniscus visibility, scale contrast, overlapping glass, lighting and apparent indicator hue on each supported headset. Attractive glass cannot distort the measured graduation or create an incorrect endpoint. Published scientific optical observations need a consistent display/lighting profile; UI color accessibility tests alone are insufficient.

Saved means durable completion, not merely a queued write. Recovery identifies its actual last saved boundary. Capacity/resource-limit messages explain the supported session limit without deleting material or silently reducing flow.
