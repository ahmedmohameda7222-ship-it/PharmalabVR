# U01 tenant-bound private SQL authorization regression — 18 October 2026

The initial U01 `plv_private.can_manage_section(tenant, section)` and `plv_private.can_view_course(tenant, course)` functions permitted an authenticated A admin to return `true` for arbitrary identifiers paired with A, because admin membership was checked without verifying the supplied section/course row belonged to A. This is unsafe for future privileged server RPCs even if existing row policies also limit tenant IDs.

- Test-first source SHA: `de8118b40482677c1af636362076232a97098cea`. Adds negative A-admin vs B-section/B-course and positive A-admin vs A records to the *real PostgreSQL* synthetic fixture in `supabase/security-tests/tenant_isolation.sql`. Adds static early-warning checks to `scripts/tests/test_university_sql_contract.py`.
- The pre-fix source was evaluated against the exact static predicates at `de8118b`: both section and course existence checks were absent (RED static checks).
- Production fix SHA: `704afb501c67bbd86948f506bd4f3560b7a8e5fe`. Both private predicates now require matching `institution_id` **and** record `id` before the active-member/admin/staff checks. A direct post-fix inspection at the exact SHA satisfied six required source markers (GREEN static inspection).
- **PostgreSQL/Supabase test: NOT_RUN** (no project provisioned or disposable local Supabase DB in this session). Python static test CI conclusions at this SHA must be independently inspected, and neither static test nor SQL text review is actual RLS proof. No role elevation or scientific changes were made. Original migration was edited safely before any application, so deployment later uses the corrected source in-order.

No branch reset, no PR merge, no real cloud resources, no Unity/editor/device claims. G-U1 remains OPEN.
