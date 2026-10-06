# PharmaLabVR VR-first implementation plan — v3

> Agentic executor: use `superpowers:executing-plans` task-by-task if available. The execution method is native coding by the implementation chat using its configured model. No new high-level planning is requested. Every task ends with Plan, Review, Critic, Alternatives/Long-term and actual verification.

**Goal:** Complete the supported, tested VR-first laboratory foundation and mouse/keyboard companion, then push a reviewable revision to the named repository.

**Architecture:** One C++ material/transport core and pinned scientific adapter; Unity geometry/input/presentation with XR first; shared sessions and model provenance.

**Stack:** Source pins in `09-SOURCES-AND-PINS.md`; Unity/C#/URP/XRI/OpenXR; C++17/IPhreeqc; CMake/CTest; Unity EditMode/PlayMode and actual Player tests.

**Spec:** All v3 numbered contracts plus compatible `authority/` snapshots. All case IDs below are defined in `06-TEST-CATALOG.md`.

## Global constraints

VR rig and spatial UI are primary. Native Windows no-headset operation remains required. No browser substitute. No replicated chemistry, unreviewed endpoint branches, current labels on stale results, silent model changes, free mass deletion, arbitrary native thread termination or fabricated device passes. Keep original source documents out of public Git. Record toolchain/build/package identity. Tests and runtime evidence must survive model context compaction.

## Review focus

1. Held liquid tool + camera/UI/focus change: material and world pose do not change accidentally (P07/P10).
2. Receiver fills during delayed solve: atomically allocate overflow and enforce precommit observation budget (P03/P05).
3. Repeated solution/context reuse: no previous definition/output affects new solve (P04).
4. Failed write/partial journal/native stall: valid state remains recoverable and view/menu/save do not deadlock (P05/P11).
5. One mode/model has evidence another lacks: record separate statuses and reject incompatible grading without blocking supported use (P09/P12/P14).

## Task protocol

For each P task, read its consumed contracts/cases, write failing meaningful tests first for new domain behavior, implement named files, run native or Unity checks appropriate to the files, fix failures, document four-stage review and commit. Do not write tests that merely duplicate output tables or mock every important dependency. Test scene object references, transitions and actual interactor/core integration. Exact invocation paths are in `07-BUILD-CI-AND-PUSH.md`; once build functions are defined, later tasks use them without renaming interfaces.

### P00 — Checkout, authority and build prerequisites

**Create:** `.gitignore`, `.gitattributes`, `README.md`, `docs/implementation-package-v3/`, `docs/evidence/environment.json`, `docs/evidence/progress.json`, `docs/evidence/decision-ledger.md`, `scripts/doctor.ps1`, `scripts/doctor.py`.

**Consumes:** package/remote/scope. **Produces:** reproducible clone/tool discovery and progress manifest.

- [ ] Inspect existing remote refs/AGENTS/worktree before writing; preserve existing work. If empty, bootstrap `main` then create `feat/foundation-vr-first` per Git instructions.
- [ ] Check pinned Editor, MSVC/CMake/Python/Android modules/auth without printing secrets; record available/missing/actual paths. Copy public authority docs/concept; source-documents remain local reference-only.
- [ ] Implement doctor with exit 0 only for required available checks; explicit `--allow-missing-unity` permits independent native work and reports missing Unity, not a false pass. C00.
- [ ] Commit bootstrap/progress and proceed with all independent tasks even if hardware is absent.

### P01 — Native library and test harness

**Create:** root `CMakeLists.txt`, `CMakePresets.json`; `native/CMakeLists.txt`, `native/core/include/plv/abi.h`, `types.hpp`, `native/core/src/abi.cpp`, `native/tests/CMakeLists.txt`, `native/tests/test_abi.cpp`; `third_party/README.md`, dependency pin manifest, `.github/workflows/native.yml`.

**Consumes:** ABI in 02. **Produces:** C-exported `pharmalab_core`, native test target `plv_tests`.

- [ ] Add CTest cases A01–A04: version/handle/error/buffer/nonconsuming poll semantics.
- [ ] Add deterministic finite/unit/ID/size validation types; no placeholder success paths.
- [ ] Build shared wrapper x64 and static PIC IPhreeqc dependency, disable upstream standalone examples/tests/Fortran needs in the product build. Use C++17 and correct CRT linkage.
- [ ] In the parent CMake scope set `BUILD_SHARED_LIBS=OFF`, `IPHREEQC_ENABLE_MODULE=OFF`, `BUILD_CLR_LIBS=OFF` before `add_subdirectory(third_party/iphreeqc-source)`. The upstream project detects subproject mode automatically. Set `POSITION_INDEPENDENT_CODE=ON` on `IPhreeqc`; create our wrapper with explicit `SHARED` so it remains shared despite the dependency setting. Do not disable our own CTest target. This source subset is for embedded builds, not standalone upstream doc/example installation.
- [ ] Run clean Release native build/tests. Commit actual logs and dependency identities, not generated third-party binaries.

