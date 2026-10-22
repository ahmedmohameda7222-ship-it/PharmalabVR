# PharmaLabVR U03/U04 execution evidence — 18 October 2026

## U03 instructor grade approval (applied, partial)
- RED source commit `5058c3f7869db64445f4732bc09e853ee2904d14`: Ubuntu Actions `39097752998` failed on absent `university_approve_grade`. This is the expected TDD failure.
- Functional SHA `92596757cd130ee98a8d13ea5c3709b8799b45de`; expanded synthetic tests `5a84afd05fc759b286e347538233fa0f51b2f958`. Push `39097901591` and PR `39097904022` completed with Windows Native, Ubuntu Native and headless panel SUCCESS.
- Applied migration `supabase/migrations/20261018040000_grade_approval_rpc.sql` on authorized Free project `hhbnlhceehgbskzdtorn` as remote `20261018044135`, blob SHA `f48a4b2dc4cd3a990dd5b582e0b6340d5975ba23`.
- Post-deployment inspection: private SECURITY DEFINER implementation and public SECURITY INVOKER wrapper, authenticated-only EXECUTE, anonymous denied; one immutable-approved-grade trigger. Live no-data rollback smoke denied anonymous and unassigned authenticated grade actions. Zero Auth users, institutions, grades or audit events remain. Security Advisor has no WARN findings; public audit_events INFO deny policy is intentional.
- Authored positive instructor, negative student/foreign tenant, duplicate-score, score range, immutable grade and one-time audit tests in the disposable-only `supabase/security-tests/tenant_isolation.sql`. These full seeded PostgreSQL cases were **NOT_RUN**. Score is a normalized 0..100 percentage, with 3 decimal precision; grade appeals/versioning are future workflows.

## U04 evidence byte checks (source only, pending integration)
- RED `4fb6c8b37758df6286b32a15efe9ea7406fb7552`: panel job on push `39098299855` rejected missing byte-verified enqueue API (`CS1061`).
- New production `UniversityEvidenceIntegrity.cs` at `6e11e383481f6978d0670bc4766270e4c11c66c8` validates SHA-256 of supplied Native receipt, science-package and exported evidence **bytes**, with size limits before persisting a pending local intent. Source-linked headless build at `a42027ed8e7077798409a3fc8e5004b7bef7e301`; Unity EditMode test authored, NOT_RUN. Latest CI conclusions to be inspected.
- This does **not** verify the actual origin or semantics of Native receipts, upload to private Storage, sign server acknowledgements or clear the pending outbox. A trusted server-side U04 upload broker, native producer connection and verified sync receipt remain missing.

## Unchanged gates
- U03 remains partial; unimplemented course enrollment/publishing and grade appeal paths must not be reported as complete.
- G-U1 and G-R1 OPEN. Unity Editor/Player/XR final campaign NOT_RUN. Native 66-task ledger and P11.01 SessionLoad Hold preserved. English-only. PR #1 remains open and unmerged.
