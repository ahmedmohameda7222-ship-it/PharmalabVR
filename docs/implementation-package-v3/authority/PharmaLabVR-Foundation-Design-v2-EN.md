# PharmaLabVR Foundation Design — v2 (review revision 2.2)

**Binding access update:** `PharmaLabVR-Desktop-and-VR-Design-EN.md` adds first-class Windows mouse/keyboard operation with optional XR startup and the same material/scientific core. It takes precedence over VR-only access or hardware sequencing assumptions below.

Date: 6 October 2026. Status: revised design for implementation review. This document and the v2 implementation plan supersede the v1 Arabic design/plan. The English audit remains the record of why this revision was needed. The companion UX specification is part of this design.

## 1. Purpose and honest readiness

Build an open VR aqueous laboratory that students can manipulate freely, with behavior calculated from scientific state, models and reviewed data. Later, university instructions consume the same state without imposing scripted reaction outcomes. Initial code is the continuing product foundation, not a disposable MVP.

The engineering decisions below resolve the audit's design gaps. They do not establish real scientific accuracy, headset compatibility, fluid calibration or educational effectiveness. Those require the plan's acceptance gates. Work can begin with environment, reference and early device/UX probes; a capability cannot be published merely because its implementation exists.

No product code, engine benchmark, headset test or rendered UI was produced during this revision. The assistant leads the four reviews and scientific research; external expert availability is not a prerequisite to starting. Missing evidence remains explicit.

## 2. Choices and boundaries

| Area | Decision |
|---|---|
| Application | Unity 6.3 LTS, C#, URP, XR Interaction Toolkit; exact compatible patch/packages locked during setup |
| Headsets | OpenXR; vendor-specific integration isolated; initial acceptance targets Quest 3, PICO 4 Ultra, Windows PC VR |
| Science | Independent C++17 core, IPhreeqc first candidate; compare Reaktoro if it fails a required gate |
| Operation | Local/offline after content installation; no required cloud solver or LLM chemical predictions |
| Scientific expansion | Reviewed, versioned packages; one authoritative state; separate protocols later |
| First scientific profile | Homogeneous aqueous fast-equilibrium research: water, HCl, NaOH, NaCl, acetic acid, sodium acetate |
| Subsequent profiles | Calibrated local mixing/indicators, then separately validated solids, precipitation, gases, energy and redox |

First research conditions: 298.15 K, 101325 Pa, stock points 0.01/0.05/0.1 mol/L. These are a test matrix, not an approved operating range. Realistic atmosphere exchange and heat generation are excluded until modeled and validated. Inactive heating controls cannot imply support. The UI identifies the active capability package.

Scientific formulas/data are necessary knowledge, not hardcoded experimental answers. All substances and conditions cannot be universally predicted. Initial hardware targets are untested targets, not purchasing recommendations or a promise that every headset runs the application.

## 3. Ownership and conservation

One native StateExecutor owns material state, transfer commits, tools' fluid inventories, phase inventories, conservation checks, capacities and the journal. Unity owns display poses and input sampling; it never decides the final available quantity or overflow from a stale snapshot. A separate SolverWorker receives immutable scientific requests and returns results; it cannot mutate material state. IPhreeqc contexts are isolated per vessel and accessed only by that worker.

State distinguishes:

- Conserved element totals and restricted reaction-family pools, such as acetate carbon distinct from inorganic carbon until a validated connecting pathway exists.
- Species distributions and phase/solvent inventories, which may change during reactions.
- Tool geometry, calibration and actuator state.
- Scientific observations, their provenance and the state to which they apply.

Pure transport preserves the selected parcel composition and conserved totals. Reaction commits may change species, water and phase allocation while satisfying the appropriate conserved balances. A species is not assumed conserved simply because it was present in an original bottle. No second chemical inventory is maintained in Unity. In the first constant-solvent, homogeneous equilibrium profile, conserved pools and solvent define material authority; asynchronous species results are derived observations only. A stale solve can be retained as a historical observation but cannot overwrite current material. Future kinetic/multiphase reaction commits must match all touched input revisions atomically, or be rejected and recomputed; their coupling and time advancement require a separate validated design before enabling those profiles.

