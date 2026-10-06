# Delivery review

## Confirmed implementation and evidence

- The untouched v3 package verifier passed over 285 files, 17 tasks, 95 specified tests, six original documents, and 40 ideal-reference cases.
- The native C++ library compiles and tests on GitHub-hosted Windows 2022/MSVC and Ubuntu 24.04/GCC. CI publishes the actual shared library, test executable, and pinned database.
- One StateExecutor owns material. Transfers conserve represented water/components/reference volume and route or reject overflow explicitly. Tool models retain rinse, tip, residual, and in-flight parcels.
- The production adapter constructs complete IPhreeqc inputs, loads the bundled `minteq.v4.dat`, extracts named columns, rejects failed/missing inputs, and is tested for water, strong acid/base, and context reuse. The ideal adapter remains a separately identified numerical reference.
- A bounded single solver worker runs outside queue locks; the scheduler rejects stale completions, enforces gross-volume/age budgets, and represents Unsupported, Pending, Stale, and Current separately. Holds require neutral input, a fresh baseline, and an explicit continue action.
- Session export/import round-trips material, pause/time state, ordered command receipts, next command identity, and per-tool input watermarks. The journal detects truncated and corrupt tails. Unity-side save replacement uses flushed temporary files.
- Unity source uses one native core for desktop and XR. It includes a boot mode selector, desktop camera/pick/place/actuator input, XR geometry samples, generated inspectable tool/panel assets, committed-volume liquid visuals, truthful measurement states, English/Arabic content with RTLTMPro and licensed Noto fonts, research-only protocols, and strict build entry points.
- Repository tooling tests the environment report and performance analyzer. Performance publication requires a real Player run, a five-minute warmup, exactly three 30-minute captures, finite samples, and zero recurring compute holds.

## Plan, review, critic, alternatives and long-term result

**Plan:** Deliver the largest independently verifiable native and Unity-source foundation while preserving the v3 capability boundaries. **Review:** Native changes are exact-head CI tested on two operating systems; Python reference/tooling tests and package integrity are independently rerun. **Critic:** Unity source could not be imported or compiled locally, so API/package/serialization defects may remain and there are no legitimate Player screenshots. The working receipt representation is restored correctly but full on-disk identity compaction and 256 MiB lifecycle policy need further implementation. Physical flow, optical response, mixing, performance, comfort, and scientific validity are not established. **Alternatives/long term:** Run the committed manual Unity workflow on a licensed pinned runner, fix any Editor-derived defects on this same PR, then perform simulator, desktop Player, and separate physical-device/experimental campaigns. Do not promote Research observations or change calibrated flow to satisfy performance targets.

## Explicit evidence boundaries

The following remain `EvidenceMissing`: Unity package resolution and `packages-lock.json`; Unity EditMode/PlayMode results; serialized scene/prefab inspection; Windows Desktop/VR Player builds; Android APK/IL2CPP/signing; actual screenshots; simulator journey; physical headset/controller tests; target frame/thermal/power captures; participant/human-factor studies; burette calibration; tracer/mixing validation; optical validation; and experimental scientific validation.

No web application, browser simulator, fabricated build, fabricated screenshot, fabricated measurement, or fabricated hardware/validation claim is included. The PR remains open and unmerged for planner review.
