# P11.01 — checkpoint absolute-path admission (18 October 2026)

**Formal P11.01 status: OPEN.** This is a bounded Native software invariant, not proof of power-loss safety, checkpoint atomicity under process death or Unity integration.

- RED test `native/tests/test_checkpoint_path.cpp`, registered via `native/tests/CMakeLists.txt` at `230f50e5536b5879d69853facc6d1a4e789f63e2`. Ubuntu push Actions `39098988539` / native job `117591186234` failed as expected: `./state.ckpt`, `../other/state.ckpt` and `sessions/state.ckpt` did not throw `std::invalid_argument`. Doctest: 124/125 cases, 18408/18411 assertions.
- GREEN source `1eb9c5d44bb08257f10edd7dffe32fec8907c15a` in `native/core/src/checkpoint.cpp`: `DurableCheckpointStore` now rejects any path where `std::filesystem::path(path_).is_absolute()` is false. No filesystem writes occur in constructor; a canonical absolute system-temporary path remains allowed. This prevents active working directory changes from silently redirecting recovered scientific checkpoints.
- At evidence creation, PR run `39099022698` Ubuntu Native and headless panel jobs SUCCESS. Windows status to be verified independently; exact GREEN push run `39099018550` is not extrapolated from an earlier SHA.
- Original 66-task ledger, P11.01 imported `SessionLoad` Hold, G-R1, Unity final verification and PR #1 state remain unchanged. Product remains English-only.

## Additional path refusal
Test SHA `feaca6233422d7ad3f855b14c9a7f77c3454ab99` reproduced accepting a filesystem directory as a checkpoint base (Ubuntu run 39099713098: two assertions failed). Source SHA `fccdf2d6965bbdf614a7c3b48132bd218ac0cd56` now rejects existing directories and path roots. Final CI at this exact SHA is pending review; P11.01 and G-R1 remain OPEN.

## Exact-SHA GREEN CI confirmation
At fixed source SHA `fccdf2d6965bbdf614a7c3b48132bd218ac0cd56`, push run `39099755840` and PR run `39099757808` all three jobs succeeded (Windows Native, Ubuntu Native, panel/headless). Ubuntu CTest reported `100% tests passed, 0 tests failed out of 1`. This verifies software path admission only; not real power cut, disk-full or Unity/Player/XR recovery. P11.01 and G-R1 remain Open.

## Symlink alias RED→GREEN (bounded Native WAL safety)
- Test `f7c528e82c40e6670aca0cc331e08cf0ef353572` proved `DurableJournal` constructor and recovery accepted existing symlink-to-regular-file aliases: push Ubuntu `39158048023`, 2 failing assertions, 126/127 cases.
- Fix `1f7a063e1785775f57290db8580906d669f2a823` rejects existing leaf symlinks during WAL construction, recovery and immediately before append. Verified all three Native/headless CI jobs SUCCESS on push `39158307456` and PR `39158312909`. Single-writer contract remains; no claim of protection from an attacker concurrently switching a link after the final check or actual device/power-loss evidence. P11.01 Open.

## Checkpoint-slot and staging symlink RED→GREEN
- RED `3ffc7ac59ec36e5f4762dccd2542c48bbe48d94f` reproduced two Native defects in Ubuntu PR `39197755551` job `117884089265`: writing through `.slot0.tmp` symlink changed an unrelated test file and returned accepted, while reading a checksum-valid `.slot0` symlink trusted a foreign checkpoint. 2 cases failed, 3 assertions failed.
- GREEN `c1e8182c851d1d692e1ff6f4155897dbbd9f0de6` now rejects a symlinked slot on load, and checks temporary checkpoint destination plus Linux `O_NOFOLLOW` opening. Exact PR Ubuntu `39198010725` job `117884860278` SUCCESS, `100% tests passed, 0 tests failed out of 1`; headless panel SUCCESS. Windows confirmation independently pending. This is a bounded software invariant, not a hostile concurrent filesystem / Windows reparse / power-loss test. P11.01 remains OPEN.

## 2026-10-19 Native hard-link aliases, WAL and checkpoint (bounded)
- WAL RED test `766eb86c55a4e40118460424afd3f9a028f658c2`, PR run `39201378108`, Ubuntu job `117894874235`: four assertions failed in new hard-linked **valid** WAL test. Existing journal path alias passed constructor/recovery and accepted a new record; the protected original bytes changed. This is a real filesystem invariant failure, not synthetic static inspection.
- WAL GREEN `8e324ae179b4e0643e294027c661062da69bf5b3` adds hard-link count admission at constructor, recovery, torn-tail resize and append boundaries. Native PR `39201610295` had Ubuntu, Windows and headless SUCCESS. The accepted tests protect against *existing/static* multiple hard links; malicious concurrent replacement remains outside the single-writer contract.
- Checkpoint slot RED `e13f455a07f7131ff1d506b0ab84c6433f857726`, PR `39201827435` Ubuntu job `117896222386`: a checksum-valid foreign checkpoint hard link was imported and permitted a new write (two assertions failed). Test cleanup correctly removes the alias before reading its original protected store at `7795147b4527cd76668cb5fca21ef5a9acbccc8a`.
- Checkpoint slot GREEN `801c1c86c56762fbe83327f75c02a33ccf70ae16` rejects any present slot whose link count is not exactly one before accepting a snapshot. PR `39202064865` all three Native/headless jobs SUCCESS; isolated PostgreSQL PR `39202064861` SUCCESS (no schema changes).
- **Not accepted:** full P11.01 SessionLoad, G-R1, actual process crash/power interruption, hostile concurrent filesystem changes, Win32 reparse-point race, Unity Editor/Player/XR. Original 66-task ledger, permanent English-only app and open/unmerged PR #1 preserved.