The initial fast-equilibrium profile may use a documented constant-solvent/additive-volume approximation only within its tested error envelope. Adding kinetic or multiphase models requires a reaction-state commit that updates solvent/phase inventory consistently; no approximation carries over silently. PHREEQC supports phase redistribution and water-producing/consuming reactions, but the chosen adapter must implement the intended semantics. [USGS capabilities](https://water.usgs.gov/water-resources/software/PHREEQC/documentation/phreeqc3-html/phreeqc3-2.htm).

Internal units: mol, kg, m³, s, K, Pa; scientific numbers are double. No implicit molarity/molality conversion, no averaged pH, and no universal pH clamp to 0–14. Geometric rendering and capacity use the same accepted volume model. If that model cannot support a composition, calibrated volume-dependent transfer is unsupported rather than invented.

## 4. Revisions, input and transport

The session has a monotonically increasing eventSequence for ordering and replay. Each vessel has materialRevision, incremented on its material/environment/mixing changes. Tools have actuatorRevision and inventoryRevision. Changes in vessel B do not invalidate an otherwise matching result for vessel A.

Live controller sampling supplies ToolInput intents: sampleId, toolId, measured pose, actuator input, tracking validity and monotonic capture time. The latest input sample replaces unconsumed pose samples; committed transfers never disappear. The core produces actual material transfers from the latest valid sample at each transport tick. A recovered frame never replays accumulated motion/time as a catch-up pour.

Discrete edits and test/replay transfers carry expectedMaterialRevisions for touched vessels. A stale discrete request is rejected without mutation. Live intent is evaluated against current state, not blindly retried as an old fixed quantity. The journal records what was committed and the selected input/model parameters.

TransferEnvelope has source, sourceRegion, selection, quantity, intendedDestinations, overflowSink and commandId. Initial supported selection is HomogeneousAqueousLiquid, with quantity basis WaterMassKg or LiquidVolumeM3. The latter requires a supported volume/density model. A parcel includes its solvent and restricted components. Solid, gas and selective-phase variants are explicitly UnsupportedOperation in v2; future schema versions must preserve their event meaning through migrations.

Intended destination fractions describe geometric capture, not guaranteed receiver capacity. StateExecutor evaluates available source and current capacity, produces actual delivered parcels, and routes excess to the explicit overflow sink in one atomic commit. Live rates are bounded by actual available material; an oversized discrete fixed transfer is rejected. Duplicate commandId with the same payload returns its recorded receipt; the same id with a different payload is rejected. An unrelated session event cannot create a false transfer conflict.

Example: two queued 5 ml captures target a vessel with 7 ml remaining. At commit, 7 ml total enters the vessel and 3 ml enters the spill sink, assuming the accepted additive-volume fixture. The source loses 10 ml equivalent; no material is destroyed or credited twice.

## 5. Result and package semantics

These are independent fields:

| Dimension | Values |
|---|---|
| SupportLevel | Validated, Approximate, Unsupported |
| ComputationState | Ready, Pending, Failed |
| ResultFreshness | Current, Stale, Absent |
| PackageMaturity | Research, Published, Retired |

Each observation carries vesselId, asOfMaterialRevision, modelId, packageHash, dependencyHash, units and support bounds. A known validated model can have Pending computation. Research does not mean published; Failed does not mean chemically unsupported.

Results are checked against their immutable request and model identity. newestSolvedRevision advances monotonically. A result for an intermediate committed revision is retained as that revision's observation even if newer transfers exist; it is not incorrectly relabeled Current. The UI marks a retained observation Updating while it is stale and excludes it from assessment as a current result. This avoids discarding every solve during continuous input. A result older than newestSolvedRevision cannot overwrite it.

Current means the observation's dependencies match current authoritative state. Restoring a checkpoint also restores package/model identity. Observations from another package cannot silently apply to the current one.

## 6. Scheduling and responsiveness

Three schedules are separate: headset rendering/tracking at the supported refresh rate; authoritative transport at an initial 20 ms tick; scientific solving when relevant state changes. No assumption requires a full solve every transport tick. Transport convergence is tested at 10/20/40 ms; kinetic solvers may use adaptive internal steps later.

For the initial explicitly fast-equilibrium profile: one in-flight solve and one replaceable pending immutable request per vessel, serviced round-robin. Coalescing pending equilibrium requests preserves all committed material events and calculated endpoints; it does not promise to reproduce unobserved intermediate color transitions. It is accepted only within the observation budget. Unchanged dependency hashes reuse a result. Different restricted paths, kinetics and future energy models cannot use this coalescing rule without separate proof.

A profile declares observation budgets. Initial engineering defaults for prototype testing:

| Interaction | Age budget | Unobserved delivered-volume budget |
|---|---:|---:|
| Burette/precision liquid delivery | 100 ms | 0.05 ml, tightened to instrument/task limits before publication |
| Bulk aqueous transfer | 200 ms | 0.5 ml, tightened to accepted task limits |

Both constraints apply; these are prototype acceptance goals, not scientific constants. For each affected vessel, the executor measures cumulative material-changing transfer volume since that vessel's last solved dependency state, counting ingress and egress without netting them away. A closed circulation cannot hide observation lag. Relevant non-transfer changes also invalidate dependencies. Request-to-publication latency and simulation-state age are measured separately; idle observations do not become stale just because wall time passes. Unsupported mixing behavior cannot be accepted by meeting the timing budget alone.

If a task budget or the bounded solver queue cannot be maintained, enter ComputeHold once: stop simulation-time/material advance, retain committed events, show the cause, and keep head tracking/menu controls responsive. Park held tools at their last committed pose, clear unconsumed input and do not auto-toggle back to pouring. Recovery requires a caught-up valid state, neutral/released controls, fresh pose baselines and an explicit Continue action. Pending overload is not a student mistake. Standard published workloads must finish without ComputeHold; it remains a protective failure path. Repeated holds fail performance acceptance rather than counting as successful operation.

Actual rendering can show current liquid geometry and a previous observation only with a visible Updating marker identifying its freshness; the inspect panel does not present the old numeric value as a current measurement. No stale result is silently graded. UX testing must confirm this is understandable, and normal published operation must meet the low-lag limits.

## 7. Science and material publication

The first candidate package is reviewed from minteq.v4.dat using a necessary-species allowlist and explicit restricted pools. Retain compatible thermodynamic/activity definitions; do not force values to an unrelated ideal fixture. Independent ideal reference calculations are numerical checks, not an assumed IPhreeqc ideal-mode configuration.

The capability evidence matrix includes composition and ratios, stock concentrations, ionic strength/model applicability, temperature, pressure/atmosphere assumptions, volume approximation, measured quantity convention, uncertainties, and interpolation/extrapolation policy. No broad range is inferred from three successful stock points. Unsupported or extrapolated conditions remain identified.

Reference fixtures include acid/base curves near equivalence, mixed HCl/acetic acid, buffer ratios, dilution, contamination, and material transfer. Ideal pH agreement target is 0.001 under matching assumptions. A realistic 0.05 pH target is an initial project goal requiring suitable independent data and uncertainty assessment; it is not an established validity claim. Indicator color, delivered volume, local mixing and endpoint detection have separate acceptance reports.

Calibration/fitting data and held-out verification data are distinct when fitting occurs. Lack of experimental data does not block headless contracts, but does block publishing the affected scientific capability. MIX's redox equilibrium and heat/volume approximations cannot be assumed to model all lab behavior. [USGS MIX documentation](https://water.usgs.gov/water-resources/software/PHREEQC/documentation/phreeqc3-html/phreeqc3-27.htm).

## 8. Fluid tools, local mixing and atmosphere

Each tool has a defined model, geometry and calibration; burette tip fill, hanging droplets, pipette residuals/drainage, overflow and rinse are material operations. Controller hit areas may be forgiving without changing calibrated geometry. The homogeneous reference comes first. A three-region mixing candidate is evaluated against tracer/mixing data and region/timestep convergence; its coefficients are not assumed realistic before evidence.

Indicators require known preparation, solvent, concentration, species behavior and optical observation model. Two indicators are not last-added-color selection. Trace-indicator approximations need bounds. A polished unsupported color animation cannot become a valid endpoint cue. Atmosphere/heat limitations appear in the capabilities panel, and educational content cannot require an excluded phenomenon.

## 9. UI/UX and accessibility contract

Use the companion UX specification's screen flow, action vocabulary, tool interaction, measurement view and recovery states. Prototype those early using simple final-architecture assets and clearly labeled water/reference fixtures. Final scene assets come after the instrument and student-journey gates.

Bench reach and position, seated/standing, dominant hand, text scale, sound/captions and retrieval tray are configurable. The physical size/calibration of instruments does not change when menus or labels scale. Accessible UI status has text/icon cues. An alternative way to observe an indicator can change the assessed skill; this is recorded rather than claiming equivalence. Arabic shaping, mixed-direction units and font coverage require tests on the selected text implementation; English fallback is not Arabic support.

## 10. Sessions and resource limits

Local checkpoint every 30 s simulation time and on pause/save/exit. Persist a journal of committed semantic events, model parameters and scientific observations needed for playback; never every rendered frame by default. Import validates everything into a new empty context before publishing it. Playback displays recorded observations; recompute uses pinned models or explicit migration and comparison.

Per-vessel pending solve slots are bounded as above. Command identities are `(branchId, commandSequence)` in an ordered discrete channel. A checkpoint records a terminal-outcome watermark and identity/payload digests; terminal outcomes distinguish committed, rejected and unknown commands. A pruned receipt returns AlreadyCommitted only when the persistent identity index proves commitment; rejected or unknown commands cannot be silently credited or re-executed. Conflicting historical payloads are checked where their digest is retained; unavailable historical verification is reported explicitly. Branching creates a new branchId, not a false reversal of chemistry. The implementation plan specifies crash-safe persistence and resource measurement.

Checkpoint compaction does not delete history required for the selected replay mode. If full historical observations are pruned after explicit retention selection, the UI labels reduced playback coverage. Storage limits are measured and recorded in the device acceptance profile before publication. Oversized/corrupt input is rejected without changing the current session. Missing packages allow recorded playback but prevent misleading live continuation. Updates happen between sessions with identity/integrity checks; formal distribution adds publisher signature verification.

## 11. Device and performance acceptance

Build and profile early, then repeat acceptance on exact release builds: 1/4/10 active-vessel traces; three 30-minute runs after five-minute warm-up; supported native refresh rate at least 72 Hz, preferred 90 where viable; p95 CPU/GPU work each within 80% frame budget and p99 within it; under 1% dropped frames in the stable workload. These are project goals. Measure scientific state age, unobserved volume, latency distribution, queue fairness and ComputeHold count as well as FPS.

No uncontrolled memory growth in long sessions or repeated create/load/unload. Compare scientific outputs across Windows/Android within accepted tolerances, not bitwise equality. Missing hardware is EvidenceMissing, not a simulated device pass. Input/readability failure prevents acceptance of that task/device even if its chemistry benchmark passes.

## 12. Acceptance and four reviews

Each task closes with Plan, Review, Critic and Alternatives/Long-term findings. Decisions can be DesignResolved while measurements are PendingEvidence; neither means runtime Passed. The full product expands through capability-specific plans without changing this separation.

The v2 plan names the implementation work and tests. The audit-closure matrix maps every R/U finding to a decision, owning task and still-needed evidence. This revision is ready for review and the first implementation phase, not a declaration of scientifically validated software.

## 13. Binding corrections from the full approval review

These corrections take precedence over less specific wording above. Their owning tests and remaining evidence are in `PharmaLabVR-Full-Approval-Review-EN.md` and the implementation plan's review addendum.

1. **Observation admission:** Check a proposed transport commit against each affected vessel's observation budget before committing it. If the next commit cannot satisfy the profile, hold before crossing its limit; a later warning does not repair an already exceeded budget. Measured solve latency, full calibrated flow range, transport quantum and active-vessel count define the accepted workload. Do not silently reduce physical flow or rewrite its calibration to conceal inadequate computation. If a released flow range cannot pass, revise scheduling/model/hardware support and retest. A 20 ms tick alone does not guarantee a 0.05 ml limit.
2. **Solver state and faults:** Fast-equilibrium requests must be reconstructed from complete immutable inputs, or reset/imported with demonstrated equivalence to a clean context. History, prior phases and prior output rows cannot contaminate a request. Same inputs under different preceding solves must match within tolerance. Pin engine/database/adapter versions. A watchdog can diagnose lateness but cannot safely terminate an arbitrary native call. A nonreturning call must never block view/menu/save or trigger unsafe context destruction; no Continue is offered until a real recoverable state exists. Test the chosen engine's iteration/failure controls and lifecycle; if bounded failure and shutdown cannot be demonstrated, fail its deployment gate. No silent fallback to another engine within a session.
3. **Observable-specific coverage:** Support, freshness and newest-solved tracking apply per observation/model, such as pH, color, density/volume or meniscus. A pH solve cannot advance color's solved revision. A valid pH does not validate color. A protocol declares every scientific and instrument capability it needs; publication cannot infer these from a single package-level green status. Solver calls hold no executor/session/save locks; immutable requests and a bounded completion channel keep a blocked worker from locking authoritative snapshots or persistence.
4. **Geometry boundary:** Unity supplies versioned, validated pose/contact/capture geometry through an adapter; the core owns quantity, flow model, capacity and commits. Geometry samples identify the calibrated asset/profile, coordinate frame, timestamp and relevant tool revisions. Journal committed capture/model parameters for replay. Do not build a second general collision/render engine inside C++. Core fixtures use explicit synthetic geometry; rendered glass/meniscus require separate optical tests against the observation model.
5. **Durability:** A committed in-memory event is not necessarily durable. Save reports success only after the chosen platform's durable-write/atomic-replacement steps complete. Checkpoint scheduling uses the earlier of 30 simulation seconds and 30 wall-clock seconds while dirty, plus explicit save and available lifecycle callbacks. Exit/suspend callbacks are best-effort; forced termination may lose events since the last durable journal/checkpoint boundary. Expose that recovery boundary honestly, retain complete valid records only, and test interruption at write/replace points.
6. **Resource admission:** Bound live vessel/tool/context counts, input sizes and scientific-package sizes as well as queue slots. Derive initial device limits from the 1/4/10-vessel tests and publish the limits before release. Reject additional work with explanation before exhaustion; unlimited context creation cannot be called scalable.
7. **Product relevance:** Obtain early feedback on the tool/readout journeys and the supplied titration learning objectives during Tasks 2–3. This does not add procedure scripting to the core. It prevents completing a scientifically interesting engine whose interaction does not serve a real lab skill. Access to measurements and physical headsets is an implementation dependency, not something the assistant can replace with confidence.
