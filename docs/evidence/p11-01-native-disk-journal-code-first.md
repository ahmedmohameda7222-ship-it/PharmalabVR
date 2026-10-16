# P11.01 — Native single-writer disk journal and persisted receipt index

**Date:** 2026-10-16. **Product:** English-only. **Formal status:** P11.01 Open/NotReviewed. This code is a bounded Native core increment, **not a completed Native ABI persistence system** and **not actual Unity testing**.

## Implemented scope

`DurableJournal` (Native `plv/journal.hpp`, `journal.cpp`) implements bounded CRC-framed on-disk records, ordered restart reads, success only after C standard-stream flush + OS `fsync`/Windows `_commit`, strict total and record size bounds, discarded incomplete trailing records only, and failure without overwriting complete CRC-corrupted records. It assumes a **single writer**. The existing core `Journal` in-memory contract is retained.

The disk-mode overload of `ReceiptIdentityIndex(cacheLimit,journalPath)` commits canonical branch/sequence/64-hex payload hash identities to that journal before in-memory acceptance, verifies disk entries on constructor recovery, and rescans bounded on-disk history to reject duplicates after process restart even when the recent working cache evicted them. The disk-mode working cache is capped at 4096. A torn unconfirmed record is not treated as accepted; appending a retry repairs the tail and establishes a new record.

## Observed tests and commits

- Native RED `d0c65748b948fea30368eb023c226df0bc69f022`: new `R02/R03` tests failed compiling due to missing `DurableJournal` in [push 38867200142](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38867200142).
- Native GREEN `c3ca7e32bf2f9767830f5bcb5b91de86ce449cb9`: [push 38867581980](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38867581980) all three headless/Native jobs SUCCESS; [PR 38867587035](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38867587035) independently verify.
- Identity RED `666f8a52956777817b256891d0e31b48450c4763`: Ubuntu Native compile missing disk-mode receipt constructor in [push 38867980821](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38867980821).
- Identity GREEN `ced83f5f5087df6634428138d0cb1459699cc3fb`: [push 38868273059](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38868273059) Ubuntu Native and headless SUCCESS with Windows pending at evidence time; [PR 38868280086](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38868280086) status separately confirm. Native tests verify a restarted disk journal, append ordering, safe torn-tail repair, complete-record CRC fail-closed, no mutation on record size limit, restart duplicate identities and nonresurrection of torn receipt identities.

## Explicit remaining acceptance

This is **not yet called by `plv_create`, `plv_submit_command` or the Native session checkpoint path**. It therefore does not establish persistence of a real chemistry command across application crashes or material-state replay consistency. No cross-process file lock, power-loss emulation, actual disk-full test, atomic native session checkpoint, 30s wall/sim dirty policy, full import mismatch behavior, or integrated Unity Player/XR test has been delivered. Ongoing R01–R05 P11.01 remains Open; G-R1 Open, PR #1 unmerged.

## R04 first-dirty checkpoint scheduling policy (source-level only)

RED Native `905193749614b9834de1ee6ab9b5bf1c7dbbb06a` failed at compile due to missing `DirtyCheckpointPolicy` ([push 38868695259](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38868695259)). GREEN `a6e0096ad9f5903a1fda625e7f03e112db1e0602` introduces monotonic supplied wall nanoseconds and simulation seconds and a **first-dirty** deadline that repeated edits cannot postpone. It returns true on the first reached **30s wall / 30s simulation** limit, pause while dirty, backwards clock or nonfinite elapsed simulation. Explicit `acknowledgeFlush()` is only for use after an actual successful checkpoint; the source does not call it autonomously. [Push 38868969993](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38868969993) and [PR 38868977342](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38868977342) status must be rechecked before calling GREEN CI verified.

**Correction to earlier "unimplemented" sentence:** the pure **decision policy** now exists, but *no actual Native checkpoint-writer integration or 30-second checkpoint persistence proof* exists. Continue R01–R05 with current Native ABI submission/receipt pathway; do not treat this as task completion.

## R01/R04 failed-write truth (2026-10-16)
The initial policy source `a6e0096` had `acknowledgeFlush()` without an explicit durable result. This could be misused by the future checkpoint writer after a failed disk flush. Regression RED `cd584547dcfed22a95ea8a8210dcddd28178ae1d` calls `acknowledgeFlush(false)` and verifies dirty remains due; Ubuntu Native Build in [38869493757](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38869493757) failed for absent overload. GREEN `314f264b17960e13398771bd2357e0dceec1d21f` requires `checkpointDurablyWritten` and clears only on true, preserving dirty and retry on false. [Push CI 38869832555](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38869832555) and [PR 38869840059](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/38869840059) require independent full Native conclusions.

This is not a simulated real disk failure; the policy only accepts a *caller-supplied durability result*. Binding it to actual atomic checkpoint return values and real fault injection remains Open.