### P02 — Material authority, preparation and revisions

**Create:** `native/core/src/state_executor.cpp`, `material.cpp`, `commands.cpp`, `canonical_payload.cpp`, corresponding headers under `include/plv/`; `native/tests/test_material.cpp`, `test_commands.cpp`; `science/packages/aqueous-six-research-v1/manifest.json`, `stocks.json`.

**Consumes:** pool/unit/stock schema in 02/04. **Produces:** `StateExecutor::apply`, material snapshots and terminal receipts.

- [ ] Add M01–M05/C01–C05 with represented-pool inventory, stale touched revisions, unrelated vessels, duplicate/rejected/conflicting payloads and oversized inputs.
- [ ] Implement explicit six-stock preparation, initial empty vessels/sinks/tool inventories, command sequencing/atomic commits and provenance. Source preparation does not fix pH.
- [ ] Implement callback-free snapshots and bounded queues; reserve outcome/event slots before mutation; no native exceptions cross ABI.
- [ ] Run all existing native tests; checkpoint progress/commit.

### P03 — Transport and calibrated geometry contract

**Create:** `native/core/include/plv/transport.hpp`, `geometry_sample.hpp`, `native/core/src/transport.cpp`, `tool_models.cpp`; `native/tests/test_transport.cpp`, `test_tools.cpp`; `science/packages/.../instruments.json`.

**Consumes:** validated input, material authority, typed TransferFixed. **Produces:** conservative committed parcels, tool flow/tip/receiver/spill receipts.

- [ ] Add T01–T08: 7/3 overflow, available-source debit, atomic stale/retry, rinse/residual/in-flight drop, wrong coordinates/profile and 10/20/40 ms convergence.
- [ ] Implement homogeneous parcel division in water/pools/reference volume/indicator inventory. Fixed oversized request rejects; live flow obeys available-source/capture/capacity.
- [ ] Use research flow `Q = Cd·Aopen·sqrt(2·g·h)` with g=9.80665 m/s², Cd=0.60, maximum aperture radius 0.00030 m and Aopen proportional to actuator01 for the first burette profile. Head comes from current liquid geometry. These are explicit research parameters, not measured burette calibration. Bottles/pipettes have separate profile/flow/residual parameters; no shared fake “all tools pour identically” law.
- [ ] Track tip hold-up, hanging/in-flight droplets and residual parcels through state. Start research drop reference volume 0.05 ml with calibration pending; do not use an emitted particle count as material authority. Validate different tick sizes and update evidence/commit.
- [ ] For T08 compare cumulative delivered quantities on the same continuous actuator trace at 10/20/40 ms against a 1 ms research reference. Require error at most `max(0.01 ml, 0.005 * referenceDeliveredMl)` and exact represented-pool inventory tolerance from 04. This is discretization evidence, not physical calibration.

### P04 — Independent reference and IPhreeqc adapter

**Create:** `native/solver/include/plv/solver.hpp`, `native/solver/src/iphreeqc_adapter.cpp`, `reference_adapter.cpp`, `solve_request.cpp`; `native/tests/test_science.cpp`, `test_solver_lifecycle.cpp`; `science/reference/ideal-aqueous.json`, `science/provenance/`, `science/packages/.../coverage.json`.

**Consumes:** immutable complete pools, pinned vendor/database, fixtures. **Produces:** `IPhreeqcAdapter::solve(const SolveRequest&) -> SolveResult` and explicitly separate ideal-reference identity.

- [ ] Copy the package fixture/verifier as reference tooling; add S01–S07, with full engine calls for engine tests. Test matching versus different activity assumptions correctly.
- [ ] Implement mapping in 04, named-column extraction, bounded errors, pool/charge/finite validation, complete-cell reconstruction and pinned identity. Never reuse an old result row after a failed run.
- [ ] Test same request after diverse prior requests, initialization/solve/memory measurements and bounded convergence controls. Do not add an unverified cutoff thread that kills the engine.
- [ ] Record engine gate and database/source/runtime version distinctions. Implement baseline Research capability; if a hard gate fails, use the specified evidence-triggered comparison rather than inventing results. Continue independent core/UI work, keeping the failed capability unavailable.

