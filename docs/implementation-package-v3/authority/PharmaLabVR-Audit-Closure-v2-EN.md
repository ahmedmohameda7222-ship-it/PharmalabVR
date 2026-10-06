# PharmaLabVR Audit Closure — v2

Date: 6 October 2026. Scope: design/document review, not implemented-software acceptance.

The v2 design, implementation plan and UX specification supersede the earlier foundation plan. The original English critical review remains a historical finding record. **DesignResolved** below means the documents now specify a correction; every row remains **PendingEvidence** for implementation/runtime acceptance.

| Finding | Design correction | Owning plan tasks | Evidence still required |
|---|---|---|---|
| R1 — global revision conflicts | Per-vessel material revisions; session sequence orders events; immutable historical results never overwrite current material | 1, 5 | Concurrent vessel traces, continuous-flow progress, stale-result tests |
| R2 — capacity authority | Core atomically computes actual source debit, destination credit and overflow | 1, 4 | Capacity race, source depletion, spill and duplicate-command tests |
| R3 — scheduling/global pause | Separate render/transport/solve schedules; bounded fair queue; age and gross volume budgets; explicit protective hold | 5, 9 | Sustained device timing, fairness, overload and neutral-control recovery |
| R4 — mixed status meanings | Independent support, computation, freshness and package maturity fields | 1, 5, 7 | Schema cases and student understanding of status/recovery |
| R5 — water-only transfer envelope | Typed material selection and units; unsupported phase operations explicit; future schema migrations | 1, 4, 11 | Unsupported-selection rejection and schema compatibility |
| R6 — reaction conservation | Conserved pools separated from species; first profile derived observations only; future reaction commits revision-matched | 1, 2, 4, 11 | Element/pool balances, transport conservation; future coupled phase tests |
| R7 — numerical goals versus real validity | Independent references, realistic capability evidence matrix, uncertainty and held-out checks | 2, 6, 10 | Actual independent datasets and supported-range reports |
| R8 — apparent realism beyond profile | Visible capability limits; no implied atmosphere/heat support; task eligibility tied to capabilities | 6, 7, 11 | UI comprehension and unsupported-content rejection |
| R9 — late device validation | Early native/tool/readability probe before detailed scene assets | 0, 3, 9 | Actual target builds and physical headset tests |
| R10 — unbounded history/resources | Bounded queues; checkpoint identity index; separate playback/recompute; explicit retention coverage | 8, 9 | Compaction, duplicate/crash/import tests and long-session resource profiles |
| U1 — first-use orientation | Setup plus optional grab/water/valve/reading/pause orientation | 3, 7 | First-session observation and severe blocker resolution |
| U2 — precision manipulation | Exclusive component capture, calibrated valve mapping, separate calibrated geometry and hit areas | 3, 4, 7 | Two-hand flow control, accidental-selection and alternative-input comparison |
| U3 — graduation readability | Magnified instrument view preserves measurement geometry; device-specific angular legibility tests | 3, 7, 9 | Reading accuracy across accepted devices/text profiles |
| U4 — moving/dense panels | Stable repositionable world panel; direct bench interaction and menu ray separated | 3, 7 | Seated/reach/readability testing of rendered panels |
| U5 — incomplete recovery | Distinct student errors, unsupported science, compute and tracking holds; explicit resume; checkpoint branch | 5, 7, 8 | Fault-injection journeys with no unintended transfers |
| U6 — seated accessibility | Reach tray, bench placement and recenter rules; no calibrated instrument resizing | 3, 7 | Seated and dominant-hand journey testing |
| U7 — accessibility semantics | Non-color UI cues/captions; scientific alternatives declare objective changes; Arabic tested | 7, 11 | Font/bidi/device checks and adapted assessment review |
| U8 — absent visual specification | Companion UX flow, interaction vocabulary and visual direction; blockout gate before art | 3, 7 | Actual screens/recordings and formative student tests |

## Four-stage final review

**Plan:** The immediate build sequence is reproducible environment → contracts → independent science and early device probe → transport/scheduling → calibrated observations and full UX → persistence/device acceptance → capability publication. This limits expensive downstream work before the key risks are measured.

**Review:** All ten engineering/science and eight UX audit findings now have decisions, owners and evidence gates. Material authority and derived observations are consistent; pruning cannot turn a rejected command into a committed transfer. Older plans are explicitly superseded.

**Critic:** The largest unresolved risk is reliable real behavior across combinations, local mixing and endpoint cues. The current six-material research scope cannot support arbitrary iodine/chloride or unmodeled chemistry. Target headset support and scientific tolerance goals also remain unmeasured. A polished scene cannot resolve either gap. Written UX specifications cannot establish usability.

**Alternatives/long term:** Model-driven local science with portable interfaces remains the recommendation. It avoids per-experiment outcome scripts and an unreliable remote prediction dependency. More detailed physics is justified only when the current model fails relevant evidence gates. OpenXR remains the portability interface, while device-specific input profiles and tests determine practical support. Future reaction/phase coupling requires its own plan; the first profile's asynchronous observation semantics must not be reused blindly.

**Verdict:** The revised documents are coherent enough to start the foundation implementation sequence. They do not prove the selected engine, scientific ranges, fluid model or devices will pass. Start with Tasks 0–3; retain the explicit gates before publication and before implementing more scientific domains.

## Verification performed for this revision

Document checks only: audit-to-task mapping, contract consistency, dependency order, future versus existing artifact distinction, and preservation of the user’s pre-implementation boundary. No application, native solver integration, hardware benchmark, rendered UX or student study was performed in this revision.
