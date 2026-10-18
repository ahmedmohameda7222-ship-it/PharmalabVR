# P11.01 — checkpoint absolute-path admission (18 October 2026)

**Formal P11.01 status: OPEN.** This is a bounded Native software invariant, not proof of power-loss safety, checkpoint atomicity under process death or Unity integration.

- RED test `native/tests/test_checkpoint_path.cpp`, registered via `native/tests/CMakeLists.txt` at `230f50e5536b5879d69853facc6d1a4e789f63e2`. Ubuntu push Actions `39098988539` / native job `117591186234` failed as expected: `./state.ckpt`, `../other/state.ckpt` and `sessions/state.ckpt` did not throw `std::invalid_argument`. Doctest: 124/125 cases, 18408/18411 assertions.
- GREEN source `1eb9c5d44bb08257f10edd7dffe32fec8907c15a` in `native/core/src/checkpoint.cpp`: `DurableCheckpointStore` now rejects any path where `std::filesystem::path(path_).is_absolute()` is false. No filesystem writes occur in constructor; a canonical absolute system-temporary path remains allowed. This prevents active working directory changes from silently redirecting recovered scientific checkpoints.
- At evidence creation, PR run `39099022698` Ubuntu Native and headless panel jobs SUCCESS. Windows status to be verified independently; exact GREEN push run `39099018550` is not extrapolated from an earlier SHA.
- Original 66-task ledger, P11.01 imported `SessionLoad` Hold, G-R1, Unity final verification and PR #1 state remain unchanged. Product remains English-only.

## Additional path refusal
Test SHA `feaca6233422d7ad3f855b14c9a7f77c3454ab99` reproduced accepting a filesystem directory as a checkpoint base (Ubuntu run 39099713098: two assertions failed). Source SHA `fccdf2d6965bbdf614a7c3b48132bd218ac0cd56` now rejects existing directories and path roots. Final CI at this exact SHA is pending review; P11.01 and G-R1 remain OPEN.
