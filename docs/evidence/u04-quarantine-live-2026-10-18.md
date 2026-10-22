# U04 quarantined uploads — 18 October 2026

**Security status: partial source + Free backend deployment.** Cloud academic evidence attestation and U04 are **NOT complete**.

## Tested real code
- TDD RED Node admission `ae8f7c889c85c2b4454b0172dbd58f18f278129a`: missing `intent.mjs` in GitHub headless panel. GREEN `8e10bab48665b112ec0ca335f21cd7101da08023` source: student, ownership, institution and actual evidence byte SHA checks.
- RED `31503bb5eff8e2310668be3b6dba3ef16b5ce447`: missing `protocol.mjs`; GREEN `da26844aa3809feb7ae8563430da422c3ea08e7b` streaming byte-limit and canonical request-header validation. Headless Node tests passed in GitHub push `39177206523` and `39177409002`; these are **not Unity/HTTP integration tests**.
- DB RED `e26787d6c391b2ae407f70ef8edf8ccc67fb5a81`: after correcting SQL dollar-delimiters, isolated PostgreSQL fixture rejected missing private staging bucket. GREEN migration `bda009610b711922c8086528683e4387587e1410`: local Supabase Actions `39177062225` succeeded with **16/16** migrations, **17** RLS tables, **57** PostgreSQL fixture DO blocks, `ROLLBACK` and isolated stack teardown. Prior first GREEN attempt stopped on hard-coded old 16-table count, corrected at `1bc15b7b09788444390af5da8c058d02dcc7e0ba`.

## Live owner Free project
- Migration `evidence_staging_20261018` remote version `20261018233744`, exact SQL blob SHA `ac1d6e6b3c4e780fc9359ed332fa7312542c2fbe`.
- Supabase SQL verifies staging bucket private, RLS enabled, no anonymous/authenticated table INSERT or SELECT, no staging RLS policies, **0** Auth users, **0** staging rows and **0** academic artifact rows; security adviser WARN 0, INFO 2 for intentional private audit/staging tables with no policies.
- `student-evidence-upload` function ACTIVE version 1, `verify_jwt=true`, bundle `5a53331725b173c932f6d9bb95421cc947186dea164d834d5efcda83cd4c316e`, source commit `76e3bb28ef3fcbefc0b26e688e7b235be4ef5bb6`. Requires current Auth user verified/active in same institution, exact pending session ownership, canonical UUID headers, actual body SHA, valid bounded JSON. Writes only to separate private staging bucket and immutable metadata using a function-private secret key. No Unity/browser secret is exposed.

## Deliberate hard stops
- **Staged is not attested.** User-controlled JSON can pass a SHA check but is not an authentic Native command receipt. U03 refuses staged-only artifacts because `session_artifacts` and the academic Storage bucket are separate. The Edge reply is `staged_unverified`.
- Real authorized HTTP upload, private Storage object recovery, Native-produced evidence/receipt binding, signed server acknowledgement, retry/reconciliation and final student pilot **NOT_RUN/NOT_IMPLEMENTED** because no real Auth students/sessions exist yet.
- Native solver authority, English-only language, 66-task ledger, P11.01 SessionLoad Hold and deferred Unity final campaign unchanged. PR #1 unmerged; release gates G-U1/G-R1 OPEN.
