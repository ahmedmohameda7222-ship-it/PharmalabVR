# Frozen contracts — native core, adapters and sessions

## Repository modules and identifiers

Native namespace `plv`. Unity namespace `PharmaLabVR`. Export library `pharmalab_core` (`pharmalab_core.dll` Windows x64; `libpharmalab_core.so` Android ARM64). C ABI version 1; JSON schema version 1. IDs are validated ASCII `[A-Za-z0-9._-]{1,64}`; counters serialize as decimal strings to preserve uint64 values across consumers. A session has branchId, eventSequence, simulationTimeS and packageHash. Tools and vessels use stable IDs, never Unity instance IDs.

Use one main-thread StateExecutor. It owns inventory, commands, transport and receipt commits. SolverWorker owns all IPhreeqc construction/run/reset/destruction, including its global instance registry. No solver call holds state/UI/save locks. Worker inputs/results are immutable copies; completion polling applies provenance rules. Native create/destroy/submit/step/snapshot calls are serialized by Unity's CoreDriver; reject illegal cross-thread mutation. Document read-copy lifetimes.

## MaterialState and first-profile authority

`MaterialState`: solventWaterKg; analytical pools sodiumMol, chlorideMol, acetateMol; researchAdditiveVolumeM3; preparation/provenance IDs; optional separately tracked indicator quantities; materialRevision. All are finite/nonnegative; unknown pools/units are rejected. Initial pools represent the six-material restricted aqueous acid/base system. Sodium/chloride and the acetate family are conserved during transport and allowed speciation. Chemical forms of acetate are derived, not conserved independently. No acetate-to-carbonate pathway is enabled.

The research volume convention is additive prepared-solution volumes with an explicitly declared nominal solvent conversion of 1000 kg/m³. This is a reference convention, not measured solution density or full solvent-reaction physics. Track reference volume independently of water mass and reject ambiguous mixing of conventions. HCl/NaOH preparation derives strong-acid/base charge balance from counterions; do not conserve fictitious free H+ as a species. A neutralization's tiny solvent-water change is excluded by this first approximation, so do not claim exact hydrogen/oxygen balance for it. Full H/O/energy/phase accounting requires a later coupled reaction capability. Transport must still preserve all represented inventories exactly within numeric tolerance.

`VesselState`: id, capacityM3, inventory, materialRevision, calibratedGeometryId, poseRevision, observations. `ToolState`: id, calibratedGeometryId, actuatorRevision, inventoryRevision, actuator01, pose, tip/residual/inFlight parcels and current owner. Inventory-bearing tool parts have stable inventory IDs and participate in the same ledger. Sinks are explicit material holders, not discarded mass. Spill parcels are allocated to a named spill sink, then rendered from committed state.

`Observation`: vesselId, observableId, modelId, asOfMaterialRevision, packageHash, dependencyHash, unit, finite value/structured result when available, SupportLevel, ComputationState, ResultFreshness, warning IDs and validity bounds. `SupportLevel=Validated|Approximate|Unsupported`; `ComputationState=Ready|Pending|Failed`; `ResultFreshness=Current|Stale|Absent`; `PackageMaturity=Research|Published|Retired`. A Research package never supplies certified assessment scores. Latest solved revision is tracked per vessel/observable/model. Idle unchanged state stays Current. A stale result remains attached to its own snapshot and never updates current material.

## Command envelope and executor

All discrete commands contain schemaVersion=1, branchId, commandSequence (decimal string), type, expectedMaterialRevisions for touched inventories and payload. Hash the canonical validated payload plus command identity. One ordered channel processes discrete sequences; rejected commands get terminal outcomes. Duplicate identical identities return the recorded outcome; changed payload returns Conflict. Unknown/pruned identities do not execute by guessing. A transport tick generates uniquely identified committed events independently from the discrete-command sequence.

Supported command types: CreateVessel, PrepareStock, TransferFixed, SetActuator, PlaceTool, RinseTool, DisposeContents, Pause, Continue, Recenter, CreateCheckpoint, RestartCheckpoint and BeginModeChange. Programmatic stock preparation is setup/configuration only; student edits cannot replace a vessel's contents during normal play. Debug controls are excluded from normal assessments. Each atomic command either commits all effects and event or none. Reserve receipt/event capacity before mutating; Busy means no mutation.

`TransferFixed`: sourceInventoryId, sourceRegion=`Homogeneous`, selection=`HomogeneousAqueousLiquid`, quantity `{basis:WaterMassKg|LiquidVolumeM3,value}`, capture fractions `{destinationInventoryId,fraction}`, overflowSinkId. Fractions finite/nonnegative, sum ≤1; uncaptured fraction goes to sink; rounding remainder allocated deterministically by destination ID order. Require a valid volume convention for volume quantities. Oversized fixed requests reject; live rate is limited by available source. Split the parcel proportionally in every represented pool, water/reference volume and indicator inventory. Recalculate each destination's actual capacity at commit. No ignored overflow.

Native internal entry points:

```cpp
CommandOutcome StateExecutor::apply(const Command& command);
void StateExecutor::acceptInput(const ToolInput& input);
StepOutcome StateExecutor::step(double deltaS, uint64_t monotonicNowNs);
SessionSnapshot StateExecutor::snapshot() const;
void StateExecutor::acceptSolveResult(const SolveResult& result);
SolveResult IPhreeqcAdapter::solve(const SolveRequest& request);
```

Types live in `native/core/include/plv/types.hpp`; implementations follow the task map. Exact class names/signatures above are interface decisions, not provided compiled code. Exceptions are translated at ABI boundaries; domain failures are typed outcomes with no partial state. Begin with a testable research homogeneous transfer model; no hidden Unity-side quantity updates.

