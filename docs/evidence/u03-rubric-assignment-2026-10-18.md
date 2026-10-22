# U03 Rubric and Assignment Publishing — evidence (18 October 2026)

Source branch: `feat/foundation-vr-first`, PR #1 remains OPEN and unmerged. Owner Supabase Free project: `hhbnlhceehgbskzdtorn`.

## Rubric Versions
- RED: `469de29dd17ccc0160dd07ef8110a8648e1342d2`: missing private/public RPC detected in headless SQL guardrails and isolated PostgreSQL.
- GREEN: `2f5045510db102bf65fc294a88537b911998d51b`: source `supabase/migrations/20261018110000_rubric_publication_rpc.sql`; 14 migrations, 51 fixture DO blocks, 16 RLS tables, final rollback. Push Actions `39165679182` and PR `39165683724` SUCCESS.
- Live applied: `rubric_publication_rpc_20261018`, remote version `20261018210006`, SHA blob `6c2ff8fb7e5886476aaf79609873a021a22efc6d`. Anonymous/unassigned role denied under no-data rollback. Hash verification uses canonical PostgreSQL `jsonb::text` rather than arbitrary raw JSON bytes.

## Published Assignments
- RED: `edbb60344bfb31561a30de98d9c9b1510f8fcb1e`: SQL missing RPC, isolated PostgreSQL failed at the expected function.
- GREEN: `37170465f8216236053cd320636351c45c1590a1`: source `supabase/migrations/20261018120000_assignment_publication_rpc.sql`; 15 migrations, 56 real PostgreSQL DO blocks, 16 RLS tables, final rollback. Push `39166396995` and PR `39166400384` SUCCESS. Snapshot pins a published Lesson and Rubric in the same institution/course/section; unassigned staff, students and foreign tenant fail; exact retry returns same UUID, conflicting retry fails. Published row UPDATE/DELETE blocked by trigger. Read visibility tested for enrolled section student.
- Live applied: `assignment_publication_rpc_20261018`, remote version `20261018210953`, SHA blob `738fae7c3f87f8179d2a76c24a049b3a07a6cb52`. Anonymous/unassigned rollback smoke PASS. Counts unchanged: 0 Auth users, 0 assignments, 0 audit events. Security Advisor has only intentional INFO about private audit_events.

## Unverified
Real email-verified student/instructor Auth client, academic onboarding, signed Native Storage evidence upload and attestation, grade end-to-end, Unity Editor/Player/XR, physical VR acceptance all remain NOT_RUN. G-U1 and G-R1 still OPEN. No remote fixture data inserted, no change to C++17 scientific authority. Remote API migration history versions differ from repo filenames: **do not use** `supabase db push` on production; manually review and apply the next migration through authorized owner connector.
