# PharmaLabVR Implementation Plan — v2 (review revision 2.2)

**Binding execution update:** Apply `PharmaLabVR-Desktop-and-VR-Design-EN.md` to Tasks 0–11. Start the operational bench probe on Windows using mouse/keyboard without a headset; retain real VR acceptance as PendingEvidence. Desktop and VR share science, material authority and sessions.

Date: 6 October 2026. Status: planning complete for the initial foundation phase; implementation and acceptance evidence pending.

**Goal:** Implement the continuing open VR lab foundation described in the v2 design, with model-driven aqueous behavior, offline operation, truthful capability limits and early headset/UX validation.

**Architecture:** Unity/OpenXR presents interaction; an independent native core owns material and transport; asynchronous scientific adapters produce observations from immutable state. University procedures later consume this state.

**Stack:** Unity 6.3 LTS/C#/URP/XR Interaction Toolkit/OpenXR, C++17 native core, IPhreeqc first candidate. Lock actual compatible versions in Task 0. Reaktoro is a gate-triggered alternative, not a second simultaneous production solver.

**Authority:** Read `PharmaLabVR-Foundation-Design-v2-EN.md` and `PharmaLabVR-UX-Spec-v2-EN.md` together. The audit closure document records remaining evidence. Earlier Arabic plans are historical. Supplied experiments are scientific reference examples, not binding implementation instructions.

## Execution rules

Use assistant-led execution with **Plan → Review → Critic → Alternatives/Long-term** after every task. Record acceptance results, failed attempts, actual versions/build/device and remaining limits in the task evidence report. Do not silently pass a gate because an alternative looks plausible. Necessary contract/scientific tests precede or accompany implementation; verify native behavior and user journeys, not just mocked interfaces.

Proposed product locations below are a future repository layout, not existing code: `native/core`, `native/solver`, `native/tests`, `unity/Assets/PharmaLabVR`, `science/packages`, `science/reference`, `docs/decisions`, `docs/evidence`. Create these only at the agreed implementation stage. Exact file boundaries can change without changing ownership/contracts; record the reason.

The initial six-material profile is a development capability. Research packages may be explored with clear limitations but cannot supply validated assessment outcomes. New phases, reactions, temperatures and atmosphere models require explicit capability extensions. There is no requirement to deliver every chemistry domain before testing the first foundation.

## Dependency order

Task 0 precedes everything. Task 1 defines contracts. Task 2 investigates science while Task 3 probes native/device interaction using labeled water fixtures. Task 4 builds transport; Task 5 integrates scheduling/results. Task 6 evaluates calibrated instruments and mixing/indicators. Task 7 completes UX. Task 8 completes persistence. Task 9 runs exact-build device acceptance. Task 10 publishes only evidence-backed capabilities. Task 11 prepares university content adapters and subsequent capability plans. Tasks can overlap only where their interfaces and dependencies are stable; overlap does not waive a gate.

## Task 0 — Reproducible environment and evidence baseline

Deliver version manifest, build instructions, platform matrix, dependency/license inventory, initial risk register and evidence template. Check installed tools instead of assuming Unity/compiler/Android SDK availability. Review redistributed database and library licenses separately from executable licenses. Lock Windows and Android architectures supported by the chosen Unity release and plugins.

Build a native hello-world library and Unity binding on Windows and Android; confirm architecture, calling convention and ownership. Build the actual OpenXR application on an available target; record unavailable targets as EvidenceMissing. Check package compatibility before pinning. Define deterministic test seeds and numeric tolerances; deterministic event order does not require bitwise-identical floating point across CPUs.

**Exit:** Another clean checkout can reproduce native tests and the basic build using the recorded toolchain. No claim of chemical or device acceptance yet. If IPhreeqc cannot satisfy deployment/licensing constraints, evaluate Reaktoro against the same requirements before selecting it.

## Task 1 — Authoritative state and versioned contracts