## C ABI

Use UTF-8 JSON at the initial integration boundary to avoid fragile marshaled nested layouts; no JSON parsing or snapshot allocations every rendered frame. Cache unchanged snapshots, batch all changed tool samples once per transport tick, and instrument overhead. Hot-path replacement with packed buffers is only justified by measured failing budgets with ABI versioning.

```c
uint32_t plv_abi_version(void);
int32_t plv_create(const char* config, uint32_t size, uint64_t* handle);
int32_t plv_destroy(uint64_t handle);
int32_t plv_submit(uint64_t handle, const char* command, uint32_t size);
int32_t plv_input_batch(uint64_t handle, const char* samples, uint32_t size);
int32_t plv_step(uint64_t handle, double delta_s, uint64_t monotonic_now_ns);
int32_t plv_snapshot(uint64_t handle, char* out, uint32_t capacity, uint32_t* required);
int32_t plv_poll(uint64_t handle, char* out, uint32_t capacity, uint32_t* required);
int32_t plv_export(uint64_t handle, char* out, uint32_t capacity, uint32_t* required);
int32_t plv_import(const char* json, uint32_t size, uint64_t* new_handle);
```

Export as C symbols with cdecl on Windows; no native pointers/booleans/STL types cross ABI. Required includes UTF-8 trailing NUL; input size excludes it. Snapshot/export queries with null output return BufferTooSmall plus required; retry larger buffer and tolerate changed snapshot size. `plv_poll` consumes the next event only after successful copy; sizing cannot pop it. Empty poll returns NoEvent, required=0. Snapshot/export never mutate. Submit enqueues validated commands; outcome is emitted on poll after apply, not assumed from enqueue success. Status enum: Ok=0, BufferTooSmall=1, NoEvent=2, InvalidArgument=3, InvalidHandle=4, UnsupportedVersion=5, Busy=6, InternalError=7. Domain rejection appears in CommandOutcome. Dispose handles through SafeHandle; double destroy rejects safely. Import builds a new context, never overwrites active state on parse failure.

## Geometry, input and time

Unity coordinate convention uses metres and its normal world axes; transform calibrated outlet/receiver geometry into the registered lab frame before submitting. ToolInput contains toolId, sampleSequence, captureMonotonicNs, posePositionM, normalized quaternion, trackingValid, actuator01, geometryProfileHash, tool revisions, and capture estimates with destination IDs. Input units/coordinate-frame ID are explicit. Core validates profile IDs, finite geometry, ownership, fractions and recency; rejects stale/future samples. Research input stale cutoff=100 ms (configuration + evidence record). Pose/contact calculations occur in one LabGeometryAdapter shared by both input modes.

Latest unconsumed pose sample replaces earlier samples per tool; committed transfers never coalesce. Rendering updates head/controller visuals every frame; native transport uses 20 ms initially. If a frame would require more than two outstanding transport ticks, enter TimeDiscontinuityHold instead of processing an unbounded catch-up pour. Clear unconsumed samples, keep simulation/material frozen and require fresh baselines plus neutral actuator and explicit Continue. During ordinary operation a complete tick's material amount uses calibrated flow×deltaS, source limits and capture geometry.

## Scheduling and failure

One in-flight and one replaceable pending fast-equilibrium request per vessel; round-robin fairness. No kinetic coalescing. Budget admission checks gross ingress+egress since that vessel's latest eligible solved snapshot, plus relevant non-transfer invalidation. Precision: 100 ms state-age goal and 0.05 ml unobserved-transfer goal; bulk: 200 ms/0.5 ml. Commit only when the proposed next tick stays within both. Age is measured from the oldest unresolved dependency change in simulation time; request/publication wall latency is also recorded. Do not age idle unchanged observations.

ComputeHold freezes session material/time; tracking/focus/time holds use the same explicit recovery state machine. If required chemistry is genuinely Unsupported, record unsupported observations instead of repeatedly enqueueing impossible solves; ordinary exploration can continue within supported transport capabilities. Numeric failure cannot be silently treated as unsupported chemistry. Results are never silently graded while stale/failed.

Initial watchdog diagnostic threshold=2 s wall time, hard release acceptance requires tested bounded engine convergence/failure controls. A blocked native call cannot be forcibly detached/deleted/reused. Keep head/UI/save responsive, disallow live Continue and preserve state. The process/application restart path is explicit when safe worker recovery is impossible. No accumulating replacement workers. This fault path and clean shutdown must pass before publishing the solver on a target.

## Persistence and limits

Serialize schema, identity, material/tool state, valid model observations, accepted geometry/model parameters and mode/assistance. Write ordered checksummed journal records and atomic checkpoints with durable platform flush semantics; Save success only after completion. Dirty checkpoint interval earlier of 30 simulation/30 wall seconds. Crash recovery discards incomplete trailing records, never fabricates complete events. Full trajectory playback is not promised: semantic replay/recorded observations are available; recompute uses pinned engine/package or explicit version comparison.

Research structural limits: 16 vessel inventories, 64 tool inventories, 4 sinks, 256 queued discrete commands/results (reserve capacity before commit), one pending input per tool, one live session, 16 MiB session import, 32 MiB scientific package, 256 MiB journal per session. Keep command identity index on disk; receipt working cache ≤4096 entries. Reaching storage limits holds material with Save/Export/Start new branch options, no silent deletion. Published device workloads may be lower than structural limits and require measurements; 10-vessel acceptance is not assumed. Resource and package limits are schema/configured, not arbitrary hidden edits.