### P05 — Worker, scheduling, holds and resource admission

**Create:** `native/solver/src/solver_worker.cpp`, `native/core/src/observation_store.cpp`, `scheduler.cpp`, `hold_state.cpp`, headers; `native/tests/test_scheduler.cpp`, `test_recovery.cpp`, `test_limits.cpp`.

**Consumes:** immutable requests, transport proposals. **Produces:** per-observable current/stale results, fair queue and precommit budget admission.

- [ ] Add O01–O07/H01–H05/L01–L03, including continuous transfer, stale result order, circulation budget, unsupported versus failed, blocked-call diagnostics and capped resources.
- [ ] Implement one worker, per-vessel in-flight/pending slots, round-robin fairness and per-observable freshness. No worker holds state/save locks or destroys a stuck context.
- [ ] Implement precommit age/gross-volume checks; frame backlog >two ticks and stale input produce explicit holds. Resume requires fresh baseline/neutral input/explicit action; no auto-pour.
- [ ] Test every fault path without deadlocking view/save tests; release workloads require zero recurring compute holds. Commit limits and timing instrumentation.

### P06 — Unity bridge, project and reproducible scenes

**Create:** `unity/Packages/manifest.json`, real `packages-lock.json`, `ProjectSettings/`, `Assets/PharmaLabVR/Runtime/Core/{NativeMethods,CoreSession,CoreDriver,NativeJsonCodec}.cs`, `.asmdef`, `Editor/{BuildLabAssets,BuildLabScenes,BuildPipelineEntry}.cs`, `Assets/PharmaLabVR/Scenes/{Boot,Lab,Review}.unity`, `Tests/EditMode/NativeInteropTests.cs`, `SceneContractTests.cs`.

**Consumes:** ABI/pinned packages. **Produces:** `CoreSession.Submit(string commandJson)`, `SubmitInputs(string batchJson)`, `Step(double deltaS, ulong nowNs)`, `ReadSnapshot()`, `PollEvents()`, `ExportSession()`, static `ImportSession(string json)`; `CoreDriver` serializes main-thread calls and publishes immutable snapshots.

- [ ] Marshal UTF-8 explicitly with cdecl/uint widths/SafeHandle; bounded retry for buffer growth; poll query never consumes. Do not call native once per mesh/controller every rendered frame.
- [ ] Add U01/U02 for real interop and absent plugin errors; absent core disables lab entry with clear message, not a silent fake core.
- [ ] Build project/scenes/assets using pinned Editor, serialized references and `.meta`; run EditMode. Build functions: `PharmaLabVR.Editor.BuildPipelineEntry.WindowsDesktop`, `WindowsVR`, `AndroidVR`, `AllAssets`.
- [ ] Preserve actual Unity import/compile/build logs. Real package lock must come from successful resolution; never fabricate it.

### P07 — VR rig and geometry adapter FIRST

**Create:** `Runtime/Input/{ILabInputAdapter,XRInputAdapter,XRSettingsController,InteractionOwnership,LabGeometryAdapter}.cs`; `Prefabs/XRPlayerRig.prefab`, `Input/LabXR.inputactions`, `Tests/PlayMode/XRInteractionTests.cs`, `GeometryAdapterTests.cs`.

**Consumes:** CoreSession, XRI action/interactor APIs and calibrated profiles. **Produces:** `ILabInputAdapter.SampleInputs(ulong nowNs)`, `ResetBaselines()`, `SetEnabled(bool)`; `LabGeometryAdapter.BuildSample(...)` registered lab-coordinate capture proposals.

- [ ] Configure OpenXR Windows/Android providers and controller profiles, direct grip/ray UI distinction, component capture, mounted-tool ownership, hand swap/seated setup.
- [ ] Enable official simulator for development tests; retain runtime VR rig, not simulator-only rig. Exclude simulator from production builds.
- [ ] Add X01–X06/G01–G03: two-controller interaction, control capture, neutral recovery, recenter, collision/profile geometry and simulated tracking validity.
- [ ] Run PlayMode tests with deterministic injected actions; note SimulatedXR. No headset claim. Commit VR rig before desktop-specific UI polish.

### P08 — Tool prefabs, liquid visuals and spatial panels

**Create:** `Runtime/Tools/{ToolPresenter,ValveActuator,PipetteActuator,InstrumentMeshBuilder,LiquidPresenter,MeniscusPresenter}.cs`; `Runtime/UI/{LabPanelController,InspectPanel,MeasurementView,HoldPanel,SettingsPanel}.cs`; instrument/stock/panel prefabs/materials/shaders; `Tests/PlayMode/ToolJourneyTests.cs`, `PanelStateTests.cs`.