Implement vessel/material/tool identifiers, units, restricted conserved pools, material revisions, actuator/inventory revisions, branch command sequence, receipts and semantic journal. A command identity is `(branchId, commandSequence)`; discrete commands enter one ordered channel. Validate finite values, nonnegative inventory, known IDs and supported schema before mutation.

Define a versioned C ABI with explicit buffer sizes/status codes and core-owned handles. Expose create/destroy session, submit input/command, step transport, get snapshot, poll observations, export/import session. Never pass native exceptions across the boundary. Copy immutable snapshots into caller-owned buffers with documented lifetimes; reject incompatible versions cleanly. Thread ownership and destruction ordering must be testable.

Define TransferEnvelope as in design: source/region, typed selection, quantity/basis, intended destinations and overflow sink. Define observation provenance and the four orthogonal status dimensions. Derived species observations cannot mutate the first profile's conserved authority. Future reaction commits remain disabled.

**Tests/exit:** Unrelated-vessel revision changes do not cause false conflicts; touched stale requests reject atomically; duplicates return receipts; conflicting duplicate payloads reject; invalid numbers leave state unchanged; unsupported phase selection cannot accidentally transfer water. Session handles and buffers survive repeated create/destroy without leaks or use-after-free. Round-trip schemas preserve units and identities.

## Task 2 — Independent references and scientific engine gate

Create independently calculated ideal fixtures and provenance notes before engine comparisons. Include strong/weak acid titration, mixtures, buffers, dilution and contamination. For example, 25 ml 0.1 M HCl plus 0/12.5/25/37.5 ml 0.1 M NaOH gives ideal pH approximately 1/1.477121/7/12.301030 at 298.15 K, Kw=1e-14, additive volumes and no atmosphere exchange. Weak acid fixtures explicitly state Ka; these are ideal numeric checks, not real sample measurements.

Build the necessary-species package candidate from reviewed thermodynamic data. Inspect restricted pools, permitted equilibria, activity conventions, concentration units and ionic-strength assumptions. Do not force realistic engine activity coefficients to fit an ideal curve. Establish matching-assumption checks independently; document differences when assumptions differ. Keep source provenance, hashes and licenses in the package manifest.

The evidence matrix must include composition/ratios, concentration, ionic strength, temperature, pressure/atmosphere, volume/density convention, uncertainty and interpolation policy. Identify independent experimental verification sources; mark absent data explicitly. When fitting models, keep held-out data separate.

**Tests/exit:** Charge, element and restricted-pool balances pass; ideal reference computation reaches the design's matching-assumption goal; engine results are investigated against independent references, including near equivalence. Measure initialization, repeated solves and memory on candidate platforms. Choose IPhreeqc only if scientific, deployment and performance gates hold; otherwise document a same-case Reaktoro comparison. No Published status without adequate evidence. Initial conservation test tolerance: absolute 1e-12 mol plus 1e-9 relative for these fixtures; revise only with justified numerical analysis.

## Task 3 — Early headset and instrument UX probe

Build a simple bench with water fixtures, one bottle, receiver, mounted burette, retrieval tray, scale-reading view and pause panel. These use the continuing architecture; any temporary scientific stub is explicitly labeled and cannot enter a published build. Probe standalone native binding overhead and controls before detailed art.

Test seated/standing, dominant-hand swap, two-hand valve/receiver operation, reach/recenter, grab ownership, instrument readability and recovery after tracking loss. Start with the UX specification's valve interaction; calibrate its gain, deadband and hit areas on hardware, record the chosen profile, and compare a thumbstick alternative if manipulation blocks users. A research default may use 90 degrees valve motion per 0.10 m horizontal controller displacement, clamped to calibrated stops; this is an input mapping, not a scientific flow law.

**Exit:** Available headset users can control flow, stop intentionally, identify the receiver and read the required graduation. Recenter and tracking recovery cause no unintended transfer. Record task failures and profile-specific limits. Missing devices leave their acceptance open. Stop and revise interaction or workload if this gate fails.

