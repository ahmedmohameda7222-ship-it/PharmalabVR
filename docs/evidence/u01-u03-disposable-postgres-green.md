# U01–U03 local Supabase PostgreSQL integration — VERIFIED

**Platform:** GitHub Actions Ubuntu 24.04 isolated Supabase CLI/Docker runtime, database PostgreSQL 17 per `supabase/config.toml`. Not linked to owner production project.

**Exact results:**
- Push run `39102130003`, job `117600383468`: SUCCESS.
- PR run `39102133277`: SUCCESS.
- Source migrations: 10 applied during `supabase start`.
- Public RLS enabled table count from `pg_class`: 16.
- `psql -X -v ON_ERROR_STOP=1 -f supabase/security-tests/tenant_isolation.sql`: 32 PostgreSQL DO blocks completed, followed by ROLLBACK.
- Local containers destroyed without backup; workflow never executes `supabase link`, `db push`, or receives owner project credentials.

The fixture covers known two-university Auth identities, RLS visibility, revoked membership, invitation and revocation operations, course/section/enrollment management, student submission and grade approval/immutability; the precise assertions are in the SQL source. This proves **those SQL fixtures**, not real email verification, API transport, Native export byte attestation, HTTP Storage operations or end-user student pilot.

**G-U1 remains OPEN.** The Supabase Free production project still has no enrolled users/academic records. Unity Editor, Player and XR verification remain intentionally deferred.
