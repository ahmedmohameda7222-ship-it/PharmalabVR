# Test catalogue — executable assertions and evidence limits

Use IDs as test-name prefixes and evidence references. A unit test that verifies its own fake does not satisfy the integrated case. Keep numeric fixtures in `fixtures/`; native tests exercise the actual executor/adapter. Unity tests exercise instantiated prefabs/rig/scene/core, not just strings. Human/physical cases are recorded honestly when unavailable. Failure paths are as important as normal operation.

## Native ABI, preparation and commands

| ID | Required assertion |
|---|---|
| C00 | Doctor reports real available versions/paths and exits nonzero for required missing tools; missing hardware is distinct from failed tool discovery. |
| A01 | ABI=1; create valid handle; unknown schema/version rejects without creating live state. |
| A02 | Invalid/double-destroyed handles reject; repeated create/destroy has no growing native allocation count. |
| A03 | Snapshot/poll sizing returns required NUL-inclusive length; too-small poll buffer does not consume event; successful retry returns same event once. |
| A04 | UTF-8 labels/IDs, ulong counters and finite doubles survive round-trip; malformed JSON/NaN/Infinity/excess size rejects with no native exception crossing. |
| M01 | Prepared six stocks contain the expected Na/Cl/acetate amounts and reference volume for concentration×volume; no hardcoded pH field supplies result. |
| M02 | Mixing preserves represented pools/water/reference volume within abs 1e−12 mol +rel 1e−9 (appropriate water/volume tolerances recorded separately). |
| M03 | No species-level free-H+ conservation assumption; acetate acid/base speciation changes while total acetate stays constant. |
| M04 | Empty inventory never divides by zero; empty chemical observations are Absent/Unsupported as appropriate, not a made-up pH. |
| M05 | Independent inventory-bearing tools/tips/sinks are counted in the full represented ledger; moving a tool/camera does not create material. |
| C01 | Stale expected revision on a touched vessel rejects all effects; change on unrelated vessel does not reject valid command. |
| C02 | Identical identity/payload returns previous terminal outcome with no second debit/event. |
| C03 | Same identity/different payload returns Conflict and leaves ledger unchanged. |
| C04 | Rejected sequence has a terminal rejection; compaction does not reinterpret it as committed. Unknown historical identity does not execute by guessing. |
| C05 | Full result/command queue returns Busy before mutation; invalid units/profile/IDs/capacity leave state unchanged. |

## Transport and geometry

| ID | Required assertion |
|---|---|
| T01 | Two sequential/queued 5 ml captures into 7 ml free capacity: source −10 ml, receiver +7 ml, spill +3 ml under accepted additive convention, with proportional pools. |
| T02 | A source with 4 ml rejects fixed 5 ml request atomically; live delivery cannot remove more than 4 ml or make negative inventories. |
| T03 | Multiple capture fractions and uncaptured/overflow material sum to the source parcel exactly within tolerance; deterministic rounding allocation. |
| T04 | Rinse adds water, transfers contaminated residual to waste and leaves measured model residual; no magic clean reset. |
| T05 | Closing valve stops new outlet flow; an existing hanging/in-flight parcel remains then lands/spills once. |
| T06 | Source/receiver state changes before commit recalculate current capacity/availability, never accept stale frontend amounts. |
| T07 | Wrong coordinate/profile/revision, nonunit quaternion or capture fraction sum>1 rejects without unintended material change. |
| T08 | 10/20/40 ms traces converge in total delivery, residual and captured/spilled pools; report actual differences against defined research error budget, not visual resemblance. |
| G01 | Registered calibrated outlet/receiver geometry uses metres/lab frame; identical world geometry from both adapters yields same capture proposal. |
| G02 | UI/camera/controller mesh is never an inventory receiver; panel clicks do not generate capture/actuation. |
| G03 | Invalid placement keeps held tool/contents; no penetration teleport or forced discard. |

## Science and scheduling

