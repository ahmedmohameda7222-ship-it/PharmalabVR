# PharmaLabVR — 20 October 2026 CI teardown and U04 stage filename hardening

This document records real RED→GREEN test outcomes without promoting release gates.

## Isolated Supabase Actions teardown
On the same commit `0b6c58c6909d0e174d516fe1a939649993c69147`, Push run `39352126405` passed and PR run `39352131215` failed **only** at `supabase stop --no-backup`. The PR job already logged success for local Auth/RPC/Edge/Storage behavior and PostgreSQL fixture. Docker's attempt to stop the disposable containers returned exit 1; start logs also included Docker Hub rate-limit messages.

A source test suite `scripts/tests/test_supabase_teardown.mjs` was registered in headless CI before the cleanup script existed. The test at commit `3b7b767ed63f09d9e28aa7206aeb77757f5711f7` failed as expected on missing script (`39357350108` panel). The bounded implementation `aace1ef36b79aaa5007d09c7bd97629271faa4b6`:
- makes up to three stop attempts without backups;
- verifies no Docker container whose **exact project-name pattern** matches `supabase_.*_pharmalabvr` remains;
- rejects Docker state-query failure and left-over containers, including after an apparent successful CLI exit;
- never touches containers owned by other projects.

Workflow commit `5fc3a7d8d83e85640c045bdd30652a7bc2cac498` enabled it on Push and PR. Four synthetic CLI/Docker tests passed in `39357543027` panel. The actual owner-unlinked disposable PostgreSQL/HTTP test and teardown passed in Push `39357537454` and PR `39357543057`. Native Windows, Ubuntu and panel also passed at this SHA.

## U04 local durable outbox stage
- RED commit `60ceefa6d2eb6e94d20233cb272d5487c837e76d`, panel of Native PR `39357934199`: a file symlink pre-created as `<outbox>.tmp` pointed to a protected science file; new enqueue changed that file. Failure message: `Predictable outbox .tmp symlink truncated an unrelated science file.`
- Functional fix `09c625cc782ccfdd29114b2a6bc6b45602020de8`: runtime `UniversitySyncOutbox.cs` now uses `<outbox>.<random GUID>.tmp` with `FileMode.CreateNew` instead of predictable create-and-truncate staging. This prevents overwriting an existing known temp alias.
- An older test intentionally blocked staging by creating a `.tmp` directory. After eliminating that path, the test needed a legitimate **replace-backup** failure instead. Test commits `1f0e25d3afa47a471fecb03d7a0b48d264ec9015` and `86fa384d1ab9f308ae711b40fffdc45469af07e8` repaired fixture setup, preserving its non-mutation guarantee.
- Exact commit `86fa384d` PR run `39358399797` panel SUCCESS: **76** source-linked U04 .NET assertions, and **4/4** Node teardown tests. Ubuntu Native SUCCESS; Windows Native remained in build phase when written. The EditMode source test added at `079ebede8cb8ccc51da800b6dc3bc9177f793b81` is authored but Unity execution **NOT_RUN**.

## Gates
Neither patch produces authenticated Native receipt attestation, signed academic acknowledgment or completed U04 cloud synchronization. No server schema/migrations were changed in this increment. P11.01, G-U1 and G-R1 are OPEN. Unity Editor/Player/XR tests stay deferred to the final local Codex campaign. PR #1 remains open/unmerged. English-only remains permanent.