**Consumes:** native snapshots/observations, 03 geometry/tokens. **Produces:** believable inspectable assets, supported visual observations and complete spatial actions.

- [ ] Generate physical research profiles/prefabs/graduations, correctly increasing burette scale, tip/valve and receiver with no overlapping selection targets.
- [ ] Render liquid geometry from committed volume, not a detached fill animator. Drops have stable parcel IDs; purely visual pooling never destroys inventory.
- [ ] Implement all panel states and measurement view without automatic reading answers; keep endpoint/optical Unsupported/Approximate status visible.
- [ ] Add V01–V05/J01–J04 and capture actual scene screenshots. Opaque labels/stereo-compatible glass/mobile variant must remain readable; no approval based only on concept image.

### P09 — Research indicators and mixing capability plumbing

**Create:** `Runtime/Science/ScientificObservationPresenter.cs`; `native/solver/src/indicator_model.cpp`, `mixing_model.cpp` and provider interfaces; `science/packages/.../indicators.json`, `mixing.json`; `native/tests/test_indicator.cpp`, `test_mixing.cpp`.

**Consumes:** computed pH, indicator quantities, independent research config. **Produces:** explicitly approximate/theoretical optical observations or Unsupported, separate from valid pH.

- [ ] Implement generic quantity/solvent/provenance-aware indicator schema and observable composition; do not use last-added-color selection.
- [ ] Add I01–I03 and region/timestep convergence cases; missing preparation/data rejects promotion and never creates a graded endpoint.
- [ ] Use qualitative research visual model only with identified constants/provenance and its limitations; do not improvise real spectral constants. Implement unavailable-result UX completely.
- [ ] Compare homogeneous/reduced mixing as specified; keep unvalidated models Research. Record missing tracer/optical data, not participant/data fabrication.

### P10 — Desktop adapter and complete application journeys

**Create:** `Runtime/Input/DesktopInputAdapter.cs`, `DesktopCameraController.cs`; `Runtime/UI/{BootMenu,OrientationFlow,DesktopToolbar,LocalizationService,AccessibleStatus}.cs`, English/Arabic tables/fonts/notices; `Tests/PlayMode/DesktopJourneyTests.cs`, `FocusRecoveryTests.cs`, `LocalizationTests.cs`.

**Consumes:** same core/geometry/tool profiles; required desktop bindings. **Produces:** genuine no-XR Windows mode with complete mouse controls and shared UX state.

- [ ] Add D01–D06, J01–J05, B01/B02: no runtime startup, camera/UI isolation, pick/tilt/place/valve, focus pause, scale/layout/localization.
- [ ] Implement no-headset startup/mode selection and actual desktop controls; no click-through actions. Shared styles/panels adapt to screen space; no duplicate chemistry.
- [ ] Implement optional orientation, comfortable bench/retrieval/recenter, localizable status/audio captions and assistance metadata. Use tested Arabic shaping/bidi with licensed fonts; language change preserves state.
- [ ] Run actual Desktop Player interaction where environment permits, screenshot 720p/1080p; otherwise BuildNotVerified and exact missing dependency. Keep desktop's real UX distinct from XRI simulator.

### P11 — Durable persistence and review

**Create:** `native/core/src/{session_codec,journal,checkpoint,identity_index}.cpp`, tests `test_persistence.cpp`, `test_crash_recovery.cpp`; `Runtime/Session/{SessionController,SaveService,ReviewController}.cs`, `Tests/PlayMode/SessionJourneyTests.cs`.

**Consumes:** canonical semantic events, model identity, input metadata. **Produces:** validated new-context import/export, branch/review, recorded versus recompute modes, durable Save.

- [ ] Add R01–R08: atomic write interruption, flush error, terminal-receipt compaction, journal truncation, unknown/missing model, mode parity and size/resource limits.
- [ ] Implement checksummed records/atomic checkpoints, dirty wall/simulation schedule, actual recovered boundary and bounded identity cache.
- [ ] Complete Save/Continue/Restart branch/Review UI with accurate available actions and no silent migration. Recompute requires pinned model or explicit version comparison.
- [ ] Run disk fault tests, repeated load/destroy and actual save/resume journey; commit evidence and README controls.

### P12 — Two research protocol examples and capability gating