| ID | Required assertion |
|---|---|
| S01 | Independent ideal root reproduces all supplied pH/charge residual fixtures; near-equivalence tested without piecewise endpoint scripts. |
| S02 | Engine same-input/same-assumption comparisons meet ≤0.001 pH when assumptions actually match; differences from ideal activity assumptions are documented separately. |
| S03 | IPhreeqc output preserves Na/Cl/acetate totals and finite solvent/charge diagnostics within the declared tolerance; input does not “charge balance” Na/Cl away. |
| S04 | Same immutable request after varied prior cells matches clean context; old selected-output rows cannot be returned after error. |
| S05 | Unsupported material/T/atmosphere/phase does not produce a falsely valid observation; six-profile restrictions survive import. |
| S06 | Engine initialization/solve/repeated load reports latency and actual engine/database/adapter identity; placeholder runtime version is not provenance. |
| S07 | Pathological finite inputs, nonconvergence and lifecycle shutdown exercise actual engine bounds; native worker remains isolated. Missing proof fails engine deployment gate. |
| O01 | Vessel B changes cannot stale an A observation with matching dependencies; changed A state marks its observation Stale. |
| O02 | Older solve never overwrites newer solved observation/current material; pH completion cannot advance color solved revision. |
| O03 | Continuous multi-vessel input yields fair scientific progress; per-vessel pending slots bounded; every committed transport event retained. |
| O04 | Precommit precision/bulk age+volume budgets hold together; test maximum calibrated flow, delayed solve and actual tick quantum. No post-violation warning counted as success. |
| O05 | Circulation ingress/egress counted gross, not net zero; idle unchanged observation never stales by wall time alone. |
| O06 | Failed computation, Unsupported model, Stale observation and Research maturity remain orthogonal in API/UI. |
| O07 | Equilibrium coalescing does not erase material history; kinetic/multiphase request cannot accidentally use same coalescing/derived-commit path. |
| H01 | ComputeHold freezes material/simulation, preserves journal and leaves head/menu/save responsive. |
| H02 | Tracking/focus/time hold clears unconsumed samples; neutral+fresh baseline+explicit Continue needed; no recovered catch-up pour. |
| H03 | Parked tool/physical controller alignment is explicit; re-grab/reset cannot spill/jump material without a deliberate valid action. |
| H04 | Blocked-worker injection diagnoses stall, does not launch unlimited replacements or destroy used context; safe actions only. Injection test does not prove actual engine cancellation. |
| H05 | >two-tick backlog/stale input produces TimeDiscontinuity/InputHold rather than accumulated delivery. |
| L01 | One beyond each entity/queue/import/package limit rejects predictably without uncontrolled allocation or corrupted state. |
| L02 | Receipt cache≤4096; persistent identity still prevents double execution; long journal limit exposes real save/export options. |
| L03 | Repeated context/session create/load/destroy and long-session tests show bounded working memory; report actual measured bounds. |
| I01 | Indicator inventory/solvent preserved; adding two indicators does not simply choose the last-added color. |
| I02 | Valid pH + unavailable color cannot enable certified color endpoint assessment. Research theoretical display clearly marked Approximate. |
| I03 | Mixing provider/region/tick changes measured against tracer or convergence evidence; no fake local-realism promotion from swirl animation. |

## Unity, input and student journeys

| ID | Required assertion |
|---|---|
| U01 | Actual native interop sizes/statuses/lifetimes work in Editor/Player; plugin absence disables lab with explanation, not silent fake success. |
| U02 | Scene references/one active adapter/CoreDriver subscriptions valid; scene unload releases handles and returns to boot safely. |
| X01 | Grip tool/trigger valve target distinct; actuator ownership exclusive; mounted burette cannot also drag. |
| X02 | Two controllers hold receiver and actuate valve independently; no hidden single-hand assumption in native ownership. |
| X03 | Simulator/injected tracking invalidation freezes material, while controller/head display stays responsive. Physical tracking test separate. |
| X04 | Seated/standing/left/right/recenter preserve calibrated size/material and use neutral remapping. |
| X05 | Ray menu event does not trigger physical actuation; highlight/haptics/help match actual input profile. |
| X06 | Production Android/WindowsVR rig is not the simulator rig; development simulator excluded from production builds. |
| V01 | Liquid height/receiver/spill/droplet IDs reflect actual committed volume; unsupported optical values not fabricated. |
| V02 | Actual burette marks 0–50 increasing downward, menu scale does not change ml calibration; magnification does not provide numeric answer. |
| V03 | Meniscus/graduations/glass readable in implemented stereo/desktop images; severe occlusion/hue errors require fix. Physical headset legibility remains separate. |
| V04 | Panel stable/repositionable and does not cover outlet/receiver; inspect/reference statuses readable at target resolution/distance. |
| V05 | Colorless stocks stay colorless; graphical presets leave native science and instrument geometry unchanged. |
| D01 | Windows Desktop enters usable lab without headset/runtime; unavailable XR cannot block desktop. |
| D02 | Mouse pick/move/height/tilt/place/valve/measurement works; wheel mode conflict prevented. |
| D03 | Right-drag/zoom camera leaves held lab-world pose/actuator unchanged; opening/clicking UI has no click-through pour. |
| D04 | Focus loss mid-flow preserves ledger and clears input; returning does not automatically resume. |
| D05 | Invalid placement/clamp/receiver interaction does not auto-correct procedure or quantity; one-pointer assistance recorded. |
| D06 | Paused mode transition preserves package/material/assistance and resets pose baseline; unavailable mode/task explained. |
| J01 | First-use setup/orientation can be completed/skipped/replayed; Help contains actual controls. |
| J02 | Inspect/Pause/Save/Recenter/Restart/Review paths complete, actions only offered when working. |
| J03 | Students distinguish Updating, Unsupported, calculation and tracking/focus faults; no student error score for system faults. Formative evidence needed. |
| J04 | Retrieval/bench placement works seated and left-handed; no forced smooth locomotion or equipment resizing. |
| J05 | English/Arabic strings, mixed formula/decimal/unit bidi, scaling/captions/non-color status; locale switch preserves session. Actual font/shaping tested. |

