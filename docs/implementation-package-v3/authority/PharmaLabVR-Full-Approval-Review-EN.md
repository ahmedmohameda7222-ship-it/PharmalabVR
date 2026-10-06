# PharmaLabVR — Full Plan Approval and Long-Term Review

Date: 6 October 2026. Reviewed: v2 foundation design, implementation plan, UX specification and earlier audit closure. This review introduces binding revision 2.1 corrections in those documents. No product code or headset tests were performed.

## Approval verdict

**Approved to begin foundation implementation, with the revision 2.1 corrections. Not approved as a finished scientific product or a universal chemistry simulator.**

The architecture is suitable for a continuing product. It separates material authority from graphics, scientific packages from procedures, and portability from device acceptance. These are useful long-term boundaries. The exact engine, scientific limits, fluid model, controller mapping and supported workload are provisional choices to be earned through tests. No review can guarantee that all later requirements will fit without changes.

The user's broader goal remains an open laboratory where students choose materials and actions freely. The first six-material aqueous profile is only its initial scientific capability. It does not yet satisfy arbitrary iodine/chloride combinations, temperature changes, precipitation or all laboratory behavior. Approving that foundation must not be presented as approving the complete universal goal.

## Decisions I approve

| Decision | Why it is useful long term | Qualification |
|---|---|---|
| One authoritative material core | Prevents separate inventories drifting between tools, rendering, solver and replay | Keep interfaces small; do not recreate Unity's complete physics engine |
| Scientific models/data rather than experiment outcome branches | New procedures reuse chemistry and state | Requires reviewed reaction coverage and validity bounds; missing chemistry stays unsupported |
| Versioned native interface | Enables isolated engine tests and consistent Windows/Android integration | ABI, buffers, lifetimes and build deployment need real tests |
| Unity/OpenXR client | Provides a practical device abstraction and interaction/rendering ecosystem | Runtime/extensions/input profiles and hardware still differ |
| Local offline science | Avoids making ordinary instrument response depend on network availability | Thermal/memory/latency constraints must fit actual devices |
| Independent observation statuses and provenance | Distinguishes scientific uncertainty from computing delay and research maturity | Apply statuses per observable, not just per vessel/package |
| Separate university procedures | Instructions and assessment do not override reaction behavior | Begin task-relevance feedback early, even before building protocol software |
| Capability-specific expansion | Limits scientific claims and gives new domains explicit acceptance gates | Future phase/kinetic coupling can require significant implementation changes |
| Early device and UX probe | Finds two-hand/control/reading problems before expensive content work | Actual participants and devices remain needed |

OpenXR's official purpose supports the portability direction, but it is not evidence that our app runs on every headset. [Khronos OpenXR](https://www.khronos.org/openxr/).

## New findings from this review

The previous statement that all 18 original findings had corrections was accurate as a mapping statement. It did not mean the design had no other gaps. This review found the following additional weaknesses and corrected the documents.

### A1 — Observation budgets were not connected to actual flow admission

**Priority: high.** A 0.05 ml unobserved-volume goal and 20 ms tick are not automatically compatible with a real tool. As a simple illustration, 5 ml/s delivers 0.1 ml in one 20 ms tick, before considering solve latency. This is arithmetic, not a measured burette rate. Checking lag only after committing a tick is too late.

**Correction:** Check proposed commits before their observation budget is exceeded. Accept a device/workload only after jointly testing calibrated flow range, quantum, actual scientific response and active-vessel count. Never conceal inadequate computation by silently changing the physical flow law. Protective holds remain exceptional; recurring holds fail release acceptance.

**Evidence pending:** Tasks 4/5/9 traces, including maximum accepted flow and simultaneous receivers.

### A2 — Solver isolation lacked state-reset and nonreturning-call contracts

**Priority: high.** Per-vessel instances alone do not prove that old solutions, phase definitions or selected output cannot affect new input. A bounded pending queue does not bound the duration of a native calculation. A thread watchdog cannot safely kill arbitrary native code.

**Correction:** Reconstruct complete requests or prove reset/import equivalence to a clean context. Test varied preceding solves, pinned engine/database identity, failure limits and shutdown. Keep view/menu/save independent of the worker; a nonreturning call does not offer a fake resume or unsafe context destruction. If controllable failure cannot be demonstrated, reject the selected deployment approach. Do not change engines silently within a session.

