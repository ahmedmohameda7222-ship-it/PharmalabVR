# P00-P16 task reviews

Every entry records the required Plan, Review, Critic, and Alternatives/Long-term pass. “Partial” and “source-only” are intentional evidence states, not completion claims.

## P00 — Environment and authority

**Plan:** Verify the package, establish the named repository and isolated feature branch, retain v3 authority, and record prerequisites. **Review:** Package verification passed and the environment doctor is test-covered. **Critic:** The local host lacks CMake/C++, Unity, and VR hardware. **Alternatives/long term:** Use hosted native compilers, keep Unity evidence blocked, and never publish source references.

## P01 — Native contract foundation

**Plan:** Define a stable JSON C ABI before implementation. **Review:** ABI lifecycle, exact NUL sizing, UTF-8 boundary, queue polling, and snapshot operations compile and pass on Windows/Linux. **Critic:** Schema coverage and fuzzing are narrower than the full contract. **Alternatives/long term:** Generate schema bindings and add sanitizer/fuzz jobs without changing ABI 1.

## P02 — Material authority

**Plan:** Make a native state executor authoritative for vessels, water, components, capacity, revision, and conservation. **Review:** Deterministic creation, rejection, and round-trip tests pass. **Critic:** Solids, gas, residue, solubility, density models, and advanced inventory operations are absent. **Alternatives/long term:** Add explicit phase pools and migrations; never infer mass from presentation state.

## P03 — Transport

**Plan:** Implement fixed and live research transport atomically against authoritative state. **Review:** capacity, conservation, geometry validation, tool rinse/tip/residual/in-flight parcels, and 10/20/40 ms convergence tests pass. **Critic:** physical profile parameters remain research assumptions, not measured calibration. **Alternatives/long term:** retain parcel authority while replacing profile constants only through versioned measurement evidence.

## P04 — Scientific adapter

**Plan:** Separate state authority from production and reference observation adapters. **Review:** bundled IPhreeqc compiles into the native library, loads pinned `minteq.v4.dat`, extracts named results, and passes water/acid/base/context-reuse tests; 40 ideal cases pass separately. **Critic:** the bundled source's internal version macro appears stale relative to the pinned source identity, so runtime and source identities remain distinct. **Alternatives/long term:** preserve both identities and add measured memory/convergence evidence before promotion.

## P05 — Scheduler, holds, and instruments

**Plan:** Bound scientific work and prevent transport from outrunning observations. **Review:** a single bounded worker executes immutable requests outside queue locks; scheduler freshness, stale-result rejection, fairness, age/gross-volume admission, and neutral/fresh/explicit hold release are tested. **Critic:** blocked-engine duration instrumentation and a device workload profile remain missing. **Alternatives/long term:** keep the worker alive on diagnostic hold rather than terminating native contexts, and publish only measured workloads.

## P06 — Unity integration

**Plan:** Pin Unity packages and provide C ABI lifecycle/build entry points. **Review:** source contains SafeHandle interop, 20 ms driver, scenes generator, and strict build functions. **Critic:** it has not been imported or compiled by Unity and no package lock or serialized output exists. **Alternatives/long term:** run AllAssets first in the pinned Editor, inspect serialized references, then run EditMode/PlayMode suites.

## P07 — XR rig and geometry

**Plan:** Map XR controller poses into common lab-frame samples and keep XR inactive unless selected. **Review:** the adapter shares the geometry function tested against desktop samples and fails closed on unbound transforms. **Critic:** no real XRI rig, actions, prefabs, ownership manager, or simulator journey exists. **Alternatives/long term:** author inspectable XRI assets and test simulator/headset parity using the same command stream.

## P08 — Tools and visual truth

**Plan:** Keep tool mechanics and presentation subordinate to native state. **Review:** native residual parcels are tested; Unity generators create inspectable burette/panel prefabs, and liquid fill derives from committed volume with an edit-mode contract test. **Critic:** assets are source-only until generated and inspected by the pinned Editor; glass, stereo readability, and screenshots are unverified. **Alternatives/long term:** repair Editor-derived issues on this branch, then capture actual visual truth evidence.