## Task 4 — Conservative transport and tool inventories

Implement homogeneous liquid parcels, tool hold-up/tip inventories, source availability, capture fractions, receiver capacity and explicit sinks in the native executor. Calibrated geometry and the accepted volume model determine flow/capacity. Unity submits pose/actuator intent, never final capacity allocation. Simulation quantities and displayed volumes agree within the declared model limits.

Atomically debit source and credit receivers/tool/spill. Implement water-mass and supported volume quantity bases; reject unsupported conversions. Add rinse, disposal, contamination and residuals as material operations. In-flight material stays represented when a valve closes; closing cannot delete a hanging drop.

**Tests/exit:** Two 5 ml captures into 7 ml remaining capacity credit receiver 7 ml and spill 3 ml; exactly 10 ml leaves the source under the additive-volume fixture. Duplicate and stale command cases preserve totals. Empty-source and simultaneous requests cannot create negative inventory. Vary transport tick 10/20/40 ms and establish convergence of delivery, spills and residual behavior. Geometry/input changes cannot alter calibrated instrument capacity.

## Task 5 — Solver scheduling, observation validity and recovery

Implement immutable per-vessel requests, dependency/package hashes, one in-flight plus one replaceable pending slot per vessel, round-robin fairness and bounded work. Commit all material events; only eligible equilibrium requests may coalesce. Instrument request latency, state age and gross changed transfer volume per affected vessel, counting both ingress and egress. Idle unchanged observations stay current.

Validate solver outputs before publication. Intermediate snapshots may yield historical observations; they never overwrite current authority or become Current falsely. Newest solved revision advances monotonically. Support, progress, freshness and package maturity are independent. No universal pH clamp. Unmatched scientific dependencies cannot supply assessment values.

Implement ComputeHold and tracking hold with explicit causes: freeze material/time, preserve journal, park tools, clear unconsumed samples, keep view/menu responsive. Resume requires caught-up eligible state, neutral controls, refreshed pose baselines and explicit Continue. Releasing a fault does not auto-pour. If no valid recovery exists, offer only save/restart/dispose actions that actually work.

**Tests/exit:** Continuous-transfer traces make scientific progress without starvation or dropping committed transfers; older results cannot replace newer observations; unrelated vessel changes are isolated. Forced solver delay/failed solve/invalid output/bounded-queue saturation produce safe, understandable states. Circulation does not bypass the volume budget. No catch-up pour after paused input. Published workload goals are zero ComputeHolds, plus design age/volume budgets; overload holds are separately tested protective behavior.

## Task 6 — Instruments, local mixing and indicator evidence

Calibrate burette flow/tip fill/drop detachment, pipette delivery/residuals and vessel volume conventions against independent calibration references or measured data. Track uncertainties and distinguish reading skill from numeric inspection. Use the NIST volumetric calibration reference linked in the audit as a method source, not a claim of automatic calibration.

Evaluate the proposed three-region mixing model against simpler homogeneous and more detailed alternatives using tracer response and region/timestep convergence. Choose the simplest model that passes the learning-task error limits. No evidence means the affected mixing claim remains Approximate/Unsupported.

For each indicator, define chemical preparation, added quantity, solvent, equilibria, concentration/optical model and measurement uncertainty. Verify mixtures of indicators and endpoint visibility independently; do not select color from the last-added indicator. Protocol text does not override computed pH or scientific endpoint evidence.

**Exit:** Publishable instrument and indicator/mixing ranges have documented independent checks. If a profile cannot validate local mixing, clearly restrict it to homogeneous behavior and prevent incompatible assessment. This task can require new measurement data; absence is an evidence gap, not a reason to invent a result.

## Task 7 — Complete student journey and visual design

