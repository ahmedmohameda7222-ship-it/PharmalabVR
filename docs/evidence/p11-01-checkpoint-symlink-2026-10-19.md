# P11.01 checkpoint leaf symbolic-link safety — 19 October 2026

Original code under `native/core/src/checkpoint.cpp` opened `session.ckpt.slot0.tmp` with `std::fopen(..., "wb")`, which follows symbolic links and can truncate unrelated files. The checkpoint reader also followed a symlinked `slot0`, accepting a foreign yet valid checksummed snapshot.

**RED:** `3ffc7ac59ec36e5f4762dccd2542c48bbe48d94f`, Ubuntu PR Actions `39197755551`, job `117884089265` failed with 2 cases and 3 assertions (one accepted redirected write, one modified file and one accepted foreign slot). The sentinel file was created inside an isolated temporary test directory; no user file touched.

**GREEN:** `c1e8182c851d1d692e1ff6f4155897dbbd9f0de6`, C++17 code now rejects a symlinked checkpoint slot before reading and rejects an alias in the temporary writer path. On non-Windows systems the stage open also sets `O_NOFOLLOW` so leaf symlink switching cannot redirect the OS descriptor. Ubuntu Native PR `39198010725`, job `117884860278`, completed SUCCESS with CTest reporting `100% tests passed, 0 tests failed out of 1`; panel/headless SUCCESS. Windows job still requires exact-SHA confirmation.

Not claimed: hostile concurrent parent-path replacement, Windows reparse-point race, power-cut / device flush durability or Unity Editor/Player/XR tests. Single Native writer contract is still required; `P11.01`, imported `SessionLoad` Hold and `G-R1` remain OPEN. PR #1 remains open and unmerged.
