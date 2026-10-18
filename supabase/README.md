# PharmaLabVR university backend — connected project

**Status: 18 October 2026.** Authorized owner deployment on [Supabase project hhbnlhceehgbskzdtorn](https://supabase.com/dashboard/project/hhbnlhceehgbskzdtorn) (Free plan, region eu-west-1, PostgreSQL 17). Four ordered SQL migrations are installed, including security hardening of the privileged membership RPCs. This backend stores university identity and coursework metadata **not** the Native C++17 chemical solver or authoritative offline checkpoints.

## Applied migration order and remote history

| Source migration | Supabase migration name | Remote version |
| --- | --- | --- |
| `20261017000000_university_core.sql` | `university_core_20261017` | `20261018034553` |
| `20261017000100_university_rls.sql` | `university_rls_20261017` | `20261018034613` |
| `20261018000000_university_membership_rpc.sql` | `university_membership_rpc_20261018` | `20261018034623` |
| `20261018010000_university_rpc_hardening.sql` | `university_rpc_hardening_20261018` | `20261018034945` |

The project was empty before deployment, with zero Auth users, application tables and migrations. Current verification confirms 16 public RLS-enabled tables, 17 academic policies, a private evidence bucket, and three `public` `SECURITY INVOKER` membership RPCs delegating to private privileged implementations. Anonymous clients cannot EXECUTE those RPCs. The Supabase Security Advisor has **no WARN** findings. Its single INFO is intentional: `audit_events` has RLS but no client policies.

## What was and was not tested

`security-tests/live_no_data_smoke.sql` was executed on the owner-authorized project inside a transaction ending with `ROLLBACK`. It verifies the three RPCs reject anonymous callers and an authenticated role with an unauthorised synthetic identity. A post-run count confirmed zero Auth users, institution members, submissions, audit rows and uploaded objects. This is **not** full multitenant integration validation.

`security-tests/tenant_isolation.sql` is a separate **NOT_RUN** synthetic user fixture designed for a disposable local Supabase PostgreSQL instance. **Never run its synthetic Auth-user inserts on this live project**. When a disposable local stack is available, run migrations first, then run its rollback test using `psql -X -v ON_ERROR_STOP=1`.

## Access and release restrictions

- An initial `university_admin` is **not** automatically created. Never elevate an arbitrary email or fabricate a verified Auth user. A trusted, audited owner bootstrap is pending.
- Invitations apply only to existing, email-verified Auth accounts. The configured email delivery and signup experience have **not** been verified.
- Published course versions are immutable; student grades, assignments and Storage uploads remain non-writable via ordinary client table APIs.
- The evidence bucket is private and capped at 50 MiB per object by the schema. No general client upload, update or delete policy exists.
- The new U04 outbox is still **offline-only**: no cloud acknowledgement or real student sync integration.
- Security and enrollment gate G-U1 remains **OPEN** until genuine two-university policy tests, teacher/student onboarding, evidence uploads and a controlled pilot pass. Unity Editor, Player and XR tests remain **NOT_RUN**.
- A new migration is required for any further deployed schema change; do not edit already-applied SQL files or reuse a migration name.
- Service-role/secret keys are never stored in GitHub or client builds. Free-tier quotas apply; no paid upgrade, branching feature or extra project is assumed.

Exact schema migration versions, SHA evidence and outstanding gates are recorded in `docs/evidence/supabase-live-deployment-2026-10-18.json`.
