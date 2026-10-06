# Completion, critic review and planner handoff

## Report format

`docs/evidence/completion.json` includes schemaVersion, repository, branch, commitSha, implemented task IDs, test case results with evidence paths, build targets with status, source/package/adapter hashes, hardware profile, scientific coverage, missing prerequisites, deviations and artifact manifest. Status values Passed/Failed/BuildNotVerified/EvidenceMissing apply only to the stated scope. A task can have implemented source with missing physical evidence; record both.

## Required final checklist

- [ ] VR-first runtime rig/controller/action/spatial-panel path implemented, not a desktop app labeled VR.
- [ ] Desktop works without XR runtime/headset and uses same core/state/scientific packages.
- [ ] All P00–P16 source deliverables addressed with commits and four-stage review records; concrete blockers/deferred domain extensions named.
- [ ] Native ABI, command/revision/receipt, conservation/overflow/tool-residual, pool and independent-reference tests pass on actual code.
- [ ] IPhreeqc initialization/context-reset/failure bounds/provenance and actual platform linkage verified or target gate marked failed/unverified.
- [ ] Solver scheduling/age/volume/hold/focus/recenter preserve material, progress and correct observable freshness.
- [ ] Complete rendered Boot/setup/orientation/free lab/inspect/measure/help/pause/recovery/save/review journeys; severe interaction and readability defects fixed.
- [ ] Actual instrument scales/meniscus/glass/status captured on implemented scenes; generated reference image not substituted as evidence.
- [ ] English/Arabic/bidi/caption/non-color status support actually tested; failed language/device combinations not called supported.
- [ ] Durable save/recovery/identity compaction/model mismatch/mode metadata tests pass; actual durable loss window described.
- [ ] Research lesson definitions observe state; no experiment reaction branches or forced original endpoint assumptions.
- [ ] Available Windows/Android builds produced with real logs; missing Unity license/module/native tools do not become a green build status.
- [ ] CI checked at pushed exact SHA. Existing applicable failures resolved or stated with reason; no weakened tests.
- [ ] Benchmark/resource limits measured where available; no every-headset/every-laptop claim. Physical VR, comfort, thermal and optical evidence distinct from simulated XR.
- [ ] Source/data/assets dependency notices recorded; originals/secrets/signing keys absent from public diff; no cloud/billing/store scope added.
- [ ] Source/tests/evidence pushed to review branch/PR, no auto-merge, exact review revision supplied.

## Whole-branch four-stage review

**Plan:** Was the authorized scope delivered, including both modes, correct order and all material/evidence gates? Identify missing deliverables and why.

**Review:** Examine integrated runtime behavior, scientific identity, ledger, actual build/test/CI outputs, UX screens and persistence. Tests with fakes have a labeled limited purpose. Do not infer physical acceptance from software presence.

**Critic:** Attempt capacity/revision races, solver faults, focus/tracking recovery, placement conflicts, refill/cumulative reading, empty/unsupported mixtures, interrupted writes, stale/foreign-package observations and one-over-limit resource admission. Ask whether a student can misunderstand unavailable results or encounter unintended actions.

**Alternatives/long term:** Verify no duplicated chemistry/physics engine, no vendor SDK leaking into scientific core, no brittle hardcoded procedure outcomes, no unbounded memory and no premature account/cloud scaffolding. Explain actual justified deviations rather than inventing a “perfect forever” architecture.

## What to send back to the planner

Repository/PR URL and full commit SHA; completion.json; test/build matrix with exact commands and relevant logs; real screenshots/recordings; scientific coverage/provenance and numeric engine comparisons; actual device/benchmark profile; four-stage reviews/deviations; blocker list. Request review of that exact revision. Do not describe work after unpushed commits as remote-ready, and do not claim the planner has already reviewed the code.

“Foundation software complete” is only defensible for implemented and verified deliverables. “Published scientific capability” requires independent validation. “VR device accepted” requires physical evidence. “Universal realistic lab complete” is not a permitted claim under this package.
