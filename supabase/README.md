# PharmaLabVR university backend — source-only (U01)

**Status, 17 October 2026:** Source-authored migrations, **not applied**. The owner has not provisioned a PharmaLabVR Supabase project. Do not use an unrelated connected project or deploy without explicit owner authorization. This backend manages identity, coursework, assessment and verified evidence metadata; it is **not** the Native C++17 chemical solver or checkpoint store.

## Ordered SQL

1. `migrations/20261017000000_university_core.sql` — normalized tenants, cross-tenant composite FKs, immutable evidence/submissions, RLS enabled and API privileges revoked before any policy.
2. `migrations/20261017000100_university_rls.sql` — verified-active membership helpers, student/section-scoped SELECT, own profile updates, private evidence Storage read policy. All academic writes are deliberately blocked from public clients; U02/U03 must add authenticated, audited server workflows before these tables become usable for institution administration.

Published curriculum is immutable after publication; version changes require a new version row. Evidence keys are opaque `<institution_uuid>/<artifact_uuid>`. Only a server-approved upload may create private storage objects in a later workstream. No client Storage INSERT/UPDATE/DELETE permissions are added.

## Local validation once a disposable Supabase/Postgres stack exists

Use a **local, disposable Supabase database**, not the owner's production project. Install a compatible Supabase CLI via its current official instructions and run `supabase --help`, `supabase migration --help`, and `supabase db --help` before selecting exact CLI flags. For a local stack, `supabase start` / `supabase db reset` may be appropriate after verifying CLI help. Real RLS tests require Supabase's actual `auth.users`, `auth.uid()`, `storage.buckets`, `storage.objects` and PostgreSQL role definitions; plain-text static checks do **not** establish RLS enforcement.

The destructive synthetic fixture script `security-tests/tenant_isolation.sql` starts a transaction, inserts fake Auth records in a local database, impersonates `authenticated`, exercises tenant-specific SELECT, denied academic mutations, private storage reads, revoked roles, composite FKs and duplicate idempotency, then rolls everything back. Run only against a disposable local stack that already has both migrations:

```sh
psql "$LOCAL_SUPABASE_DATABASE_URL" -X -v ON_ERROR_STOP=1 -f supabase/security-tests/tenant_isolation.sql
```

Do not store database URLs, refresh tokens, service-role keys or real student records in Git. No migration, policy or CI check is considered validated until its actual log, SHA and database identity are recorded. Never run this synthetic fixture on a real project.

## Not yet implemented / release-blocking

- U02: invitation acceptance, authenticated memberships, authorized admin provisioning, audited role changes, revocation.
- U03: server-only lesson publishing, authorized grading, active enrollment and idempotent submission endpoints with a durable audit trail.
- U04: offline outbox and verified private evidence upload/download, short-lived signed URL broker, hash/size checks and conflict fork policy.
- U05: English-only university portal, real two-tenant Auth/RLS/Storage integration and G-U1 evidence.
- Retention/privacy review and real pilot before any student onboarding.

The separate U01–U05 tracker is `docs/evidence/university-execution.json`. Existing P00–P24 (66 tasks), G-R1 and P06.02 formal next task remain unchanged. Unity/Player/XR tests remain deferred.
