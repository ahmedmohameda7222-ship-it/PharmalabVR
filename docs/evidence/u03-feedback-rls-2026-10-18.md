# U03 immutable instructor feedback — verified scoped implementation

**Date**: 2026-10-18 · **Production**: Supabase Free project `hhbnlhceehgbskzdtorn` · **Release status**: PARTIAL, G-U1 OPEN.

### Exact source and Red→Green
- RED tests `6572f4ed625f129d747e8c9d7748b65453945fc0` / delimiter correction `29de57ec92fb1d70e65bd0ac5da1921042ee3f37`; native tooling and isolated Postgres identified absent `university_record_feedback`.
- Functional migration `supabase/migrations/20261018090000_instructor_feedback_rpc.sql` at `8f88c1761209d4f96c271c4f9848392fbdad8e3c`. One foreign-tenant synthetic fixture SELECT was correctly denied by RLS, so the fixture used an owner-staged opaque submission ID at `490508432df4048405f0572a308f1da8854781fc` without exposing records to the foreign user.
- Separate visible/hidden/cross-tenant RLS assertions at `b55af0252849e9d077cadce81a2ff39c5daa3300`. Both isolated Supabase PostgreSQL jobs **SUCCESS**: push `39158497069` job `117766616056` and PR `39158501190`, all 12 source migrations, 16 RLS-enabled public tables, 43/43 DO blocks, final ROLLBACK and disposable stack stopped without backup.

### Production authorization evidence
- Migration `instructor_feedback_rpc_20261018` installed as remote version `20261018191721` (source blob SHA `27977211224ea0197cf7fbaa25c0606382641d7f`).
- Public SECURITY INVOKER wrapper/private SECURITY DEFINER implementation; only active email-verified role `university_admin` or instructor assigned to the exact submission section may append; idempotent exact replay yields the same feedback ID, changed replay rejected. Feedback UPDATE/DELETE trigger rejects updates even by the table owner. Student-visible flag is server-authorized and RLS-scoped.
- Live no-data rollback smoke denied anonymous and unassigned authenticated calls; `auth.users`, `public.feedback` and audit row counts remained **zero**. Security Advisor INFO-only `audit_events` no client-readable policy is intentional; no WARN.

**Not proven**: Real Auth signup, actual university/student identities, Storage object authenticity, Native evidence upload broker, live user HTTP paths, Unity Editor/Player/XR, grade appeals, clinical/scientific acceptance. Do not promote U03 or G-U1.
