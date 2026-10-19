# PharmaLabVR university backend — connected project

**Status: 18 October 2026.** Authorized owner deployment on [Supabase project hhbnlhceehgbskzdtorn](https://supabase.com/dashboard/project/hhbnlhceehgbskzdtorn) (Free plan, region eu-west-1, PostgreSQL 17). Seventeen ordered SQL migrations are installed, including security hardening of the privileged membership RPCs. This backend stores university identity and coursework metadata **not** the Native C++17 chemical solver or authoritative offline checkpoints.

## Applied migration order and remote history

| Source migration | Supabase migration name | Remote version |
| --- | --- | --- |
| `20261017000000_university_core.sql` | `university_core_20261017` | `20261018034553` |
| `20261017000100_university_rls.sql` | `university_rls_20261017` | `20261018034613` |
| `20261018000000_university_membership_rpc.sql` | `university_membership_rpc_20261018` | `20261018034623` |
| `20261018010000_university_rpc_hardening.sql` | `university_rpc_hardening_20261018` | `20261018034945` |
| `20261018020000_owner_bootstrap.sql` | `owner_bootstrap_20261018` | `20261018035637` |
| `20261018030000_student_submission_rpc.sql` | `student_submission_rpc_20261018` | `20261018042735` |
| `20261018040000_grade_approval_rpc.sql` | `grade_approval_rpc_20261018` | `20261018044135` |
| `20261018050000_section_enrollment_rpc.sql` | `section_enrollment_rpc_20261018` | `20261018053627` |
| `20261018060000_course_creation_rpc.sql` | `course_creation_rpc_20261018` | `20261018054744` |
| `20261018070000_section_creation_rpc.sql` | `section_creation_rpc_20261018` | `20261018055029` |
| `20261018080000_section_staff_rpc.sql` | `section_staff_rpc_20261018` | `20261018060400` |
| `20261018090000_instructor_feedback_rpc.sql` | `instructor_feedback_rpc_20261018` | `20261018191721` |
| `20261018100000_lesson_publication_rpc.sql` | `lesson_publication_rpc_20261018` | `20261018193506` |
| `20261018110000_rubric_publication_rpc.sql` | `rubric_publication_rpc_20261018` | `20261018210006` |
| `20261018120000_assignment_publication_rpc.sql` | `assignment_publication_rpc_20261018` | `20261018210953` |
| `20261018130000_evidence_staging.sql` | `evidence_staging_20261018` | `20261018233744` |
| `20261018140000_staging_session_registration.sql` | `staging_session_registration_20261018` | `20261018235349` |

The project was empty before deployment, with zero Auth users, application tables and migrations. Current verification confirms 16 public RLS-enabled tables, 17 academic policies, a private evidence bucket, and three `public` `SECURITY INVOKER` membership RPCs delegating to private privileged implementations. Anonymous clients cannot EXECUTE those RPCs. The Supabase Security Advisor has **no WARN** findings. Its single INFO is intentional: `audit_events` has RLS but no client policies.

## Verified disposable PostgreSQL regression

Push [39102130003](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39102130003) and PR [39102133277](https://github.com/ahmedmohameda7222-ship-it/PharmalabVR/actions/runs/39102133277) successfully started an **unlinked local Supabase stack**, applied all ten repository migrations, exercised the synthetic two-university fixture under real PostgreSQL/RLS with 32 DO blocks, rolled back the fixture, and stopped the stack without backups. This establishes database-side tests for defined synthetic cases, **not** real email delivery, production Auth flows, Storage object bytes, or Unity/VR integration. The workflow never has the owner project URL or credentials.

## What was and was not tested

`security-tests/live_no_data_smoke.sql` was executed on the owner-authorized project inside a transaction ending with `ROLLBACK`. It verifies the three RPCs reject anonymous callers and an authenticated role with an unauthorised synthetic identity. A post-run count confirmed zero Auth users, institution members, submissions, audit rows and uploaded objects. This is **not** full multitenant integration validation.

`security-tests/tenant_isolation.sql` is a separate synthetic user fixture verified by isolated CI, designed for a disposable local Supabase PostgreSQL instance. **Never run its synthetic Auth-user inserts on this live project**. When a disposable local stack is available, run migrations first, then run its rollback test using `psql -X -v ON_ERROR_STOP=1`.

## Access and release restrictions

- An initial `university_admin` is **not** automatically created. The private owner-only SQL function `plv_private.owner_bootstrap_institution` is installed, but it requires an existing email-verified application Auth user and chosen institution metadata. It has no `anon`, `authenticated`, or `service_role` EXECUTE permission. Never elevate an arbitrary email or fabricate a verified Auth user. A trusted, audited owner bootstrap is pending.
- Invitations apply only to existing, email-verified Auth accounts. The configured email delivery and signup experience have **not** been verified.
- Published course versions are immutable; student grades, assignments and Storage uploads remain non-writable via ordinary client table APIs.
- The evidence bucket is private and capped at 50 MiB per object by the schema. No general client upload, update or delete policy exists.
- The new U04 outbox is still **offline-only**: no cloud acknowledgement or real student sync integration.
- Security and enrollment gate G-U1 remains **OPEN** until genuine two-university policy tests, teacher/student onboarding, evidence uploads and a controlled pilot pass. Unity Editor, Player and XR tests remain **NOT_RUN**.
- A new migration is required for any further deployed schema change; do not edit already-applied SQL files or reuse a migration name.
- Service-role/secret keys are never stored in GitHub or client builds. Free-tier quotas apply; no paid upgrade, branching feature or extra project is assumed.

Exact schema migration versions, SHA evidence and outstanding gates are recorded in `docs/evidence/supabase-live-deployment-2026-10-18.json`.

## U03 receipt-bound submissions (partial)
`public.university_submit_attempt(tenant,assignment,lab_session,idempotency_key,evidence_sha256)` delegates to private authorized SQL. The API rejects unauthenticated, nonstudent, un-enrolled, unpublished, late and non-owned session attempts; a matching server-attested `session_artifacts` record and private Storage object must exist. No client may create these metadata rows directly. A verified U04 upload broker is still missing, so positive submissions are blocked in the currently empty project. The test fixture's synthetic Auth users must **never** be inserted into this live project. See `docs/evidence/u03-submission-progress.md`.

## Grade approval and evidence integrity status
The approved-score RPC records one instructor/administrator-approved 0–100 percentage grade, immutable after approval and audited. The private function checks tenant membership, assigned instructor section, published assignment and rubric; the public wrapper is invoker-only. Local U04 `UniversityEvidenceIntegrity` can hash receipt/package/export bytes before staging an outbox intent. Neither feature establishes a completed student pilot or a trusted cloud upload. See `docs/evidence/u03-grade-u04-evidence-2026-10-18.md`.

## U03 academic administration — partial
Owner-approved Supabase Free migrations now include audited `university_create_course`, `university_create_section` and `university_enroll_student` RPCs. Each requires an email-verified active `university_admin` for the requested tenant, and validates the institution/section/student identities against normalized database rows. Audited instructor assignments are deployed. Lesson/rubric publishing remains not implemented.

**Important:** The remote migration versions were assigned by the Supabase API and differ from repository filenames. Do not run `supabase db push` against the owner project; it could replay installed SQL. The `university-postgres.yml` workflow tests the original files only in an isolated, unlinked local Supabase stack.

## Staff feedback and student visibility (partial U03)
`public.university_record_feedback` allows an active instructor assigned to the submission's section, or an active institution admin, to append a tenant-scoped immutable feedback row with an audited idempotency key. A `student_visible=false` note is visible to authorized section staff only; the current RLS policy grants a student only `student_visible=true` feedback for their own submission. Verified using 43 real PostgreSQL disposable fixture DO blocks (12/12 source migrations) on both push and PR CI; not yet tested through a real Auth/HTTP student client. No grade appeal, trusted U04 evidence ingestion, owner bootstrap, or live student pilot is claimed.

## U03 canonical lesson publication (partial)
`public.university_publish_lesson_version` creates an immutable published lesson version once an active instructor assigned to the course (or a tenant admin) has been verified. The server recomputes SHA-256 from PostgreSQL's canonical `jsonb::text` representation; clients must hash those exact canonical bytes rather than an arbitrary input JSON file. Every accepted publication has an immutable audit request and idempotent exact replay. Synthetic multi-tenant PostgreSQL acceptance is verified; real Auth/HTTP and student assignment/lesson-visibility pilot remain open. Published lessons do not automatically become student assignments. See `docs/evidence/u03-lesson-publish-2026-10-18.md`.

## U03 curriculum publication status
An active assigned instructor or institution admin can now publish a canonical SHA-256-verified immutable rubric version and then publish a section assignment pinned to an already published lesson and rubric from that same course. The assignment is immutable after publication and its request is audited/idempotent. In isolated PostgreSQL, 56/56 fixture DO blocks passed against 15 migrations and 16 RLS tables (with ROLLBACK). These backend authoring functions do **not** prove actual student onboarding, Native evidence attachment or any Unity integration; see `docs/evidence/u03-rubric-assignment-2026-10-18.md`.

## U04 staging is **not** Native attestation
Owner Free project now has a private `plv-university-evidence-staging` bucket and unexposed, immutable `public.evidence_upload_staging` table with RLS enabled and **no** client policies. `student-evidence-upload` Edge Function v1 (JWT verification on) receives up to 1 MiB of JSON, checks actual SHA-256, active student and owned pending session, and stages bytes under a tenant-prefixed UUID object key. It returns `staged_unverified`, never creates `session_artifacts`, and never marks the offline outbox synced. No real Auth users or live authorized HTTP tests exist. The staging object cannot satisfy the academic submission RPC's separate protected evidence-bucket requirement. See `supabase/functions/student-evidence-upload/README.md` and `docs/evidence/u04-quarantine-live-2026-10-18.md`.

## U04 untrusted student Native session registration
An authenticated, email-verified active student can call `public.university_register_staging_session(institution_id, local_session_id, native_branch_id, science_package_sha256, request_id)` to receive a tenant-bound server session UUID for quarantined uploads. The claimed branch/digest are **not Native-authenticated**: the record starts at `current_revision=0`, `sync_state='pending'`; retry of the exact request UUID is idempotent, while conflicting replays are rejected and logged. The U03 grade/submission flow still requires a genuine nonzero committed revision and a vetted artifact from the **different** academic evidence bucket. Headless PostgreSQL verified the source registration, but no real Auth/HTTP/native application journey exists. See `docs/evidence/u04-staging-session-2026-10-18.md`.

## U04 student staging registration and C# transport

An active, verified student can self-register a **pending revision-zero** database lab session through `public.university_register_staging_session`; Native branch/package fields remain **untrusted** until an independent attestation. `UniversityStagingSessionRegistration` is a Unity-compatible, source-linked C# client for this PostgREST RPC using the public publishable key and a student JWT. `UniversityStagingTransport` computes the SHA-256 of raw JSON export bytes and sends them over HTTPS to the already deployed private staging Edge function. Headless fake-HTTP tests passed on both Windows and Ubuntu PR CI, but authorized HTTP against a real student account and Unity Editor/Player have not been run. Even HTTP 201 does **not** mark the local outbox synced.