## P09 — XR deployment variants

**Plan:** Keep research indicator and mixing capabilities explicit and independently gated. **Review:** native generic indicator composition retains quantity/solvent/provenance and never claims validated optical color; homogeneous mixing is explicit while local mixing stays Unsupported without tracer evidence. **Critic:** the Unity scientific presenter and optical/tracer validation are incomplete. **Alternatives/long term:** qualify pH independently and never let absent color/mixing evidence block supported manipulation.

## P10 — Desktop, UI, and localization

**Plan:** use the same native core with mouse/keyboard sampling and English/Arabic data. **Review:** source includes a no-headset boot selector, desktop camera, pick/place/actuator controls, focus/time holds, shared geometry, robust JSON localization, RTLTMPro, and licensed Noto Latin/Arabic fonts. **Critic:** Unity compilation, complete screen/spatial panel styling, accessibility pass, Player journey, and screenshots remain unverified. **Alternatives/long term:** validate desktop first in the pinned Editor without forking chemistry or state logic.

## P11 — Persistence

**Plan:** preserve authoritative state and identities across failures and reloads. **Review:** checksummed journal recovery detects truncation/corruption; export/import restores full state, ordered terminal receipts, next sequence, and input watermarks; Unity uses flushed temporary replacement. **Critic:** the full 256 MiB disk journal, atomic checkpoint fault injection, migrations, review playback, and durable receipt-index compaction remain partial. **Alternatives/long term:** keep the native semantic log authoritative and add explicit retention choices before pruning history.

## P12 — Research protocols

**Plan:** encode protocol limitations and capability gates rather than imply validity. **Review:** two research-only protocol definitions and a runner track cumulative versus stage delivery/refill without mutating the core; certified scoring rejects Research/Unsupported/stale observations. **Critic:** Unity execution is unverified and counterbalancing, audit export, and validation evidence are absent. **Alternatives/long term:** keep Open Lab independent and require explicit validation before any certified scoring.

## P13 — Testing and CI

**Plan:** run native tests on Windows/Linux and make licensed Unity verification explicit. **Review:** exact-head hosted native jobs publish self-contained binaries/database; current official checkout/artifact actions are pinned, Python tooling runs on both hosts, and a manual pinned self-hosted Unity lane fails closed when its licensed Editor is absent. **Critic:** that Unity lane has not run; sanitizers, fuzzing, Android, simulator, and hardware evidence remain missing. **Alternatives/long term:** keep simulator, Player, and physical-device evidence as separate lanes.

## P14 — Performance and acceptance

**Plan:** define per-target records, controlled workloads, and an evidence-safe analyzer. **Review:** one/four/ten-vessel traces and analyzer tests exist; the analyzer demands a real Player flag, five-minute warmup, three 30-minute runs, finite samples, target frame budget, and zero recurring compute holds. **Critic:** all target records remain BuildNotVerified because no Player capture exists. **Alternatives/long term:** retain raw captures and never infer thermal, GPU, comfort, or power results.

## P15 — Evidence pack

**Plan:** keep evidence machine-readable and distinguish tested native results from blocked Unity/device results. **Review:** environment, progress, completion, target acceptance, decisions, reviews, real CI artifacts, package checks, and artifact hashing tooling are available. **Critic:** no legitimate screenshots or Player artifacts can be supplied. **Alternatives/long term:** append immutable Editor logs, Player hashes, images, and hardware identifiers only after real runs.

## P16 — Delivery

**Plan:** push the named branch, require exact-head CI, and leave one review PR unmerged. **Review:** PR #1 is open on `feat/foundation-vr-first`; commits and checks are real and downloadable native artifacts are retained. **Critic:** Unity/Player/device/experimental gates remain incomplete and are not reclassified as success. **Alternatives/long term:** continue corrections on this same branch/PR and never replace unavailable evidence with declarations.