## Sessions, protocols, builds and performance

| ID | Required assertion |
|---|---|
| R01 | Save/load exact material/model/branch and represented inventory; import publishes only a validated new context. |
| R02 | Corrupt/oversized/unknown schema input cannot alter active session; missing package permits recorded review only where available. |
| R03 | Interrupted append/checkpoint/replace recovers complete prior records; no half-transfer; last durable boundary reported accurately. |
| R04 | Save flush failure cannot display Saved; dirty checkpoint fires on wall time while paused/held. |
| R05 | Compacted/pruned committed/rejected identities never re-execute; mismatched historical payload verification unavailable is truthful. |
| R06 | Recorded playback distinct from recompute/migration; model hash mismatch cannot silently become current. |
| R07 | Desktop/XR same semantic operations produce equivalent pools/science; record source mode and assistance, not bitwise tracking trajectory equality. |
| R08 | Restart checkpoint creates new branch identity, preserves original history and current conservation. |
| Q01 | Protocol runner observes events/state and cannot mutate chemical results. |
| Q02 | Missing required observable/freshness/maturity/mode prevents certified score; Research guidance remains clearly identified. |
| Q03 | Burette refill/reading reset does not reset cumulative delivered material; mixed-acid endpoint never forced to HCl-only threshold. |
| Q04 | Free lab remains free; changing procedure data reuses same science/core without experiment outcome code. |
| B01 | Real WindowsDesktop executable build/start, correct plugin, no headset/runtime dependency. |
| B02 | Real WindowsVR build imports rig/plugin/OpenXR profiles; no-headset run explains availability. Physical VR pass separate. |
| B03 | Real Android ARM64 IL2CPP build/native plugin linkage and runtime feature configuration; physical install/thermal acceptance separate. |
| B04 | Clean checkout reproduces available build/test targets with pinned packages/vendor identity; unconfigured license CI never masquerades as passed build. |
| K01 | 1/4/10-vessel controlled traces record exact device/build/settings, frame p95/p99, solver age/volume and queue/hold counts. |
| K02 | Target desktop 60 FPS/headset≥72 Hz under accepted workload; actual scientific budgets and no repeated ComputeHold required, not FPS alone. |
| K03 | Long/repeated sessions measure bounded native/managed memory/storage; thermal tests need actual devices. |
| K04 | Structural limits distinct from lower published measured workload; one-over-limit admission predicts behavior without changing flow/chemistry. |

## Test invocation and evidence

Native test names start with case IDs and are registered through CTest. Unity NUnit names include case IDs; EditMode handles schemas/asset references/geometry and PlayMode handles rigs/input/panels/core/time/focus/session. The two modes share semantic fixture assertions. Native property/fuzz tests use seed 20261006 and at least 1000 finite, supported transfer cases, then separately invalid cases; verify no partial mutation and all represented inventory totals. Fuzzing does not prove chemistry outside the defined profile.

Every case records status: Passed, Failed, BuildNotVerified or EvidenceMissing, evidence path, exact commit/toolchain/profile and scope (unit/integration/Player/simulated/physical/experimental/human). Test-only delay/blocked-worker fakes exercise orchestration; actual IPhreeqc tests exercise science/lifecycle separately. J03/J04/V03 physical ergonomics and independent optical/calibration/experiment checks cannot be marked Passed from screenshots alone.

Full CI native + Unity tests and actual available builds must pass before software completion. Unavailable license/device/data can remain explicit blockers to target/capability acceptance, not automatic passes. Fix implementation-caused failures; never weaken tests to make a report green.
