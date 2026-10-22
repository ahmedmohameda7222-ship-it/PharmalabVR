# U03 canonical published lesson versions — partial delivery evidence

**Project:** `hhbnlhceehgbskzdtorn`, Supabase Free. Date: 2026-10-18. G-U1 remains OPEN.

- Source: `supabase/migrations/20261018100000_lesson_publication_rpc.sql`, functional commit `78936b1f1a838426967c770d11f2be676503118b`, blob SHA `c083781ff3167c974e948b37eef8fd42ba53ee76`.
- RED: test SHA `fde67649b60fb1cd4b31da6fee6edbcdf9ae32e0`, disposable local PostgreSQL run `39159007745` reported function missing for first student denial case.
- GREEN: isolated PostgreSQL push `39159245779` job `117768857478` and PR `39159251579` SUCCESS: 13/13 migrations, 16 public RLS tables, **47/47 DO blocks**, final ROLLBACK and container cleanup. Tests include A student/B admin denial, assigned instructor publish, exact replay, mismatched digest, immutable published content and one audit record.
- Live applied version `20261018193506`. Public SECURITY INVOKER → private SECURITY DEFINER, no anonymous EXECUTE. The initial rollback smoke used intentionally wrong digest and therefore failed at input validation (`22023`) before membership; the corrected canonical-digest synthetic caller test SHA `b648c358427251e58ef20f5678df590dbaec4c94` yielded the expected insufficient-privilege denies, under ROLLBACK.
- No real `auth.users`, `lesson_versions` or audit rows exist. Security Advisor INFO-only `audit_events` no client policies is intentionally private; no WARN.

**Format contract:** SHA-256 is computed from PostgreSQL `jsonb::text`, so an equivalent but differently formatted raw JSON file is not itself a valid hash claim. This publication is coursework metadata, not a Native scientific state transition. Rubric versions, assignment publishing, real Storage uploads, Auth email verification and Unity Editor/Player/XR tests are still unverified.