**Create:** `Runtime/Protocols/{ProtocolDefinition,ProtocolRunner,CapabilityGate,AssessmentContext}.cs`; `Content/Protocols/{strong-acid-titration,mixed-acid-titration}.json`; `Tests/EditMode/ProtocolTests.cs`, `Tests/PlayMode/ResearchLessonTests.cs`.

**Consumes:** current authoritative events/results, supported capabilities and supplied source protocols. **Produces:** optional data-driven instructions and ungraded Research examples, not reaction outcomes.

- [ ] Add Q01–Q04: no direct material mutation, missing color/freshness prevents certified scoring, refill versus cumulative volume, mode/assistance eligibility.
- [ ] Use original protocols as drafts with corrections/uncertainties from 04. No “first endpoint equals HCl exactly” magic or hidden forced success.
- [ ] Keep Open Lab entry independent; procedure content does not restrict free manipulation. Research examples can explain absent capability; no fake complete experiment claim.
- [ ] Run data-driven protocol tests and actual walkthrough screenshots; persist source references/limitations.

### P13 — Build scripts and CI

**Create:** `scripts/{build-native,build-unity,test-unity,package-artifacts}.ps1`, shell/Python equivalents as needed; `.github/workflows/{native,unity}.yml`; `docs/build.md`, third-party notices, artifact manifest.

**Consumes:** build functions/test targets/pins. **Produces:** WindowsDesktop, WindowsVR, AndroidVR artifacts and actual CI checks without leaked secrets.

- [ ] Implement commands/status handling in 07; separate native and licensed Unity jobs. Missing Unity credentials do not silently pass a required Unity build.
- [ ] Build native plugins for x64/ARM64 with correct importer settings; IL2CPP Android stripping/linking and absent-plugin test verified where possible.
- [ ] Run A01–A04/U01/B01–B04 from clean checkout. Package logs/JUnit/screenshots/hash manifest; no Library/Temp/credentials/huge player output in source Git.
- [ ] Commit workflows and artifact reproducibility instructions; use existing credentials only.

### P14 — Profiling, resource gates and physical acceptance matrix

**Create:** `Runtime/Diagnostics/PerformanceRecorder.cs`, `Content/Traces/{one,four,ten}-vessels.json`, `scripts/analyze-performance.py`; `docs/evidence/{desktop,simulated-xr,windows-vr,android-vr}-acceptance.json`.

**Consumes:** exact builds/controlled traces. **Produces:** frame/science/queue/memory/resource records and per-target acceptance.

- [ ] Implement K01–K04; 1/4/10-vessel traces, five-minute warmup, three 30-minute measured runs for actual publication profiles.
- [ ] Desktop provisional 60 FPS; headset target ≥72 Hz, preferred 90 if viable. Report p95/p99 CPU/GPU, dropped frames, gross-volume/state-age budgets, fairness/holds/memory and settings.
- [ ] Physical hardware/participant/experimental gaps stay EvidenceMissing, not assumed success. Simulator cannot prove thermal/optical/comfort behavior.
- [ ] Reject unsupported workload admission predictably; do not alter calibrated flow to make benchmarks pass. Publish no capability/device without its relevant evidence.

### P15 — Final correctness, visual and long-term review

**Create/update:** `docs/evidence/final-review.md`, `completion.json`, source/build/test/artifact manifest, README; current screenshots/recording, all task review entries.

**Consumes:** entire implemented branch and gate matrix. **Produces:** exact-head reviewable foundation, clear residual risks.

- [ ] Run full native/Unity/player regression; fix code-caused failures and severe UI blockers before final claim.
- [ ] Complete 08 checklist with actual evidence paths/commit, status and reasons. No “all tests passed” if some targets never ran.
- [ ] Distinguish software implemented, built, numerically checked, experimentally validated, simulated XR and physically tested; no universal chemistry promise.
- [ ] Final four-stage review for the whole branch; commit final evidence then rerun affected checks at the committed HEAD.

### P16 — Push and planner review handoff

**Consumes:** verified commits, user's push authorization. **Produces:** remote branch, exact SHA, PR and downloadable available artifacts.

- [ ] Confirm exact remote/default branch, diff scope and no leaked sources/credentials/binaries; no force/reset.
- [ ] Push `feat/foundation-vr-first`; create a PR to main where supported, describe final implemented scope and validation. Attach it to the chat if Codex artifact tooling is available.
- [ ] Inspect checks for that exact pushed SHA, resolve failures within scope and update head/evidence. Pending unavailable licensed/hardware checks remain explicit.
- [ ] Return the exact review target and completion report to the user. Do not merge; planner reviews after implementation. If auth/network prevents push, keep local commits and exact error/retry command, never invent a PR URL.