USGS documents retained model state and explicit management/reset mechanisms; that supports the need for adapter-specific lifecycle tests rather than assuming stateless calls. [IPhreeqc technical description](https://water.usgs.gov/water-resources/software/PHREEQC/IPhreeqc.pdf).

**Evidence pending:** Tasks 0/2/5 pathological-input, clean-context equivalence and lifecycle tests. Android deployment is still a build gate, not established by that documentation.

### A3 — A valid pH could imply unsupported color or volume

**Priority: high.** Package-level validity is too coarse for endpoint training. pH, optical color, density, local mixing and meniscus can have different coverage.

**Correction:** Apply support/provenance to each observable. Procedure eligibility requires every relevant scientific and instrument capability. Indicator assessment cannot publish merely because pH passes.

**Evidence pending:** Tasks 1/6/10/11 schema and publication-rejection cases, plus independent optical/calibration evidence.

### A4 — Native-core geometry ownership risked duplicating a physics engine

**Priority: medium.** Material ownership is correct; building a second broad collision engine in C++ would add complexity and disagreement with rendered geometry.

**Correction:** Use Unity's geometry/contact adapter with explicit coordinate/profile/revision/timestamp fields. Native logic validates samples and decides flow, amounts, capacity and conservation. Record committed geometry/model parameters for semantic replay. Use synthetic capture fixtures for headless tests.

**Evidence pending:** Tasks 1/4 calibrated geometry consistency and wrong-profile/coordinate rejection.

### A5 — Hold recovery and glass rendering needed stricter UX gates

**Priority: medium, potentially high for measurement tasks.** Parked virtual tools can separate from the student's physical hand. Simply resuming can cause a jump or unexpected action. Attractive transparent glass can obscure/distort a graduation or shift an indicator's apparent appearance.

**Correction:** Show parked tools and tracked controllers, explicitly align/re-grab before resuming, reset actuation baselines, and test two-hand recovery. Test actual glass/scale/meniscus/hue rendering on devices under the accepted lighting/display profile.

**Evidence pending:** Tasks 3/7/9 actual rendered screens and physical-device journeys. The written UX direction is approved for prototyping; the final interface is not visually or ergonomically approved yet.

### A6 — Persistence lacked a precise durability/recovery boundary

**Priority: high.** An atomic in-memory transfer does not imply durable storage. A simulation-time-only checkpoint may never fire while the simulation is held. A mobile process can be terminated without an exit callback.

**Correction:** Specify durable writes and atomic checkpoint replacement; Saved appears only on success. Schedule dirty checkpoints by simulation or wall time, whichever arrives first, plus explicit save and available lifecycle events. Forced termination may lose only events after the real durable boundary; report the recovered boundary, discard incomplete records and preserve prior valid state.

**Evidence pending:** Task 8 interruption, lifecycle, disk-error and recovery tests. This does not promise zero data loss after every hardware/process failure.

### A7 — Bounded queues were insufficient for total resource scalability

**Priority: medium.** Unlimited vessels, tools or engine contexts can exhaust memory even with one pending request each.

**Correction:** Publish measured limits for active vessels/tools/contexts/packages/import sizes per device profile. Reject work beyond them predictably. Persistence compaction and scientific packages also need measured memory/storage limits.

**Evidence pending:** Tasks 0/5/8/9 admission and long-session measurements. Scalability means a controlled supported envelope plus extension paths, not unlimited objects.

### A8 — Product relevance was scheduled too late

**Priority: medium.** A clean scientific engine can still miss the lab skills a university wants students to practice.

**Correction:** During Tasks 2–3, use the supplied titrations as task benchmarks and obtain early student/instructor feedback where available. Verify tool choice, preparation, reading, mixing and recovery needs. Keep protocol execution separate, as intended by the user. Document missing participants or measurements; the assistant cannot replace physical evidence.

**Evidence pending:** Early task-feedback record, Task 7 formative testing and future institution requirements. No educational effectiveness claim without a study.

## Alternatives and long-term judgment

**Unity versus another rendering engine:** Keep Unity as the current engineering choice. Changing render engines does not itself solve chemistry or measurement validity. Verify current platform/package compatibility in Task 0 and revisit the choice only if deployment, licensing or workload gates fail. Unity's release policy distinguishes LTS and update releases; LTS is a support tradeoff, not proof it is always the best new-project release. [Unity support policy](https://unity.com/releases/unity-6/support).

**IPhreeqc versus Reaktoro or a narrow custom acid/base solver:** Retain IPhreeqc as a candidate, not a permanent commitment. A custom narrow solver is useful as an independent reference and can be a limited production option if its coverage is explicit. It does not automatically improve long-term chemistry breadth. Compare alternatives on the same independent science, deployment, runtime and lifecycle cases before changing the primary engine.

PHREEQC's documented scope includes aqueous speciation and several phase/kinetic processes, which makes it a reasonable candidate for investigation. That does not prove arbitrary laboratory combinations or visible fluid/optical behavior. [USGS capabilities](https://water.usgs.gov/water-resources/software/PHREEQC/documentation/phreeqc3-html/phreeqc3-2.htm).

**Local versus remote calculations:** Keep normal interaction local. Optional future services can distribute packages or perform explicitly separate research tasks; network dependence should not enter ordinary control/observation loops without demonstrated benefit and an offline failure design.

**General physics versus simpler calibrated models:** Start with conservative parcels and evidence-backed reduced models. More detail is justified where it improves task accuracy within device budgets. Neither a universal molecular simulation nor decorative scripted effects is established as a better fit for this product.

**Long-term verdict:** The revised boundaries are defensible and extensible. The project will still need changes as kinetic/phase models, actual customer workflows and device constraints are learned. Keeping those changes isolated is the architecture's strength; guaranteeing no future rewrite would be dishonest.

## Four-stage decision

**Plan:** Approved sequence for Tasks 0–3 and conditional downstream gates, with revision 2.1 additions.

**Review:** The original 18 findings and new eight findings have specified corrections and task owners. Document consistency checks are separate from science/device tests.

**Critic:** Main residual risks are scientific coverage, data access, standalone solve latency, calibrated tool realism, optical readability and availability of physical validation. They can change engine/input choices and the published range. No fixed delivery schedule or cost is approved from these documents.

**Best approach/long term:** Approved architecture direction; provisional engines/models/profiles. Validate the riskiest pieces early and preserve the continuing foundation. Do not prebuild account/billing/cloud complexity before institutional needs are known.

**Final status:** APPROVED FOR FOUNDATION IMPLEMENTATION WITH GATES. NOT VALIDATED FOR RELEASE. NOT A PROMISE OF UNIVERSAL CHEMISTRY OR EVERY-HEADSET COMPATIBILITY.