Implement the companion UX flows: setup/orientation, open lab, inspect, measurement view, updating, capability limits, recovery, pause/save, restart branch and review. Complete visual blockouts before detailed assets. Stable readable panels, object/component selection and opaque bottle labels support a credible lab. Colorless materials remain colorless.

Run formative sessions with 5–8 participants, including seated and left-handed use where available. Test bottle identification, retrieval, two-hand valve control, graduation reading, support/progress distinction, tracking/compute recovery and save/resume. Record qualitative failures; this sample does not demonstrate learning effectiveness. Validate Arabic shaping, mixed-direction units, text scaling, captions and non-color UI cues on the selected text stack.

**Exit:** No unresolved severe journey blocker; no unintended recovery transfers; task/device reading and control requirements met. Alternative scientific observations record their changed assessment objective. A reviewer can inspect actual screens/recordings; written UX approval alone is not visual acceptance.

## Task 8 — Persistence, replay and bounded resources

Implement checkpoint/journal/observations with package identity, integrity validation, explicit migration and playback/recompute modes. Import into a fresh context and publish only after successful validation. Branching creates a new branch identity. Missing packages permit supported recorded playback, not fabricated live continuation.

Checkpoint on the design's simulation/wall-clock dirty-state schedule and explicit save/lifecycle opportunities. Define a contiguous terminal-outcome watermark: rejected commands receive recorded terminal outcomes so sequence gaps cannot masquerade as committed material. If an older receipt has been pruned, return AlreadyCommitted only when the identity index proves the command committed; rejected or unknown identities retain distinct outcomes and never replay automatically. The digest detects mismatched duplicate payloads where retained; unknown historical payload verification must be reported unavailable rather than claimed successful.

Measure memory/disk growth; bound input/solver queues and import sizes. Compaction preserves required selected playback history. Any retention option that reduces coverage labels the loss before confirmation. Choose actual numeric memory/storage limits from device measurements and document them before publication.

**Tests/exit:** Save/load/replay preserve material, branch IDs and provenance. Corrupt/oversized/unknown-version files cannot mutate the active session. Crash interruption cannot yield half a transfer or half a checkpoint. Old duplicates cannot execute again after compaction. Repeated create/load/unload and long-session tests show bounded working memory; playback coverage matches the selected policy.

## Task 9 — Exact-build multi-device acceptance

Use the design's 1/4/10-vessel traces and three 30-minute runs after five-minute warm-up on each supported device/build. Report native refresh, CPU/GPU p95/p99, dropped frames, solver latency/state age, unobserved transfer volume, queue fairness, holds, memory and storage. Compare Android/Windows scientific outputs within accepted tolerances.

Exercise offline installed content, pause/resume, tracking faults, thermal behavior, text/instrument reading and two-hand input. OpenXR reduces vendor coupling but cannot replace physical-device evidence. Document extension/fallback behavior and minimum capabilities.

**Exit:** A versioned acceptance matrix identifies Passed, Failed or EvidenceMissing per task/device. A device can be excluded from the initial published support list without changing the architecture. Standard workloads must meet frame and observation budgets without protective holds. Do not label unsupported hardware universally compatible.

## Task 10 — Capability publication and foundation review

Assemble evidence for each specific capability: scientific matrix, calibration/mixing/indicator validity, UX/device task acceptance, package provenance, licenses, performance and persistence. Research/Published/Retired lifecycle is explicit; installed published versions remain pinned for active sessions. Verify update integrity and signing before formal external distribution.

Run full Plan/Review/Critic/Alternatives review across integrated behavior and residual risks. Produce a release report distinguishing numeric references, engine comparisons, scientific accuracy, physical-device tests and student usability. Learning-effectiveness claims require their own study.

**Exit:** Only evidenced capabilities enter Published. Missing experimental or device evidence keeps that capability open. Foundation ready does not mean a universal chemistry simulator has been completed.

## Task 11 — University content and long-term extension contracts

Only after the relevant foundation capabilities pass, add protocol/assessment definitions that observe authoritative events/state and emit instructions/feedback without changing chemistry. Define objective, supported capabilities, preparation, expected observation windows, instructor configuration and accessibility adaptation. Replay uses the same model/package identity as the original session.

Write separate capability plans for selective phases/precipitation, gas exchange, energy/temperature and restricted redox/kinetics. Each covers state schema, conservation, coupling, datasets, uncertainty, performance and UX limits. Account administration, institutional reporting and distribution follow actual customer requirements; do not embed billing or cloud dependencies into the scientific core prematurely.

**Exit:** Protocol changes require data/configuration rather than chemistry outcome branches. Unsupported objectives cannot be assigned. Extension proof includes regression of existing capabilities. This is an extension contract, not approval of an unresearched universal solver.

## Final self-review of this plan

**Plan:** Dependencies now place contracts, independent science and headset/tool probes before expensive lab content. Each task has a concrete evidence gate.

**Review:** Capacity authority, per-vessel revisions, historical observations, hold recovery and retention semantics are explicit. UX and science reference plans cover the audit findings.

**Critic:** Experimental validation data, actual solver timings, valve manipulation, volume/density models and hardware availability are unresolved. They can change supported ranges or even the solver/input choice. No schedule or budget is promised without those measurements.

**Alternatives/long term:** Keep a portable local core with one primary solver. Compare alternatives only against failed gates; avoid maintaining two unneeded engines. Preserve the phase envelope and provenance without enabling unvalidated phase physics. OpenXR and isolated adapters improve portability while per-device testing determines support. The foundation can expand, but expansion remains evidence-driven.

**Immediate next implementation unit:** Tasks 0–1, followed by Task 2 and the Task 3 early probe. This document finishes planning; no product implementation was performed to create it.

## Full-review addendum — required acceptance additions

The design's section 13 is binding. Add these checks to the owning tasks; they do not authorize product implementation in this document revision.

| Tasks | Added checks |
|---|---|
| 0, 2, 5 | Bound pathological scientific runs using demonstrated engine controls; test blocked-worker diagnosis, responsive menus/save, no unsafe destruction, lifecycle termination and clean-context equivalence after varied prior inputs. Do not promise cancellation from a watchdog alone. |
| 1, 4 | Specify calibrated geometry sample schema; reject wrong coordinates/profile/revisions; test conservation with synthetic capture samples; journal accepted parameters. No duplicate general-purpose physics engine. |
| 4, 5, 9 | Test maximum calibrated flow, transport quantum, simultaneous vessels and measured solve delay together. Hold before an observation budget violation; no silent flow throttling as a scientific shortcut. |
| 1, 2, 6, 10, 11 | Track coverage per observable; demonstrate that validated pH with absent color prevents indicator-based task publication. Unsupported density prevents calibrated volume claims. |
| 3, 7 | Recover held tools through visible docking/re-grab or another tested remapping; reset actuator and pose baselines before continuing. No invisible jumps or apparent flow from physical hand motion while parked. Test glass sorting, refraction, meniscus and indicator appearance on actual devices. |
| 8 | Define durable boundary and recovery-point policy; test wall-clock checkpoints while simulation is paused/held and dirty; Save must not report success before durable completion. Forced termination may lose only records after the actual durable boundary, never corrupt prior material history. |
| 0, 5, 8, 9 | Derive and publish limits for vessels/tools/contexts/packages/imports; test one request beyond each limit without corruption or uncontrolled allocation. |
| 2, 3, 7 | Check early laboratory-task relevance against the supplied experiments and participant/instructor feedback where available; record missing participants/data as evidence gaps rather than delaying all product feedback until the end. |

The first implementation phase is approved as a way to resolve these uncertainties. Solver selection, numeric flow/device limits and final visual design remain provisional until their gates pass. Universal open chemistry is a long-term objective, not an approved accuracy claim.
